using System.IO;

namespace IndicadoresMovimiento.App.Services;

/// <summary>Dónde se guardan los datos de la aplicación (sin necesidad de permisos de administrador).</summary>
internal static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IndicadoresMovimiento");

    public static string SettingsFile => Path.Combine(DataDirectory, "ajustes.json");

    public static string CertificateFile => Path.Combine(DataDirectory, "certificado-movil.pfx");

    public static string ErrorLogFile => Path.Combine(DataDirectory, "errores.log");
}

internal static class ErrorLog
{
    private static readonly object Gate = new();

    public static void Write(Exception exception, string context)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(AppPaths.DataDirectory);
                var info = new FileInfo(AppPaths.ErrorLogFile);
                if (info.Exists && info.Length > 512 * 1024) info.Delete();
                File.AppendAllText(AppPaths.ErrorLogFile,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
            }
        }
        catch (Exception)
        {
            // Si ni siquiera se puede escribir el registro, no hay nada más que hacer.
        }
    }
}
