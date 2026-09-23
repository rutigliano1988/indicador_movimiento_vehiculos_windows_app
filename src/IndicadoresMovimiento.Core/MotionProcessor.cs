using System.Diagnostics;
using System.Numerics;

namespace IndicadoresMovimiento.Core;

/// <summary>
/// Convierte las lecturas del acelerómetro (y giroscopio, si lo hay) en la aceleración del vehículo:
/// lateral (curvas) y longitudinal (acelerar/frenar), filtrada y lista para animar los puntos.
/// </summary>
/// <remarks>
/// <para>
/// Se asume que quien usa el ordenador mira hacia delante en el sentido de la marcha y que la pantalla
/// "mira" hacia esa persona (como un portátil sobre las piernas o un móvil en un soporte). Así:
/// </para>
/// <list type="bullet">
/// <item>La vertical se obtiene de la gravedad (media lenta de la fuerza medida o fusión de sensores).</item>
/// <item>La "derecha" del coche es la horizontal contenida en el plano de la pantalla. Esto funciona
/// aunque la pantalla esté girada (vertical/horizontal). Si la pantalla está tumbada se usa el eje
/// derecho de la pantalla (<see cref="MotionSample.FlatRightAxis"/>).</item>
/// <item>"Delante" es perpendicular a ambos.</item>
/// </list>
/// <para>Es seguro llamar a <see cref="AddSample"/> y <see cref="GetState"/> desde hilos distintos.</para>
/// </remarks>
public sealed class MotionProcessor
{
    // Peso del eje derecho "de pantalla tumbada" frente al de "pantalla de pie".
    private const float FlatBlend = 0.35f;
    private const double AxisTimeConstant = 0.5;
    private const double FastForceTimeConstant = 0.1;

    private readonly object _gate = new();
    private readonly Func<double> _clock;

    private bool _initialized;
    private bool _reorienting;
    private double _recoveryUntil = double.NegativeInfinity;
    private double _lastTimestamp;
    private double _lastReceived = double.NegativeInfinity;
    private Vector3 _gravity;
    private Vector3 _fastForce;
    private Vector3 _right = Vector3.UnitX;
    private double _lat1, _lat2, _lon1, _lon2;
    private double _envelope;
    private double _meanDt;
    private long _sampleCount;
    private bool _hasGyro;
    private bool _hasFused;

    /// <param name="options">Parámetros; si es nulo se usan los de por defecto.</param>
    /// <param name="clock">Reloj local en segundos (para detectar que la fuente deja de enviar datos).</param>
    public MotionProcessor(MotionProcessorOptions? options = null, Func<double>? clock = null)
    {
        Options = options ?? new MotionProcessorOptions();
        _clock = clock ?? MonotonicClock.Seconds;
    }

    public MotionProcessorOptions Options { get; }

