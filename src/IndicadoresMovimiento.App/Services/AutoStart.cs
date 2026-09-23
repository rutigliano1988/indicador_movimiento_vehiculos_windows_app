using System.IO;
using Microsoft.Win32;

namespace IndicadoresMovimiento.App.Services;

/// <summary>Arranque automático al iniciar sesión en Windows (solo para el usuario actual).</summary>
internal static class AutoStart
{
    public const string StartupArgument = "--inicio";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "IndicadoresMovimiento";

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && Environment.ProcessPath is { } path)
            {
                key.SetValue(ValueName, $"\"{path}\" {StartupArgument}");
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            ErrorLog.Write(e, "No se pudo cambiar el arranque automático");
        }
    }
}
