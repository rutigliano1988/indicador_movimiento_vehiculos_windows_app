using System.IO;
using System.Windows;
using System.Windows.Threading;
using IndicadoresMovimiento.App.Overlay;
using IndicadoresMovimiento.App.Sensors;
using IndicadoresMovimiento.App.Services;
using IndicadoresMovimiento.App.Views;
using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.App;

/// <summary>Une todas las piezas: ajustes, fuentes de movimiento, puntos en pantalla, bandeja y ventanas.</summary>
internal sealed class AppController : IDisposable
{
    private readonly Application _application;
    private readonly DispatcherTimer _saveTimer;
    private readonly bool _firstRun;
    private OverlayManager? _overlay;
    private TrayIcon? _tray;
    private GlobalHotKey? _hotKey;
    private SettingsWindow? _settingsWindow;
    private PhoneWindow? _phoneWindow;

    public AppController(Application application)
    {
        _application = application;
        Settings = SettingsStore.Load(AppPaths.SettingsFile, out var existed);
        _firstRun = !existed;
        Sources = new MotionSourceManager(AppPaths.CertificateFile);
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            Save();
        };
    }

    /// <summary>Ajustes actuales. Solo se modifican a través de <see cref="UpdateSettings"/>.</summary>
    public AppSettings Settings { get; }

    public MotionSourceManager Sources { get; }

    public bool HotKeyAvailable => _hotKey?.IsRegistered == true;

    /// <summary>Se lanza (en el hilo de la interfaz) cada vez que cambian los ajustes.</summary>
    public event Action? SettingsChanged;

    public void Start(bool fromAutoStart)
    {
        if (_firstRun) Save(); // guarda el código secreto del móvil recién generado

        _overlay = new OverlayManager(Sources.GetState);
        _overlay.Apply(Settings);
        _tray = new TrayIcon(this);
        _hotKey = new GlobalHotKey(ToggleEnabled);
        AutoStart.Set(Settings.StartWithWindows);
        _ = ApplySourcesAsync();
        _tray.Refresh();

        if (_firstRun)
        {
            ShowSettings();
            _tray.Notify("Indicadores de movimiento",
                "La aplicación queda junto al reloj de Windows. Haz clic en su icono para abrir los ajustes.");
            if (!Sources.BuiltIn.IsAvailable) ShowPhoneWindow();
        }
        else if (!fromAutoStart)
        {
            _tray.Notify("Indicadores de movimiento", Settings.Enabled
                ? "Funcionando. Los puntos aparecerán cuando se detecte el movimiento del vehículo."
                : "En pausa. Pulsa Ctrl+Alt+M o usa el icono junto al reloj para activarlos.");
        }
    }

    public void UpdateSettings(Action<AppSettings> change)
    {
        var before = Settings.StartWithWindows;
        change(Settings);
        Settings.Normalize();

        _overlay?.Apply(Settings);
        _ = ApplySourcesAsync();
        if (before != Settings.StartWithWindows) AutoStart.Set(Settings.StartWithWindows);
        _tray?.Refresh();

        _saveTimer.Stop();
        _saveTimer.Start();
        SettingsChanged?.Invoke();
    }

    public void ToggleEnabled() => UpdateSettings(s => s.Enabled = !s.Enabled);

    public void ResetSettings()
    {
        var defaults = new AppSettings();
        UpdateSettings(s =>
        {
            s.Enabled = defaults.Enabled;
            s.Mode = defaults.Mode;
            s.Source = defaults.Source;
            s.Sensitivity = defaults.Sensitivity;
            s.DotSize = defaults.DotSize;
            s.DotSpacing = defaults.DotSpacing;
            s.Columns = defaults.Columns;
            s.DotOpacity = defaults.DotOpacity;
            s.Color = defaults.Color;
            s.SideEdges = defaults.SideEdges;
            s.TopBottomEdges = defaults.TopBottomEdges;
            s.AllMonitors = defaults.AllMonitors;
            s.HideFromScreenCapture = defaults.HideFromScreenCapture;
            s.InvertLateral = defaults.InvertLateral;
            s.InvertLongitudinal = defaults.InvertLongitudinal;
            // Se conservan el arranque con Windows y la conexión con el móvil.
        });
    }

    public void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(this);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        BringToFront(_settingsWindow);
    }

    public void ShowPhoneWindow()
    {
        // Quien abre esta ventana quiere usar el móvil: si había otra fuente fija, se cambia.
        if (Settings.Source is MotionSourceKind.BuiltInSensor or MotionSourceKind.Demo)
        {
            UpdateSettings(s => s.Source = MotionSourceKind.Phone);
        }
        _ = EnsurePhoneAsync();

        if (_phoneWindow is null)
        {
            _phoneWindow = new PhoneWindow(this);
            _phoneWindow.Closed += (_, _) => _phoneWindow = null;
            _phoneWindow.Show();
        }
        BringToFront(_phoneWindow);
    }

    public void Exit()
    {
        _saveTimer.Stop();
        Save();
        _application.Shutdown();
    }

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }

    private async Task ApplySourcesAsync()
    {
        try
        {
            await Sources.ApplyAsync(Settings.Clone());
        }
        catch (Exception e)
        {
            ErrorLog.Write(e, "Error al cambiar la fuente de movimiento");
        }
        _tray?.Refresh();
    }

    private async Task EnsurePhoneAsync()
    {
        try
        {
            await Sources.EnsurePhoneAsync();
        }
        catch (Exception e)
        {
            ErrorLog.Write(e, "Error al preparar la conexión con el móvil");
        }
    }

    private void Save()
    {
        try
        {
            SettingsStore.Save(AppPaths.SettingsFile, Settings);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            ErrorLog.Write(e, "No se pudieron guardar los ajustes");
        }
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        _hotKey?.Dispose();
        _overlay?.Dispose();
        _tray?.Dispose();
        Sources.Dispose();
    }
}
