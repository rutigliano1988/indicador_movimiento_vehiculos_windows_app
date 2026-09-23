using System.Text;

namespace IndicadoresMovimiento.Movil;

/// <summary>
/// Regla del firewall de Windows para que el móvil pueda conectarse en cualquier tipo de red.
/// </summary>
/// <remarks>
/// Windows solo da permiso en el tipo de red en el que se abrió la aplicación por primera vez
/// (normalmente la Wi-Fi de casa, "privada"). El punto de acceso del móvil suele quedar como red
/// "pública", así que en el coche el firewall bloquearía al móvil. Este script, ejecutado como
/// administrador, borra las reglas previas del programa (también las de bloqueo que Windows crea si
/// se cancela su aviso) y añade una que permite la entrada en todas las redes, solo en los puertos
/// de la aplicación.
/// </remarks>
public static class FirewallRule
{
    public const string DisplayName = "Indicadores de movimiento (movil)";

    // PowerShell trata también las comillas tipográficas como comillas simples.
    private static readonly string[] SingleQuotes = ["'", "‘", "’", "‚", "‛"];

    public static string BuildScript(string programPath, int firstPort, int lastPort)
    {
        var program = QuoteSingle(programPath);
        return $$"""
            $ErrorActionPreference = 'Stop'
            $exe = {{program}}
            Get-NetFirewallApplicationFilter |
                Where-Object { [Environment]::ExpandEnvironmentVariables($_.Program) -ieq $exe } |
                Get-NetFirewallRule |
                Remove-NetFirewallRule
            New-NetFirewallRule -DisplayName {{QuoteSingle(DisplayName)}} -Direction Inbound -Action Allow `
                -Program $exe -Protocol TCP -LocalPort '{{firstPort}}-{{lastPort}}' -Profile Any | Out-Null
            """;
    }

    /// <summary>Codifica el script para pasarlo a PowerShell con -EncodedCommand (sin problemas de comillas).</summary>
    public static string EncodeCommand(string script) =>
        Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

    internal static string QuoteSingle(string value)
    {
        var escaped = value;
        foreach (var quote in SingleQuotes) escaped = escaped.Replace(quote, quote + quote);
        return "'" + escaped + "'";
    }
}
