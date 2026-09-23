using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IndicadoresMovimiento.Core;

/// <summary>De dónde se obtiene el movimiento del vehículo.</summary>
public enum MotionSourceKind
{
    /// <summary>El sensor del ordenador si lo tiene; si hay un móvil conectado, el móvil.</summary>
    Automatic,

    /// <summary>Acelerómetro (y giroscopio) integrado en el ordenador.</summary>
    BuiltInSensor,

    /// <summary>Un móvil conectado por Wi-Fi que envía sus sensores.</summary>
    Phone,

    /// <summary>Movimiento simulado para ver cómo funciona.</summary>
    Demo,
}

public enum DotColorScheme
{
    /// <summary>Gris oscuro con borde blanco: se ve sobre fondos claros y oscuros.</summary>
    Automatic,
    White,
    Black,
    Blue,
}

/// <summary>Ajustes del usuario. Se guardan en JSON.</summary>
public sealed class AppSettings
{
    public const int DefaultPhonePort = 47800;

    public bool Enabled { get; set; } = true;
    public DisplayMode Mode { get; set; } = DisplayMode.Automatic;
    public MotionSourceKind Source { get; set; } = MotionSourceKind.Automatic;

    /// <summary>Multiplicador del desplazamiento de los puntos (0,25 a 3).</summary>
    public double Sensitivity { get; set; } = 1.0;

    /// <summary>Diámetro de los puntos en píxeles independientes del DPI.</summary>
    public double DotSize { get; set; } = 10;

    /// <summary>Separación entre puntos a lo largo del borde.</summary>
    public double DotSpacing { get; set; } = 72;

    /// <summary>Número de columnas de puntos junto a cada borde (1 a 3).</summary>
    public int Columns { get; set; } = 2;

    /// <summary>Opacidad máxima de los puntos (0,2 a 1).</summary>
    public double DotOpacity { get; set; } = 0.85;

    public DotColorScheme Color { get; set; } = DotColorScheme.Automatic;

    public bool SideEdges { get; set; } = true;
    public bool TopBottomEdges { get; set; }
    public bool AllMonitors { get; set; } = true;

    /// <summary>Que los puntos no salgan al compartir pantalla o grabarla (Windows 10 2004 o posterior).</summary>
    public bool HideFromScreenCapture { get; set; } = true;

    public bool InvertLateral { get; set; }
    public bool InvertLongitudinal { get; set; }
    public bool StartWithWindows { get; set; }

    public int PhonePort { get; set; } = DefaultPhonePort;

    /// <summary>Código secreto que el móvil debe enviar para que se acepten sus datos.</summary>
    public string PhoneToken { get; set; } = "";

    /// <summary>Corrige valores fuera de rango (p. ej. si se edita el JSON a mano).</summary>
    public void Normalize()
    {
        Sensitivity = ClampOr(Sensitivity, 0.25, 3, 1);
        DotSize = ClampOr(DotSize, 4, 28, 10);
        DotSpacing = ClampOr(DotSpacing, 30, 200, 72);
        Columns = Math.Clamp(Columns, 1, 3);
        DotOpacity = ClampOr(DotOpacity, 0.2, 1, 0.85);
        if (!Enum.IsDefined(Mode)) Mode = DisplayMode.Automatic;
        if (!Enum.IsDefined(Source)) Source = MotionSourceKind.Automatic;
        if (!Enum.IsDefined(Color)) Color = DotColorScheme.Automatic;
        if (PhonePort is < 1024 or > 65000) PhonePort = DefaultPhonePort;
        if (string.IsNullOrWhiteSpace(PhoneToken) || PhoneToken.Length < 8 || !PhoneToken.All(char.IsAsciiLetterOrDigit))
        {
            PhoneToken = NewToken();
        }
    }

    public AppSettings Clone() => (AppSettings)MemberwiseClone();

    public static string NewToken() =>
        RandomNumberGenerator.GetString("abcdefghijkmnpqrstuvwxyz23456789", 12);

    private static double ClampOr(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}

/// <summary>Lee y guarda <see cref="AppSettings"/> en disco.</summary>
public static class SettingsStore
{
    /// <summary>Carga los ajustes. Si el archivo no existe o está dañado devuelve los de por defecto.</summary>
    /// <param name="existed">Verdadero si había un archivo válido (para saber si es la primera ejecución).</param>
    public static AppSettings Load(string path, out bool existed)
    {
        existed = false;
        AppSettings? settings = null;
        try
        {
            if (File.Exists(path))
            {
                settings = JsonSerializer.Deserialize(File.ReadAllText(path), AppSettingsJsonContext.Default.AppSettings);
                existed = settings is not null;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            settings = null;
        }

        settings ??= new AppSettings();
        settings.Normalize();
        return settings;
    }

    public static void Save(string path, AppSettings settings)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
        var temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    public static string Serialize(AppSettings settings) =>
        JsonSerializer.Serialize(settings, AppSettingsJsonContext.Default.AppSettings);
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class AppSettingsJsonContext : JsonSerializerContext;
