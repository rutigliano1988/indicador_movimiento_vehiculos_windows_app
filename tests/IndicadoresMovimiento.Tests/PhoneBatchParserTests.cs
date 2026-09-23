using System.Numerics;
using System.Text;
using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.Tests;

public class PhoneBatchParserTests
{
    private static bool Parse(string json, out PhoneBatch? batch) =>
        PhoneBatchParser.TryParse(Encoding.UTF8.GetBytes(json), out batch);

    [Fact]
    public void Lee_muestras_con_y_sin_aceleracion_lineal()
    {
        Assert.True(Parse("""{"ang":90,"s":[[1.5,0.1,0.2,9.8],[1.52,0.1,0.2,9.8,0.01,0.02,0.03]]}""", out var batch));
        Assert.NotNull(batch);
        Assert.Equal(90, batch.ScreenAngle);
        Assert.Equal(2, batch.Samples.Count);
        Assert.Null(batch.Samples[0].LinearAcceleration);
        Assert.Equal(new Vector3(0.01f, 0.02f, 0.03f), batch.Samples[1].LinearAcceleration);
        Assert.Equal(1.52, batch.Samples[1].Timestamp, 6);
    }

    [Fact]
    public void Descarta_muestras_mal_formadas_pero_conserva_las_buenas()
    {
        Assert.True(Parse("""{"s":[[1,2,3],[1,"x",2,3],[2,0,0,9.8],[3,0,0,1e9]]}""", out var batch));
        Assert.NotNull(batch);
        Assert.Single(batch.Samples);
        Assert.Equal(0, batch.ScreenAngle);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nada")]
    [InlineData("[]")]
    [InlineData("""{"ang":0}""")]
    [InlineData("""{"s":{}}""")]
    public void Rechaza_paquetes_no_validos(string json)
    {
        Assert.False(Parse(json, out _));
    }

    [Fact]
    public void Rechaza_paquetes_demasiado_grandes()
    {
        var samples = string.Join(",", Enumerable.Repeat("[1,0,0,9.8]", PhoneBatchParser.MaxSamples + 1));
        Assert.False(Parse($$"""{"s":[{{samples}}]}""", out _));
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(90, 0, -1, 0)]
    [InlineData(-90, 0, 1, 0)]
    [InlineData(270, 0, 1, 0)]
    [InlineData(180, -1, 0, 0)]
    public void Eje_derecho_segun_el_giro_de_pantalla(int angle, float x, float y, float z)
    {
        Assert.Equal(new Vector3(x, y, z), PhoneBatchParser.FlatRightAxisForScreenAngle(angle));
    }
}
