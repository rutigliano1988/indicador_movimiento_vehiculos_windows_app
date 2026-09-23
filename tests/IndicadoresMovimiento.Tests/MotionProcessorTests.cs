using System.Numerics;
using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.Tests;

public class MotionProcessorTests
{
    private const double Rate = 60;

    public static TheoryData<string> Frames => new()
    {
        "portátil",
        "portátil muy inclinado",
        "móvil tumbado",
        "móvil de pie",
        "móvil de pie girado a la izquierda",
        "móvil de pie girado a la derecha",
    };

    private static DeviceFrame FrameByName(string name) => name switch
    {
        "portátil" => DeviceFrame.Laptop(20),
        "portátil muy inclinado" => DeviceFrame.Laptop(55),
        "móvil tumbado" => DeviceFrame.PhoneFlat,
        "móvil de pie" => DeviceFrame.PhoneUpright,
        "móvil de pie girado a la izquierda" => DeviceFrame.PhoneUprightRotatedLeft,
        "móvil de pie girado a la derecha" => DeviceFrame.PhoneUprightRotatedRight,
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>Alimenta el procesador con una aceleración constante durante un tiempo.</summary>
    private static double Feed(MotionProcessor processor, FakeClock clock, DeviceFrame frame, double start,
        double seconds, double lateral, double longitudinal, bool fused = false, Vector3? flatRight = null)
    {
        var t = start;
        var n = (int)(seconds * Rate);
        for (var i = 0; i < n; i++)
        {
            t += 1 / Rate;
            clock.Now = t;
            processor.AddSample(frame.Sample(t, lateral, longitudinal, fused, flatRight));
        }
        return t;
    }

    [Theory]
    [MemberData(nameof(Frames))]
    public void Quieto_no_genera_movimiento(string frameName)
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        Feed(processor, clock, FrameByName(frameName), 0, 3, 0, 0);

        var state = processor.GetState();
        Assert.True(state.HasData);
        Assert.InRange(state.Lateral, -0.01, 0.01);
        Assert.InRange(state.Longitudinal, -0.01, 0.01);
        Assert.Equal(0, state.Activity, 3);
    }

    [Theory]
    [MemberData(nameof(Frames))]
    public void Curva_a_la_derecha_da_lateral_positivo(string frameName)
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        var frame = FrameByName(frameName);
        var t = Feed(processor, clock, frame, 0, 3, 0, 0);
        Feed(processor, clock, frame, t, 1, 2.0, 0);

