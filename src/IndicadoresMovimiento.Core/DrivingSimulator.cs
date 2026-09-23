using System.Numerics;

namespace IndicadoresMovimiento.Core;

/// <summary>
/// Genera lecturas de acelerómetro como las de un portátil sobre las piernas en un coche
/// que hace un recorrido urbano en bucle. Sirve para el modo demostración y para las pruebas.
/// </summary>
public sealed class DrivingSimulator
{
    /// <summary>Un tramo del recorrido: duración y aceleraciones objetivo (m/s²).</summary>
    public readonly record struct Maneuver(double Duration, double Lateral, double Longitudinal, string Description);

    public static readonly IReadOnlyList<Maneuver> DefaultScript =
    [
        new(3.0, 0, 0, "Parado"),
        new(4.0, 0, 2.2, "Acelerando"),
        new(3.0, 0, 0.2, "En marcha"),
        new(4.0, -2.5, 0, "Curva a la izquierda"),
        new(2.5, 0, 0, "En marcha"),
        new(4.0, 2.5, 0, "Curva a la derecha"),
        new(2.0, 0, 0, "En marcha"),
        new(3.5, 0, -3.0, "Frenando"),
        new(2.0, 0, 0, "Parado"),
        new(3.0, 0, 2.5, "Acelerando"),
        new(1.5, 1.8, -0.8, "Entrando en la rotonda"),
        // En España las rotondas se recorren en sentido antihorario: curva continua a la izquierda.
        new(5.0, -2.2, 0, "En la rotonda"),
        new(1.5, 1.8, 0.8, "Saliendo de la rotonda"),
        new(3.0, 0, 1.0, "Acelerando"),
        new(4.0, 0, 0, "En marcha"),
        new(3.0, 0, -2.5, "Frenando"),
    ];

    private readonly IReadOnlyList<Maneuver> _script;
    private readonly Vector3 _deviceX;
    private readonly Vector3 _deviceY;
    private readonly Vector3 _deviceZ;
    private readonly Random _random;
    private readonly double _totalDuration;
    private double _lastTime = double.NaN;
    private double _lateral;
    private double _longitudinal;
    private double _speed;

    /// <param name="screenTiltDegrees">Inclinación hacia atrás de la pantalla respecto a la vertical.</param>
    /// <param name="vibration">Amplitud de las vibraciones de la carretera (m/s²).</param>
    public DrivingSimulator(double screenTiltDegrees = 20, double vibration = 0.35, int seed = 1,
        IReadOnlyList<Maneuver>? script = null)
    {
        _script = script ?? DefaultScript;
        _totalDuration = _script.Sum(m => m.Duration);
        Vibration = vibration;
        _random = new Random(seed);

        // Ejes del coche: X derecha, Y delante, Z arriba. La pantalla mira hacia atrás (a quien la usa)
        // y está inclinada hacia atrás "screenTiltDegrees" grados.
        var tilt = screenTiltDegrees * Math.PI / 180;
        var s = (float)Math.Sin(tilt);
        var c = (float)Math.Cos(tilt);
        _deviceX = Vector3.UnitX;
        _deviceY = new Vector3(0, s, c);
        _deviceZ = Vector3.Cross(_deviceX, _deviceY); // hacia quien mira la pantalla
    }

    public double Vibration { get; }

    /// <summary>Descripción del tramo actual (en español).</summary>
    public string CurrentManeuver { get; private set; } = "";

    /// <summary>Aceleración lateral real del coche simulado (m/s²).</summary>
    public double TrueLateral => _lateral;

    /// <summary>Aceleración longitudinal real del coche simulado (m/s²).</summary>
    public double TrueLongitudinal => _longitudinal;

    public MotionSample Next(double time)
    {
        var dt = double.IsNaN(_lastTime) ? 0 : Math.Clamp(time - _lastTime, 0, 0.1);
        _lastTime = time;

        var maneuver = ManeuverAt(time);
        CurrentManeuver = maneuver.Description;

        // Las aceleraciones reales no cambian de golpe.
        var k = 1 - Math.Exp(-dt / 0.45);
        _lateral += (maneuver.Lateral - _lateral) * k;
        _longitudinal += (maneuver.Longitudinal - _longitudinal) * k;
        _speed = Math.Max(0, _speed + _longitudinal * dt);

        var moving = _speed > 0.5 || Math.Abs(_longitudinal) > 0.3;
        var amplitude = moving ? Vibration : Vibration * 0.1;
        var vibX = amplitude * 0.4 * (Math.Sin(2 * Math.PI * 11 * time) + Noise());
        var vibY = amplitude * 0.4 * (Math.Sin(2 * Math.PI * 7.3 * time + 1) + Noise());
        var vibZ = amplitude * (Math.Sin(2 * Math.PI * 13.7 * time + 2) + Noise());

        var world = new Vector3(
            (float)(_lateral + vibX),
            (float)(_longitudinal + vibY),
            (float)(MotionSample.StandardGravity + vibZ));

        var force = new Vector3(Vector3.Dot(world, _deviceX), Vector3.Dot(world, _deviceY), Vector3.Dot(world, _deviceZ));
        return new MotionSample(time, force);
    }

    private Maneuver ManeuverAt(double time)
    {
        var t = time % _totalDuration;
        if (t < 0) t += _totalDuration;
        foreach (var m in _script)
        {
            if (t < m.Duration) return m;
            t -= m.Duration;
        }
        return _script[^1];
    }

    private double Noise() => _random.NextDouble() * 2 - 1;
}
