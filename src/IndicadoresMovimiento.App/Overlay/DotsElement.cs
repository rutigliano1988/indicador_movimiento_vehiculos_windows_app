using System.Windows;
using System.Windows.Media;
using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.App.Overlay;

/// <summary>Dibuja los puntos de una franja del borde de la pantalla.</summary>
internal sealed class DotsElement : FrameworkElement
{
    private readonly List<DotPoint> _points = [];
    private DotStyle _style = new();
    private Brush _fill = Brushes.Gray;
    private Pen? _outline;
    private double _offsetX;
    private double _offsetY;

    public DotsElement(ScreenEdge edge)
    {
        Edge = edge;
        IsHitTestVisible = false;
        Focusable = false;
        SnapsToDevicePixels = false;
    }

    public ScreenEdge Edge { get; }

    public void Configure(DotStyle style, Brush fill, Pen? outline)
    {
        _style = style;
        _fill = fill;
        _outline = outline;
        InvalidateVisual();
    }

    public void SetOffset(double offsetX, double offsetY)
    {
        // Evitar redibujar por cambios invisibles (menos de 1/20 de píxel).
        if (Math.Abs(offsetX - _offsetX) < 0.05 && Math.Abs(offsetY - _offsetY) < 0.05) return;
        _offsetX = offsetX;
        _offsetY = offsetY;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        DotLayout.Compute(Edge, ActualWidth, ActualHeight, _style, _offsetX, _offsetY, _points);
        var radius = _style.Radius;
        foreach (var point in _points)
        {
            drawingContext.DrawEllipse(_fill, _outline, new Point(point.X, point.Y), radius, radius);
        }
    }

    /// <summary>Pinceles para cada esquema de color (congelados para que WPF los dibuje más rápido).</summary>
    public static (Brush Fill, Pen? Outline) CreateBrushes(DotColorScheme scheme, double radius)
    {
        var outlineWidth = Math.Clamp(radius * 0.3, 1, 2);
        (Color fill, Color outline) = scheme switch
        {
            DotColorScheme.White => (Colors.White, Color.FromArgb(150, 0, 0, 0)),
            DotColorScheme.Black => (Colors.Black, Color.FromArgb(180, 255, 255, 255)),
            DotColorScheme.Blue => (Color.FromRgb(10, 132, 255), Color.FromArgb(230, 255, 255, 255)),
            // Gris oscuro con borde blanco: se distingue tanto sobre fondos claros como oscuros.
            _ => (Color.FromArgb(225, 45, 45, 48), Color.FromArgb(235, 255, 255, 255)),
        };

        var fillBrush = new SolidColorBrush(fill);
        fillBrush.Freeze();
        var pen = new Pen(new SolidColorBrush(outline), outlineWidth);
        pen.Brush.Freeze();
        pen.Freeze();
        return (fillBrush, pen);
    }
}
