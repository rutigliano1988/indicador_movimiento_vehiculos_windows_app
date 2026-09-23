namespace IndicadoresMovimiento.Core;

public enum ScreenEdge
{
    Left,
    Right,
    Top,
    Bottom,
}

public readonly record struct DotPoint(double X, double Y);

/// <summary>Geometría de los puntos (en píxeles independientes del DPI).</summary>
public sealed class DotStyle
{
    public double Radius { get; init; } = 5;

    /// <summary>Distancia entre puntos consecutivos a lo largo del borde.</summary>
    public double Spacing { get; init; } = 72;

    /// <summary>Número de filas/columnas de puntos paralelas a cada borde.</summary>
    public int Columns { get; init; } = 2;

    /// <summary>Distancia entre columnas.</summary>
    public double ColumnGap { get; init; } = 28;

    /// <summary>Margen mínimo entre los puntos y el borde de la pantalla.</summary>
    public double EdgeMargin { get; init; } = 6;

    /// <summary>Desplazamiento máximo de los puntos (debe coincidir con <see cref="CueRenderOptions.MaxOffset"/>).</summary>
    public double MaxOffset { get; init; } = 56;
}

/// <summary>
/// Posiciones de los puntos dentro de una franja pegada a un borde de la pantalla.
/// La franja es lo bastante ancha para que los puntos no se salgan con el desplazamiento máximo,
/// y a lo largo del borde el patrón se repite para que no aparezcan huecos al moverse.
/// </summary>
public static class DotLayout
{
    /// <summary>Grosor de la franja necesaria para un estilo dado.</summary>
    public static double StripThickness(DotStyle style) =>
        FirstColumnDistance(style) + (Math.Max(1, style.Columns) - 1) * style.ColumnGap
        + style.MaxOffset + style.Radius + style.EdgeMargin;

    /// <summary>Calcula los centros de los puntos para una franja de tamaño <paramref name="width"/> × <paramref name="height"/>.</summary>
    /// <param name="offsetX">Desplazamiento horizontal actual (positivo = derecha).</param>
    /// <param name="offsetY">Desplazamiento vertical actual (positivo = abajo).</param>
    public static void Compute(
        ScreenEdge edge, double width, double height, DotStyle style,
        double offsetX, double offsetY, List<DotPoint> output)
    {
        output.Clear();
        if (width <= 0 || height <= 0 || style.Spacing <= 1) return;

        var vertical = edge is ScreenEdge.Left or ScreenEdge.Right;
        var length = vertical ? height : width;
        var along = vertical ? offsetY : offsetX;
        var across = Math.Clamp(vertical ? offsetX : offsetY, -style.MaxOffset, style.MaxOffset);
        var columns = Math.Max(1, style.Columns);

        for (var c = 0; c < columns; c++)
        {
            var distance = FirstColumnDistance(style) + c * style.ColumnGap;
            var perpendicular = edge switch
            {
                ScreenEdge.Left => distance,
                ScreenEdge.Right => width - distance,
                ScreenEdge.Top => distance,
                _ => height - distance,
            } + across;

            // Columnas alternas desplazadas media separación (patrón al tresbolillo).
            var phase = (c % 2) * style.Spacing / 2;
            var start = PositiveModulo(along + phase, style.Spacing) - style.Spacing;
            for (var p = start; p <= length + style.Spacing; p += style.Spacing)
            {
                output.Add(vertical ? new DotPoint(perpendicular, p) : new DotPoint(p, perpendicular));
            }
        }
    }

    private static double FirstColumnDistance(DotStyle style) =>
        style.EdgeMargin + style.Radius + style.MaxOffset;

    private static double PositiveModulo(double value, double modulus)
    {
        var r = value % modulus;
        return r < 0 ? r + modulus : r;
    }
}
