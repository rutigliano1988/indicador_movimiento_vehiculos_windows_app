using System.ComponentModel;
using System.Diagnostics;
using IndicadoresMovimiento.Movil;

namespace IndicadoresMovimiento.App.Services;

internal static class FirewallHelper
{
    /// <summary>
    /// Pide permiso de administrador (aviso de Windows) y deja que el móvil se conecte en cualquier red.
    /// Devuelve falso si el usuario cancela el aviso o la regla no se pudo crear.
    /// </summary>
    public static Task<bool> AllowPhoneConnectionsAsync(int firstPort, int lastPort) => Task.Run(() =>
    {
        if (Environment.ProcessPath is not { } program) return false;

        var encoded = FirewallRule.EncodeCommand(FirewallRule.BuildScript(program, firstPort, lastPort));
        var start = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand {encoded}")
        {
            UseShellExecute = true,
            Verb = "runas", // abre el aviso de permisos de administrador
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(start);
            if (process is null || !process.WaitForExit(TimeSpan.FromSeconds(60))) return false;
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false; // se canceló el aviso de administrador
        }
    });
}
