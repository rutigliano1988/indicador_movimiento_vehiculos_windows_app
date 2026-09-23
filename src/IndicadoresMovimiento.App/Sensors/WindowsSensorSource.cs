using System.Numerics;
using IndicadoresMovimiento.Core;
using Windows.Devices.Sensors;

namespace IndicadoresMovimiento.App.Sensors;

/// <summary>
/// Acelerómetro (y giroscopio, si lo hay) del propio ordenador. Suelen tenerlo las tabletas,
/// los convertibles 2 en 1 y algunos portátiles; los portátiles clásicos muchas veces no.
/// </summary>
internal sealed class WindowsSensorSource : IMotionSource
{
    private readonly object _gate = new();
    private readonly object _gyroGate = new();
    private Accelerometer? _accelerometer;
    private Gyrometer? _gyrometer;
    private bool _probed;
    private bool _running;
    private long _baseTicks;
    private Vector3 _lastGyro;
    private double _lastGyroTime = double.NegativeInfinity;
    private string? _error;

    public WindowsSensorSource(MotionProcessorOptions options)
    {
        Processor = new MotionProcessor(options);
    }

    public string DisplayName => "Sensor del ordenador";

    public bool IsAvailable
    {
        get
        {
            Probe();
            return _accelerometer is not null;
        }
    }

    public bool HasGyroscope
    {
        get
        {
            Probe();
            return _gyrometer is not null;
        }
    }

    public bool IsRunning => _running;

    public MotionProcessor Processor { get; }

    public string StatusText
    {
        get
        {
            if (!IsAvailable)
            {
                return _error is null
                    ? "Este ordenador no tiene acelerómetro (o Windows no lo reconoce)."
                    : $"No se pudo acceder al acelerómetro: {_error}";
            }
            if (!_running) return "Disponible, pero sin usar.";

            var diagnostics = Processor.GetDiagnostics();
            if (diagnostics.SecondsSinceLastSample > 2)
                return "El sensor del ordenador no está enviando datos. Puede estar desactivado en Windows.";

            var sensors = diagnostics.UsesGyroscope ? "Acelerómetro y giroscopio" : "Acelerómetro";
            return $"{sensors} del ordenador: {diagnostics.SampleRate:0} lecturas por segundo.";
        }
    }

    public Task StartAsync()
    {
        Probe();
        lock (_gate)
        {
            if (_running || _accelerometer is null) return Task.CompletedTask;
            Processor.Reset();
            _baseTicks = DateTimeOffset.UtcNow.UtcTicks;
            try
            {
                _accelerometer.ReportInterval = Math.Max(_accelerometer.MinimumReportInterval, 16u);
                _accelerometer.ReadingChanged += OnAccelerometerReading;
                if (_gyrometer is not null)
                {
                    _gyrometer.ReportInterval = Math.Max(_gyrometer.MinimumReportInterval, 16u);
                    _gyrometer.ReadingChanged += OnGyrometerReading;
                }
                _running = true;
            }
            catch (Exception e)
            {
                _error = e.Message;
            }
        }
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            if (!_running) return Task.CompletedTask;
            _running = false;
            try
            {
                _accelerometer!.ReadingChanged -= OnAccelerometerReading;
                _accelerometer.ReportInterval = 0; // valor por defecto del sistema
                if (_gyrometer is not null)
                {
                    _gyrometer.ReadingChanged -= OnGyrometerReading;
                    _gyrometer.ReportInterval = 0;
                }
            }
            catch (Exception e)
            {
                _error = e.Message;
            }
        }
        return Task.CompletedTask;
    }

    private void Probe()
    {
        lock (_gate)
        {
            if (_probed) return;
            _probed = true;
            try
            {
                _accelerometer = Accelerometer.GetDefault();
            }
            catch (Exception e)
            {
                _error = e.Message;
                _accelerometer = null;
            }

            try
            {
                _gyrometer = Gyrometer.GetDefault();
            }
            catch (Exception)
            {
                _gyrometer = null;
            }
        }
    }

    private double ToSeconds(DateTimeOffset timestamp) =>
        (timestamp.UtcTicks - _baseTicks) / (double)TimeSpan.TicksPerSecond;

    private void OnGyrometerReading(Gyrometer sender, GyrometerReadingChangedEventArgs args)
    {
        var reading = args.Reading;
        const float degreesToRadians = MathF.PI / 180;
        var omega = new Vector3(
            (float)reading.AngularVelocityX * degreesToRadians,
            (float)reading.AngularVelocityY * degreesToRadians,
            (float)reading.AngularVelocityZ * degreesToRadians);
        lock (_gyroGate)
        {
            _lastGyro = omega;
            _lastGyroTime = ToSeconds(reading.Timestamp);
        }
    }

    private void OnAccelerometerReading(Accelerometer sender, AccelerometerReadingChangedEventArgs args)
    {
        var reading = args.Reading;
        var t = ToSeconds(reading.Timestamp);

        // Windows da la aceleración en g y con el signo al revés que Android/W3C:
        // con el equipo tumbado boca arriba, Z vale -1. Se convierte a m/s² "hacia arriba".
        var force = new Vector3(
            (float)-reading.AccelerationX,
            (float)-reading.AccelerationY,
            (float)-reading.AccelerationZ) * MotionSample.StandardGravity;

        Vector3? gyro = null;
        lock (_gyroGate)
        {
            if (Math.Abs(t - _lastGyroTime) < 0.2) gyro = _lastGyro;
        }

        Processor.AddSample(new MotionSample(t, force, null, gyro));
    }

    public void Dispose() => StopAsync().GetAwaiter().GetResult();
}