        var state = processor.GetState();
        Assert.InRange(state.Lateral, 1.4, 2.1);
        Assert.InRange(state.Longitudinal, -0.25, 0.25);
        Assert.True(state.Activity > 0.9);
    }

    [Theory]
    [MemberData(nameof(Frames))]
    public void Acelerar_da_longitudinal_positivo_y_frenar_negativo(string frameName)
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        var frame = FrameByName(frameName);
        var t = Feed(processor, clock, frame, 0, 3, 0, 0);
        t = Feed(processor, clock, frame, t, 1, 0, 2.0);

        var accelerating = processor.GetState();
        Assert.InRange(accelerating.Longitudinal, 1.4, 2.1);
        Assert.InRange(accelerating.Lateral, -0.25, 0.25);

        t = Feed(processor, clock, frame, t, 4, 0, 0);
        Feed(processor, clock, frame, t, 1, 0, -2.5);
        var braking = processor.GetState();
        Assert.InRange(braking.Longitudinal, -2.6, -1.5);
        Assert.InRange(braking.Lateral, -0.3, 0.3);
    }

    [Fact]
    public void Con_fusion_de_sensores_la_aceleracion_se_mantiene_en_curvas_largas()
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        var frame = DeviceFrame.PhoneUpright;
        var t = Feed(processor, clock, frame, 0, 2, 0, 0, fused: true);
        Feed(processor, clock, frame, t, 8, -2.0, 0.5, fused: true);

        var state = processor.GetState();
        Assert.InRange(state.Lateral, -2.05, -1.95);
        Assert.InRange(state.Longitudinal, 0.45, 0.55);
    }

    [Fact]
    public void Movil_tumbado_girado_usa_el_eje_derecho_de_la_pantalla()
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        var frame = DeviceFrame.PhoneFlatRotatedLeft;
        var flatRight = PhoneBatchParser.FlatRightAxisForScreenAngle(90);
        var t = Feed(processor, clock, frame, 0, 3, 0, 0, flatRight: flatRight);
        Feed(processor, clock, frame, t, 1, 2.0, 0, flatRight: flatRight);

        var state = processor.GetState();
        Assert.InRange(state.Lateral, 1.4, 2.1);
        Assert.InRange(state.Longitudinal, -0.25, 0.25);
    }

    [Fact]
    public void Las_opciones_invierten_los_ejes()
    {
        var clock = new FakeClock();
        var options = new MotionProcessorOptions { InvertLateral = true, InvertLongitudinal = true };
        var processor = new MotionProcessor(options, clock.Read);
        var frame = DeviceFrame.Laptop();
        var t = Feed(processor, clock, frame, 0, 3, 0, 0);
        Feed(processor, clock, frame, t, 1, 2.0, 2.0);

        var state = processor.GetState();
        Assert.True(state.Lateral < -1.4);
        Assert.True(state.Longitudinal < -1.4);
    }

    [Fact]
    public void Tras_girar_el_portatil_la_vertical_se_recupera_rapido()
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        var t = Feed(processor, clock, DeviceFrame.Laptop(20), 0, 3, 0, 0);

        // Se cierra un poco la tapa: la pantalla pasa de 20° a 80° de inclinación de golpe.
        var tilted = DeviceFrame.Laptop(80);
        t = Feed(processor, clock, tilted, t, 0.2, 0, 0);
        var during = processor.GetState();
        Feed(processor, clock, tilted, t, 2.5, 0, 0);
        var after = processor.GetState();

        // Mientras se reajusta no se inventa un movimiento grande...
        Assert.InRange(Math.Abs(during.Longitudinal), 0, 1.5);
        // ...y a los pocos segundos vuelve a estar en reposo.
        Assert.InRange(after.Lateral, -0.2, 0.2);
        Assert.InRange(after.Longitudinal, -0.2, 0.2);
    }

    [Fact]
    public void Tras_mover_la_tapa_poco_a_poco_vuelve_al_reposo()
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        var t = Feed(processor, clock, DeviceFrame.Laptop(20), 0, 3, 0, 0);

        // La tapa se mueve de 20° a 70° en medio segundo.
        for (var i = 1; i <= 30; i++)
        {
            t = Feed(processor, clock, DeviceFrame.Laptop(20 + 50.0 * i / 30), t, 1 / Rate, 0, 0);
        }
        Feed(processor, clock, DeviceFrame.Laptop(70), t, 3, 0, 0);

        var state = processor.GetState();
        Assert.InRange(state.Lateral, -0.2, 0.2);
        Assert.InRange(state.Longitudinal, -0.3, 0.3);
    }

    [Fact]
    public void Con_giroscopio_girar_el_dispositivo_no_genera_movimiento_falso()
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);

        // Inclinación que va de 10° a 40° a 15°/s, informando del giro con el giroscopio.
        const double tiltRate = 15; // grados por segundo
        var t = 0.0;
        var tilt = 10.0;
        var maxAbs = 0.0;
        for (var i = 0; i < Rate * 4; i++)
        {
            t += 1 / Rate;
            clock.Now = t;
            var rotating = t > 1 && t <= 3;
            if (rotating) tilt += tiltRate / Rate;
            var frame = DeviceFrame.Laptop(tilt);
            // Echar la pantalla hacia atrás es girar alrededor de +X del dispositivo en sentido negativo
            // (regla de la mano derecha: el sentido positivo acerca la parte de arriba a quien la mira).
            var omega = rotating ? new Vector3((float)(-tiltRate * Math.PI / 180), 0, 0) : Vector3.Zero;
            var sample = frame.Sample(t, 0, 0) with { AngularVelocity = omega };
            processor.AddSample(sample);
            if (t > 1) maxAbs = Math.Max(maxAbs, Math.Abs(processor.GetState().Longitudinal));
        }

        Assert.InRange(maxAbs, 0, 0.3);
    }

    [Fact]
    public void Sin_muestras_recientes_no_hay_datos()
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        var t = Feed(processor, clock, DeviceFrame.Laptop(), 0, 1, 0, 0);
        Assert.True(processor.GetState().HasData);

        clock.Now = t + 2;
        Assert.False(processor.GetState().HasData);
    }

    [Fact]
    public void Ignora_lecturas_no_validas()
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        var t = Feed(processor, clock, DeviceFrame.Laptop(), 0, 1, 0, 0);
        processor.AddSample(new MotionSample(t + 0.01, new Vector3(float.NaN, 0, 9.8f)));
        processor.AddSample(new MotionSample(double.NaN, new Vector3(0, 0, 9.8f)));

        var state = processor.GetState();
        Assert.True(double.IsFinite(state.Lateral));
        Assert.True(double.IsFinite(state.Longitudinal));
    }

    [Fact]
    public void Estima_la_frecuencia_de_muestreo()
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        Feed(processor, clock, DeviceFrame.Laptop(), 0, 2, 0, 0);
        Assert.InRange(processor.GetDiagnostics().SampleRate, 58, 62);
    }

    [Fact]
    public void El_simulador_produce_las_senales_esperadas()
    {
        var clock = new FakeClock();
        var processor = new MotionProcessor(clock: clock.Read);
        var simulator = new DrivingSimulator(vibration: 0.35);
        var seen = new Dictionary<string, (double Lateral, double Longitudinal)>();

        var start = 0.0;
        foreach (var maneuver in DrivingSimulator.DefaultScript)
        {
            // Medir a mitad de cada tramo, cuando la aceleración ya se ha estabilizado.
            var middle = start + maneuver.Duration * 0.6;
            for (var t = start; t < start + maneuver.Duration; t += 1 / Rate)
            {
                clock.Now = t;
                processor.AddSample(simulator.Next(t));
                if (Math.Abs(t - middle) < 0.5 / Rate)
                {
                    var s = processor.GetState();
                    seen[$"{maneuver.Description}@{start}"] = (s.Lateral, s.Longitudinal);
                }
            }
            start += maneuver.Duration;
        }

        foreach (var (key, (lateral, longitudinal)) in seen)
        {
            if (key.StartsWith("Curva a la izquierda")) Assert.True(lateral < -1.5, key);
            if (key.StartsWith("Curva a la derecha")) Assert.True(lateral > 1.5, key);
            if (key.StartsWith("En la rotonda")) Assert.True(lateral < -1.2, key);
            if (key.StartsWith("Frenando")) Assert.True(longitudinal < -1.4, key);
            if (key.StartsWith("Acelerando")) Assert.True(longitudinal > 0.4, key);
        }
    }
}
