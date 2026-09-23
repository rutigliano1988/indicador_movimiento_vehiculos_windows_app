using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.Tests;

public class CueAnimatorTests
{
    private static CueAnimator Run(CueState state, CueRenderOptions options, double seconds = 2, bool enabled = true)
    {
        var animator = new CueAnimator();
        for (var i = 0; i < seconds * 60; i++) animator.Step(state, 1 / 60.0, options, enabled);
        return animator;
    }

    [Fact]
    public void Curva_a_la_derecha_mueve_los_puntos_a_la_izquierda()
    {
        var animator = Run(new CueState(2, 0, 1, true), new CueRenderOptions());
        Assert.True(animator.OffsetX < -20);
        Assert.InRange(animator.OffsetY, -0.5, 0.5);
    }

    [Fact]
    public void Acelerar_mueve_los_puntos_hacia_abajo_y_frenar_hacia_arriba()
    {
        var options = new CueRenderOptions();
        Assert.True(Run(new CueState(0, 2, 1, true), options).OffsetY > 20);
        Assert.True(Run(new CueState(0, -2, 1, true), options).OffsetY < -20);
    }

    [Fact]
    public void El_desplazamiento_nunca_supera_el_maximo()
    {
        var options = new CueRenderOptions { MaxOffset = 40 };
        var animator = Run(new CueState(-50, 50, 1, true), options);
        Assert.InRange(animator.OffsetX, 0, 40);
        Assert.InRange(animator.OffsetY, 0, 40);
    }

    [Fact]
    public void Modo_automatico_sin_movimiento_oculta_los_puntos()
    {
        var animator = Run(new CueState(0, 0, 0, true), new CueRenderOptions { Mode = DisplayMode.Automatic }, 10);
        Assert.Equal(0, animator.Opacity);
    }

    [Fact]
    public void Modo_siempre_muestra_los_puntos_aunque_no_haya_datos()
    {
        var animator = Run(default, new CueRenderOptions { Mode = DisplayMode.Always });
        Assert.True(animator.Opacity > 0.95);
    }

    [Fact]
    public void Desactivado_oculta_los_puntos()
    {
        var animator = Run(new CueState(1, 1, 1, true), new CueRenderOptions { Mode = DisplayMode.Always }, 10, enabled: false);
        Assert.Equal(0, animator.Opacity);
        Assert.InRange(animator.OffsetX, -0.5, 0.5);
    }

    [Fact]
    public void Un_paso_de_tiempo_enorme_no_desestabiliza_el_muelle()
    {
        var animator = new CueAnimator();
        animator.Step(new CueState(2, 2, 1, true), 5, new CueRenderOptions());
        Assert.True(double.IsFinite(animator.OffsetX));
        Assert.InRange(Math.Abs(animator.OffsetX), 0, 56);
    }
}

public class SimulatedMotionSourceTests
{
    [Fact]
    public async Task La_demostracion_genera_datos_y_se_puede_detener()
    {
        using var source = new SimulatedMotionSource(new MotionProcessorOptions());
        await source.StartAsync();
        Assert.True(source.IsRunning);
        await Task.Delay(400);
        Assert.True(source.Processor.GetState().HasData);
        Assert.Contains("Simulando", source.StatusText);

        await source.StopAsync();
        Assert.False(source.IsRunning);
        await source.StopAsync(); // detener dos veces no falla
    }
}
