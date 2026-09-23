using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using IndicadoresMovimiento.App.Interop;
using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.App.Overlay;

/// <summary>
/// Ventana transparente, siempre encima y que deja pasar los clics, pegada a un borde de un monitor.
/// </summary>
internal sealed class EdgeWindow : Window
{
    private nint _hwnd;

    public EdgeWindow(ScreenEdge edge, Int32Rect boundsInPixels)
    {
        Edge = edge;
        BoundsInPixels = boundsInPixels;
        Title = "Indicadores de movimiento";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
        SizeToContent = SizeToContent.Manual;
        // Posición provisional: la definitiva se aplica en píxeles físicos con SetWindowPos.
        Left = boundsInPixels.X;
        Top = boundsInPixels.Y;
        Width = Math.Max(1, boundsInPixels.Width);
        Height = Math.Max(1, boundsInPixels.Height);
        Dots = new DotsElement(edge);
        Content = Dots;
    }

    public ScreenEdge Edge { get; }

    public Int32Rect BoundsInPixels { get; }

    public DotsElement Dots { get; }

    /// <summary>Crea la ventana nativa (sin mostrarla) y le aplica los estilos necesarios.</summary>
    public void Initialize(bool hideFromScreenCapture)
    {
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        var style = NativeMethods.GetExtendedStyle(_hwnd);
        style |= (nint)(NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED
                        | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
        NativeMethods.SetExtendedStyle(_hwnd, style);
        SetHideFromScreenCapture(hideFromScreenCapture);
        ApplyBounds();
    }

    public void SetHideFromScreenCapture(bool hide)
    {
        if (_hwnd == 0) return;
        // WDA_EXCLUDEFROMCAPTURE existe desde Windows 10 2004; en versiones anteriores no hace nada.
        NativeMethods.SetWindowDisplayAffinity(_hwnd, hide ? NativeMethods.WDA_EXCLUDEFROMCAPTURE : NativeMethods.WDA_NONE);
    }

    public void ShowWithoutActivating()
    {
        if (IsVisible) return;
        Show();
        ApplyBounds();
    }

    /// <summary>Vuelve a ponerla por encima del resto de ventanas "siempre visibles".</summary>
    public void BringToTop()
    {
        if (_hwnd == 0 || !IsVisible) return;
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // WPF reescala la ventana al cambiar de monitor; se recoloca en los píxeles exactos.
        Dispatcher.BeginInvoke(ApplyBounds, DispatcherPriority.Background);
    }

    private void ApplyBounds()
    {
        if (_hwnd == 0) return;
        var b = BoundsInPixels;
        NativeMethods.SetWindowPos(_hwnd, NativeMethods.HWND_TOPMOST, b.X, b.Y, b.Width, b.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_NOOWNERZORDER);
    }
}
