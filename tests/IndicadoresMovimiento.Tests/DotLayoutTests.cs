using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.Tests;

public class DotLayoutTests
{
    private static readonly DotStyle Style = new() { Radius = 5, Spacing = 72, Columns = 2, ColumnGap = 28, MaxOffset = 56 };

    [Theory]
    [InlineData(ScreenEdge.Left)]
    [InlineData(ScreenEdge.Right)]
    [InlineData(ScreenEdge.Top)]
    [InlineData(ScreenEdge.Bottom)]
    public void Los_puntos_no_se_salen_de_la_franja_con_el_desplazamiento_maximo(ScreenEdge edge)
    {
        var thickness = DotLayout.StripThickness(Style);
        var vertical = edge is ScreenEdge.Left or ScreenEdge.Right;
        var (width, height) = vertical ? (thickness, 900.0) : (1400.0, thickness);
        var points = new List<DotPoint>();

        foreach (var offset in new[] { -Style.MaxOffset, 0, Style.MaxOffset, 3 * Style.MaxOffset })
        {
            DotLayout.Compute(edge, width, height, Style, offset, offset, points);
            Assert.NotEmpty(points);
            foreach (var p in points)
            {
                var across = vertical ? p.X : p.Y;
                var size = vertical ? width : height;
                Assert.InRange(across, Style.Radius, size - Style.Radius);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13.5)]
    [InlineData(-40)]
    [InlineData(200)]
    public void A_lo_largo_del_borde_no_quedan_huecos(double offset)
    {
        var points = new List<DotPoint>();
        DotLayout.Compute(ScreenEdge.Left, DotLayout.StripThickness(Style), 800, Style, 0, offset, points);

        var firstColumn = points.Where(p => Math.Abs(p.X - points[0].X) < 0.01).Select(p => p.Y).OrderBy(y => y).ToList();
        Assert.True(firstColumn[0] <= 0, "debe haber un punto por encima del borde");
        Assert.True(firstColumn[^1] >= 800, "debe haber un punto por debajo del borde");
        for (var i = 1; i < firstColumn.Count; i++)
        {
            Assert.Equal(Style.Spacing, firstColumn[i] - firstColumn[i - 1], 6);
        }
    }

    [Fact]
    public void Las_columnas_estan_al_tresbolillo()
    {
        var points = new List<DotPoint>();
        DotLayout.Compute(ScreenEdge.Right, DotLayout.StripThickness(Style), 500, Style, 0, 0, points);
        var columns = points.GroupBy(p => Math.Round(p.X, 3)).ToList();
        Assert.Equal(2, columns.Count);

        var a = columns[0].Select(p => p.Y).Min(y => PositiveModulo(y, Style.Spacing));
        var b = columns[1].Select(p => p.Y).Min(y => PositiveModulo(y, Style.Spacing));
        Assert.Equal(Style.Spacing / 2, Math.Abs(a - b), 6);
    }

    private static double PositiveModulo(double value, double modulus) => ((value % modulus) + modulus) % modulus;
}
