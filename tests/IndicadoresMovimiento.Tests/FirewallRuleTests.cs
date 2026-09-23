using System.Text;
using IndicadoresMovimiento.Movil;

namespace IndicadoresMovimiento.Tests;

public class FirewallRuleTests
{
    [Fact]
    public void El_script_incluye_el_programa_y_los_puertos()
    {
        var script = FirewallRule.BuildScript(@"C:\Users\Ana\Documents\IndicadoresMovimiento-x64.exe", 47800, 47809);
        Assert.Contains(@"$exe = 'C:\Users\Ana\Documents\IndicadoresMovimiento-x64.exe'", script);
        Assert.Contains("-LocalPort '47800-47809'", script);
        Assert.Contains("-Profile Any", script);
        Assert.Contains("Remove-NetFirewallRule", script);
    }

    [Theory]
    [InlineData(@"C:\Users\O'Neill\app.exe", @"'C:\Users\O''Neill\app.exe'")]
    [InlineData("C:\\Users\\Ana\u2019s\\app.exe", "'C:\\Users\\Ana\u2019\u2019s\\app.exe'")]
    [InlineData(@"C:\Users\José Pérez\$dinero\app.exe", @"'C:\Users\José Pérez\$dinero\app.exe'")]
    public void Las_comillas_de_la_ruta_se_escapan(string path, string expected)
    {
        Assert.Equal(expected, FirewallRule.QuoteSingle(path));
    }

    [Fact]
    public void El_comando_codificado_es_utf16_en_base64()
    {
        const string script = "Write-Output 'ñandú'";
        var decoded = Encoding.Unicode.GetString(Convert.FromBase64String(FirewallRule.EncodeCommand(script)));
        Assert.Equal(script, decoded);
    }
}
