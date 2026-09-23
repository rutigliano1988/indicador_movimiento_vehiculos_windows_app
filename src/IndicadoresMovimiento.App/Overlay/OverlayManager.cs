using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using IndicadoresMovimiento.App.Interop;
using IndicadoresMovimiento.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace IndicadoresMovimiento.App.Overlay;

/// <summary>
/// Crea las franjas de puntos en los bordes de cada monitor y las anima con el movimiento del vehículo.
/// </summary>
/// <remarks>
/// Para ahorrar batería, mientras los puntos están ocultos solo se consulta el estado 10 veces por
/// segundo; la animación a la frecuencia de la pantalla se activa únicamente cuando hay algo que mostrar.
/// </remarks>
internal sealed class OverlayManager : IDisposable
{
    private const double MaxOffset = 56;

    private readonly Func<CueState> _stateProvider;
    private readonly CueAnimator _animator = new();
    private readonly CueRenderOptions _renderOptions = new() { MaxOffset = MaxOffset };
    private readonly List<EdgeWindow> _windows = [];
    private readonly DispatcherTimer _idleTimer;
    private readonly DispatcherTimer _topmostTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private AppSettings _settings = new();
    private string _layoutKey = "";
    private bool _animating;
    private bool _visible;
    private double _lastFrame;
    private double _hiddenSince = double.NaN;

    public OverlayManager(Func<CueState> stateProvider)
    {
        _stateProvider = stateProvider;
        _idleTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        _idleTimer.Tick += (_, _) => OnIdleTick();
        _topmostTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(3) };
        _topmostTimer.Tick += (_, _) => { foreach (var w in _windows) w.BringToTop(); };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>Opacidad actual de los puntos (0 = ocultos).</summary>
    public double CurrentOpacity => _animator.Opacity;

    public void Apply(AppSettings settings)
    {
        _settings = settings.Clone();
        _renderOptions.Mode = settings.Mode;
        _renderOptions.Gain = 18 * settings.Sensitivity;

        var key = string.Join('|', settings.SideEdges, settings.TopBottomEdges, settings.AllMonitors,
            settings.DotSize, settings.DotSpacing, settings.Columns);
        if (key != _layoutKey)
        {
            _layoutKey = key;
            Rebuild();
        }
        else
        {
            ConfigureWindows();
        }

        if (!_animating) _idleTimer.Start();
    }

    private DotStyle CreateStyle() => new()
    {
        Radius = _settings.DotSize / 2,
        Spacing = _settings.DotSpacing,
        Columns = _settings.Columns,
        ColumnGap = Math.Max(24, _settings.DotSize * 2.4),
        MaxOffset = MaxOffset,
    };

    private void Rebuild()
    {
        foreach (var window in _windows) window.Close();
        _windows.Clear();
        _visible = false;

        if (!_settings.SideEdges && !_settings.TopBottomEdges) return;

        var style = CreateStyle();
        var thicknessDip = DotLayout.StripThickness(style);
        var screens = _settings.AllMonitors
            ? Forms.Screen.AllScreens
            : Forms.Screen.PrimaryScreen is { } primary ? [primary] : [];

        foreach (var screen in screens)
        {
            var b = screen.Bounds; // píxeles físicos (la aplicación declara PerMonitorV2)
            var scale = NativeMethods.GetDpiAt(b.Left + b.Width / 2, b.Top + b.Height / 2) / 96.0;
            var side = Math.Min((int)Math.Ceiling(thicknessDip * scale), b.Width / 3);
            var band = Math.Min((int)Math.Ceiling(thicknessDip * scale), b.Height / 3);

            if (_settings.SideEdges)
            {
                Add(ScreenEdge.Left, new Int32Rect(b.Left, b.Top, side, b.Height));
                Add(ScreenEdge.Right, new Int32Rect(b.Right - side, b.Top, side, b.Height));
            }

            if (_settings.TopBottomEdges)
            {
                var inset = _settings.SideEdges ? side : 0;
                var width = b.Width - 2 * inset;
                if (width > 0)
                {
                    Add(ScreenEdge.Top, new Int32Rect(b.Left + inset, b.Top, width, band));
                    Add(ScreenEdge.Bottom, new Int32Rect(b.Left + inset, b.Bottom - band, width, band));
                }
            }
        }

        ConfigureWindows();
    }

    private void Add(ScreenEdge edge, Int32Rect bounds)
    {
        var window = new EdgeWindow(edge, bounds);
        window.Initialize(_settings.HideFromScreenCapture);
        _windows.Add(window);
    }

    private void ConfigureWindows()
    {
        var style = CreateStyle();
        var (fill, outline) = DotsElement.CreateBrushes(_settings.Color, style.Radius);
        foreach (var window in _windows)
        {
            window.Dots.Configure(style, fill, outline);
            window.SetHideFromScreenCapture(_settings.HideFromScreenCapture);
        }
        UpdateWindows();
    }

    /// <summary>Mientras los puntos están ocultos: comprobar de vez en cuando si hay que mostrarlos.</summary>
    private void OnIdleTick()
    {
        var state = _stateProvider();
        var wantsVisible = _settings.Enabled &&
            (_settings.Mode == DisplayMode.Always || (state.HasData && state.Activity > 0.01));
        if (wantsVisible) StartAnimating();
    }

    private void StartAnimating()
    {
        if (_animating) return;
        _animating = true;
        _idleTimer.Stop();
        _topmostTimer.Start();
        _lastFrame = _clock.Elapsed.TotalSeconds;
        _hiddenSince = double.NaN;
        CompositionTarget.Rendering += OnRendering;
    }

    private void StopAnimating()
    {
        if (!_animating) return;
        _animating = false;
        CompositionTarget.Rendering -= OnRendering;
        _topmostTimer.Stop();
        _animator.Reset();
        UpdateWindows();
        _idleTimer.Start();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var now = _clock.Elapsed.TotalSeconds;
        var dt = now - _lastFrame;
        if (dt < 0.004) return; // WPF puede avisar varias veces por fotograma
        _lastFrame = now;

        _animator.Step(_stateProvider(), dt, _renderOptions, _settings.Enabled);
        UpdateWindows();

        // Si llevan un rato del todo ocultos, volver al modo de bajo consumo.
        if (_animator.Opacity <= 0)
        {
            if (double.IsNaN(_hiddenSince)) _hiddenSince = now;
            else if (now - _hiddenSince > 1.0) StopAnimating();
        }
        else
        {
            _hiddenSince = double.NaN;
        }
    }

    private void UpdateWindows()
    {
        var opacity = _animator.Opacity * _settings.DotOpacity;
        var visible = opacity > 0.005;
        foreach (var window in _windows)
        {
            window.Dots.Opacity = opacity;
            window.Dots.SetOffset(_animator.OffsetX, _animator.OffsetY);
            if (visible) window.ShowWithoutActivating();
            else if (window.IsVisible) window.Hide();
        }

        if (visible && !_visible)
        {
            foreach (var window in _windows) window.BringToTop();
        }
        _visible = visible;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        // Se ha conectado/desconectado un monitor o ha cambiado la resolución o la escala.
        Application.Current?.Dispatcher.BeginInvoke(Rebuild, DispatcherPriority.Background);
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        CompositionTarget.Rendering -= OnRendering;
        _idleTimer.Stop();
        _topmostTimer.Stop();
        foreach (var window in _windows) window.Close();
        _windows.Clear();
    }
}
