using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "im-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void Si_no_hay_archivo_devuelve_valores_por_defecto()
    {
        var settings = SettingsStore.Load(Path.Combine(_directory, "no-existe.json"), out var existed);
        Assert.False(existed);
        Assert.True(settings.Enabled);
        Assert.Equal(DisplayMode.Automatic, settings.Mode);
        Assert.Equal(12, settings.PhoneToken.Length);
    }

    [Fact]
    public void Guarda_y_carga_los_mismos_valores()
    {
        var path = Path.Combine(_directory, "ajustes.json");
        var original = new AppSettings
        {
            Mode = DisplayMode.Always,
            Source = MotionSourceKind.Phone,
            Sensitivity = 1.7,
            Columns = 3,
            Color = DotColorScheme.Blue,
            InvertLateral = true,
            PhoneToken = "abcdefgh1234",
        };
        SettingsStore.Save(path, original);

        var loaded = SettingsStore.Load(path, out var existed);
        Assert.True(existed);
        Assert.Equal(SettingsStore.Serialize(original), SettingsStore.Serialize(loaded));
        Assert.Contains("\"Phone\"", File.ReadAllText(path)); // enumerados legibles
    }

    [Fact]
    public void Un_archivo_danado_no_impide_arrancar()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "ajustes.json");
        File.WriteAllText(path, "{ esto no es json");
        var settings = SettingsStore.Load(path, out var existed);
        Assert.False(existed);
        Assert.True(settings.Enabled);
    }

    [Fact]
    public void Corrige_valores_fuera_de_rango()
    {
        var settings = new AppSettings
        {
            Sensitivity = 99,
            DotSize = double.NaN,
            Columns = 0,
            DotOpacity = -1,
            PhonePort = 80,
            PhoneToken = "corto",
            Mode = (DisplayMode)42,
        };
        settings.Normalize();

        Assert.Equal(3, settings.Sensitivity);
        Assert.Equal(10, settings.DotSize);
        Assert.Equal(1, settings.Columns);
        Assert.Equal(0.2, settings.DotOpacity);
        Assert.Equal(AppSettings.DefaultPhonePort, settings.PhonePort);
        Assert.Equal(12, settings.PhoneToken.Length);
        Assert.Equal(DisplayMode.Automatic, settings.Mode);
    }
}