    /// <summary>Olvida todo el estado (p. ej. al cambiar de fuente o reconectar el móvil).</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _initialized = false;
            _reorienting = false;
            _recoveryUntil = double.NegativeInfinity;
            _lastReceived = double.NegativeInfinity;
            _lat1 = _lat2 = _lon1 = _lon2 = 0;
            _envelope = 0;
            _meanDt = 0;
            _sampleCount = 0;
            _right = Vector3.UnitX;
        }
    }

    public void AddSample(in MotionSample sample)
    {
        var force = sample.SpecificForce;
        if (!Filters.IsFinite(force) || !double.IsFinite(sample.Timestamp)) return;
        Vector3? linear = sample.LinearAcceleration is { } l && Filters.IsFinite(l) ? l : null;
        Vector3? gyro = sample.AngularVelocity is { } w && Filters.IsFinite(w) ? w : null;
        var o = Options;

        lock (_gate)
        {
            _lastReceived = _clock();
            _sampleCount++;
            _hasGyro = gyro.HasValue;
            _hasFused = linear.HasValue;

            var t = sample.Timestamp;
            if (!_initialized || t < _lastTimestamp - 1.0 || t - _lastTimestamp > 2.0)
            {
                // Primera muestra o salto grande de reloj: empezar de cero.
                Initialize(force, linear, sample.FlatRightAxis, t);
                return;
            }

            var rawDt = t - _lastTimestamp;
            if (rawDt > 0)
            {
                _meanDt = _meanDt <= 0 ? rawDt : _meanDt + (rawDt - _meanDt) * 0.05;
                _lastTimestamp = t;
            }
            var dt = Math.Clamp(rawDt, 1e-3, 0.1);

            // 1) Con giroscopio, la vertical estimada gira junto con el dispositivo.
            //    Un vector fijo en el mundo, visto desde el dispositivo, cambia a ritmo -ω×v.
            if (gyro is { } omega)
            {
                _gravity -= Vector3.Cross(omega, _gravity) * (float)dt;
                _fastForce -= Vector3.Cross(omega, _fastForce) * (float)dt;
            }

            // 2) Estimación de la vertical.
            _fastForce += (force - _fastForce) * Filters.Alpha(dt, FastForceTimeConstant);
            if (linear is { } lin)
            {
                _reorienting = false;
                _gravity += (force - lin - _gravity) * Filters.Alpha(dt, o.FusedGravityTimeConstant);
            }
            else
            {
                var deviation = Filters.AngleDegrees(_fastForce, _gravity);
                var forceLength = force.Length();
                var instantDeviation = forceLength is > 0.7f * MotionSample.StandardGravity and < 1.3f * MotionSample.StandardGravity
                    ? Filters.AngleDegrees(force, _gravity)
                    : 0; // un bache cambia mucho el módulo: no es un giro del dispositivo
                if (deviation > o.ReorientationAngleDegrees || instantDeviation > o.ReorientationImmediateAngleDegrees)
                {
                    if (!_reorienting && instantDeviation > o.ReorientationImmediateAngleDegrees) _fastForce = force;
                    _reorienting = true;
                }
                else if (_reorienting && deviation < o.ReorientationSettledDegrees)
                {
                    _reorienting = false;
                    _recoveryUntil = t + o.RecoverySeconds;
                }

                var tau = _reorienting ? o.ReorientationTimeConstant
                    : t < _recoveryUntil ? o.RecoveryTimeConstant
                    : gyro.HasValue ? o.GravityTimeConstantWithGyro
                    : o.GravityTimeConstant;
                var target = _reorienting ? _fastForce : force;
                _gravity += (target - _gravity) * Filters.Alpha(dt, tau);
            }

            var gravityLength = _gravity.Length();
            if (gravityLength < 1f) return; // sin una vertical fiable no se puede hacer nada
            var up = _gravity / gravityLength;

            // 3) Ejes del vehículo vistos desde el dispositivo.
            UpdateRightAxis(up, sample.FlatRightAxis, dt, snap: false);
            var forward = Vector3.Cross(up, _right);

            // 4) Aceleración del vehículo en el plano horizontal.
            double lateral = 0, longitudinal = 0;
            if (!_reorienting)
            {
                var acceleration = linear ?? force - _gravity;
                lateral = Vector3.Dot(acceleration, _right);
                longitudinal = Vector3.Dot(acceleration, forward);
                if (o.InvertLateral) lateral = -lateral;
                if (o.InvertLongitudinal) longitudinal = -longitudinal;
            }

            // 5) Suavizado en dos etapas (~2 Hz) para quitar las vibraciones de la carretera.
            var k = Filters.Alpha(dt, o.SmoothingTimeConstant);
            _lat1 += (lateral - _lat1) * k;
            _lat2 += (_lat1 - _lat2) * k;
            _lon1 += (longitudinal - _lon1) * k;
            _lon2 += (_lon1 - _lon2) * k;

            // 6) Envolvente: sube rápido con el movimiento y baja despacio al parar.
            var magnitude = Math.Sqrt(_lat2 * _lat2 + _lon2 * _lon2);
            var envelopeTau = magnitude > _envelope ? o.ActivityAttack : o.ActivityRelease;
            _envelope += (magnitude - _envelope) * Filters.Alpha(dt, envelopeTau);
        }
    }

    public CueState GetState()
    {
        lock (_gate)
        {
            if (!_initialized || _clock() - _lastReceived > Options.StaleAfterSeconds) return default;
            var activity = Filters.SmoothStep(Options.ActivityLow, Options.ActivityHigh, _envelope);
            return new CueState(_lat2, _lon2, activity, true);
        }
    }

    public MotionDiagnostics GetDiagnostics()
    {
        lock (_gate)
        {
            var since = _initialized ? _clock() - _lastReceived : double.PositiveInfinity;
            var rate = _meanDt > 0 ? 1.0 / _meanDt : 0;
            var up = _gravity.LengthSquared() > 1e-6f ? Vector3.Normalize(_gravity) : Vector3.Zero;
            return new MotionDiagnostics(_sampleCount, rate, since, _hasGyro, _hasFused, up, _right);
        }
    }

    private void Initialize(Vector3 force, Vector3? linear, Vector3? flatRight, double t)
    {
        _gravity = linear is { } lin ? force - lin : force;
        _fastForce = force;
        _lastTimestamp = t;
        _lat1 = _lat2 = _lon1 = _lon2 = 0;
        _envelope = 0;
        _reorienting = false;
        _recoveryUntil = double.NegativeInfinity;
        _initialized = true;

        var gravityLength = _gravity.Length();
        if (gravityLength >= 1f) UpdateRightAxis(_gravity / gravityLength, flatRight, 0, snap: true);
    }

    private void UpdateRightAxis(Vector3 up, Vector3? flatRightAxis, double dt, bool snap)
    {
        var flatRight = flatRightAxis is { } fr && Filters.IsFinite(fr) && fr.LengthSquared() > 0.01f
            ? Vector3.Normalize(fr)
            : Vector3.UnitX;

        // Pantalla de pie: la horizontal dentro del plano de la pantalla es up × Z (vale para cualquier giro).
        // Pantalla tumbada: up × Z tiende a cero y manda el eje derecho de la pantalla proyectado.
        // Cuando ambos existen coinciden, así que mezclarlos no crea conflictos.
        var flatHorizontal = flatRight - Vector3.Dot(flatRight, up) * up;
        var candidate = Vector3.Cross(up, Vector3.UnitZ) + FlatBlend * flatHorizontal;
        var length = candidate.Length();
        if (length > 0.15f)
        {
            candidate /= length;
            if (snap || Vector3.Dot(candidate, _right) < 0.2f) _right = candidate;
            else _right += (candidate - _right) * Filters.Alpha(dt, AxisTimeConstant);
        }

        // Mantener el eje horizontal y unitario respecto a la vertical actual.
        var horizontal = _right - Vector3.Dot(_right, up) * up;
        var horizontalLength = horizontal.Length();
        if (horizontalLength > 1e-3f)
        {
            _right = horizontal / horizontalLength;
        }
        else
        {
            var fallback = Vector3.Cross(up, Vector3.UnitY);
            if (fallback.LengthSquared() < 1e-6f) fallback = Vector3.Cross(up, Vector3.UnitX);
            _right = Vector3.Normalize(fallback);
        }
    }
}

/// <summary>Datos de diagnóstico para mostrar en la ventana de ajustes.</summary>
public readonly record struct MotionDiagnostics(
    long SampleCount,
    double SampleRate,
    double SecondsSinceLastSample,
    bool UsesGyroscope,
    bool UsesSensorFusion,
    Vector3 Up,
    Vector3 Right);

/// <summary>Reloj monótono en segundos.</summary>
public static class MonotonicClock
{
    public static double Seconds() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
}
