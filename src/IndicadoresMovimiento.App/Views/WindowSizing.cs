using System.Windows;

namespace IndicadoresMovimiento.App.Views;

internal static class WindowSizing
{
    /// <summary>Reduce la ventana si no cabe en la pantalla (portátiles pequeños o con mucho escalado).</summary>
    public static void FitToWorkArea(Window window)
    {
        var area = SystemParameters.WorkArea;
        window.Width = Math.Min(window.Width, Math.Max(window.MinWidth, area.Width - 32));
        window.Height = Math.Min(window.Height, Math.Max(window.MinHeight, area.Height - 32));
    }
}
