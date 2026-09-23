using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using IndicadoresMovimiento.App.Services;
using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.App.Views;

public partial class SettingsWindow : Window
{
    private readonly AppController _controller;
    private readonly DispatcherTimer _statusTimer;
    private bool _loading = true; // los controles lanzan eventos mientras se crean
    private bool _applying;

    internal SettingsWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        LoadValues();

        _controller.SettingsChanged += OnSettingsChanged;
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _statusTimer.Tick += (_, _) => UpdateStatus();
        _statusTimer.Start();
        UpdateStatus();

        Closed += (_, _) =>
        {
            _statusTimer.Stop();
            _controller.SettingsChanged -= OnSettingsChanged;
        };
    }

    private void OnSettingsChanged()
    {
        // Cambios hechos desde el menú de la bandeja, el atajo de teclado o "Restablecer".
        if (!_applying) LoadValues();
    }

    private void Update(Action<AppSettings> change)
    {
        if (_loading) return;
        _applying = true;
        try
        {
            _controller.UpdateSettings(change);
        }
        finally
        {
            _applying = false;
        }
        UpdateSourceHelp();
        UpdateStatus();
    }

    private void LoadValues()
    {
        _loading = true;
        try
        {
            var s = _controller.Settings;
            EnabledBox.IsChecked = s.Enabled;
            EnabledText.Text = _controller.HotKeyAvailable
                ? $"Mostrar los indicadores ({GlobalHotKey.Description})"
                : "Mostrar los indicadores";
            SelectByTag(SourceBox, s.Source.ToString());
            ModeAutomatic.IsChecked = s.Mode == DisplayMode.Automatic;
            ModeAlways.IsChecked = s.Mode == DisplayMode.Always;
            DotSizeSlider.Value = s.DotSize;
            SpacingSlider.Value = s.DotSpacing;
            OpacitySlider.Value = s.DotOpacity;
            SelectByTag(ColumnsBox, s.Columns.ToString(CultureInfo.InvariantCulture));
            SelectByTag(ColorBox, s.Color.ToString());
            SideEdgesBox.IsChecked = s.SideEdges;
            TopBottomEdgesBox.IsChecked = s.TopBottomEdges;
            AllMonitorsBox.IsChecked = s.AllMonitors;
            SensitivitySlider.Value = s.Sensitivity;
            InvertLateralBox.IsChecked = s.InvertLateral;
            InvertLongitudinalBox.IsChecked = s.InvertLongitudinal;
            StartWithWindowsBox.IsChecked = s.StartWithWindows;
            HideFromCaptureBox.IsChecked = s.HideFromScreenCapture;
            UpdateValueLabels();
            UpdateSourceHelp();
        }
        finally
        {
            _loading = false;
        }
    }

    private static void SelectByTag(ComboBox box, string tag)
    {
        box.SelectedItem = box.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == tag)
                           ?? box.Items[0];
    }

    private static string? SelectedTag(ComboBox box) => (box.SelectedItem as ComboBoxItem)?.Tag as string;

    private void UpdateValueLabels()
    {
        DotSizeValue.Text = $"{DotSizeSlider.Value:0} px";
        SpacingValue.Text = $"{SpacingSlider.Value:0} px";
        OpacityValue.Text = OpacitySlider.Value.ToString("P0", CultureInfo.CurrentCulture);
        SensitivityValue.Text = "×" + SensitivitySlider.Value.ToString("0.00", CultureInfo.CurrentCulture);
    }

    private void UpdateSourceHelp()
    {
        var builtIn = _controller.Sources.BuiltIn;
        SourceHelp.Text = _controller.Settings.Source switch
        {
            MotionSourceKind.BuiltInSensor when builtIn.IsAvailable =>
                (builtIn.HasGyroscope ? "Acelerómetro y giroscopio" : "Acelerómetro") +
                " de este ordenador. Funciona mejor si el ordenador no se mueve respecto al coche.",
            MotionSourceKind.BuiltInSensor =>
                "Este ordenador no tiene sensor de movimiento. Elige «Automática» y conecta el móvil.",
            MotionSourceKind.Phone =>
                "El móvil envía sus sensores por Wi-Fi. Pulsa «Usar el móvil como sensor» para conectarlo.",
            MotionSourceKind.Demo =>
                "Simula un trayecto (acelerar, frenar, curvas y una rotonda) para ver cómo se mueven los puntos.",
            _ when builtIn.IsAvailable =>
                "Se usa el sensor de este ordenador. Si conectas un móvil, se usará el móvil mientras envíe datos.",
            _ => "Este ordenador no tiene sensor de movimiento, así que se usará el móvil en cuanto lo conectes.",
        };
    }

    private void UpdateStatus()
    {
        var settings = _controller.Settings;
        var active = _controller.Sources.Active;
        var state = active.Processor.GetState();

        string title;
        Brush light;
        if (!settings.Enabled)
        {
            title = "En pausa";
            light = (Brush)FindResource("IdleBrush");
        }
        else if (state.HasData)
        {
            title = settings.Mode == DisplayMode.Always || state.Activity > 0.05
                ? "Activo: los puntos siguen el movimiento"
                : "Activo: los puntos aparecerán cuando el vehículo se mueva";
            light = (Brush)FindResource("OkBrush");
        }
        else
        {
            title = "Sin datos de movimiento";
            light = (Brush)FindResource("WarnBrush");
        }

        StatusTitle.Text = title;
        StatusLight.Fill = light;
        StatusDetail.Text = $"{active.DisplayName}: {active.StatusText}";
        Readings.Text = state.HasData
            ? $"Lateral {FormatAcceleration(state.Lateral)}   ·   Longitudinal {FormatAcceleration(state.Longitudinal)}"
            : "";
    }

    private static string FormatAcceleration(double value) =>
        value.ToString("+0.0;-0.0;0.0", CultureInfo.CurrentCulture) + " m/s²";

    private void OnEnabledChanged(object sender, RoutedEventArgs e) =>
        Update(s => s.Enabled = EnabledBox.IsChecked == true);

    private void OnSourceChanged(object sender, RoutedEventArgs e)
    {
        if (SelectedTag(SourceBox) is { } tag && Enum.TryParse<MotionSourceKind>(tag, out var kind))
            Update(s => s.Source = kind);
    }

    private void OnModeChanged(object sender, RoutedEventArgs e) =>
        Update(s => s.Mode = ReferenceEquals(sender, ModeAlways) ? DisplayMode.Always : DisplayMode.Automatic);

    private void OnAppearanceChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        UpdateValueLabels();
        Update(s =>
        {
            s.DotSize = DotSizeSlider.Value;
            s.DotSpacing = SpacingSlider.Value;
            s.DotOpacity = OpacitySlider.Value;
            if (int.TryParse(SelectedTag(ColumnsBox), NumberStyles.Integer, CultureInfo.InvariantCulture, out var columns))
                s.Columns = columns;
            if (Enum.TryParse<DotColorScheme>(SelectedTag(ColorBox), out var color))
                s.Color = color;
            s.SideEdges = SideEdgesBox.IsChecked == true;
            s.TopBottomEdges = TopBottomEdgesBox.IsChecked == true;
            s.AllMonitors = AllMonitorsBox.IsChecked == true;
        });
    }

    private void OnMotionChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        UpdateValueLabels();
        Update(s =>
        {
            s.Sensitivity = SensitivitySlider.Value;
            s.InvertLateral = InvertLateralBox.IsChecked == true;
            s.InvertLongitudinal = InvertLongitudinalBox.IsChecked == true;
        });
    }

    private void OnSystemChanged(object sender, RoutedEventArgs e) => Update(s =>
    {
        s.StartWithWindows = StartWithWindowsBox.IsChecked == true;
        s.HideFromScreenCapture = HideFromCaptureBox.IsChecked == true;
    });

    private void OnPhone(object sender, RoutedEventArgs e) => _controller.ShowPhoneWindow();

    private void OnReset(object sender, RoutedEventArgs e) => _controller.ResetSettings();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnExitApplication(object sender, RoutedEventArgs e) => _controller.Exit();
}
