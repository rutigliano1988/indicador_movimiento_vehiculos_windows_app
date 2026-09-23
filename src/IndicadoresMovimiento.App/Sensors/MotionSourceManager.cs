using IndicadoresMovimiento.Core;
using IndicadoresMovimiento.Movil;

namespace IndicadoresMovimiento.App.Sensors;

/// <summary>Arranca y detiene las fuentes de movimiento según los ajustes y elige cuál usar.</summary>
internal sealed class MotionSourceManager : IDisposable
{
    private readonly MotionProcessorOptions _options = new();
    private readonly SemaphoreSlim _applyLock = new(1, 1);
    private volatile MotionSourceKind _kind = MotionSourceKind.Automatic;
    private volatile bool _phoneRequested;

    public MotionSourceManager(string certificatePath)
    {
        BuiltIn = new WindowsSensorSource(_options);
        Phone = new PhoneMotionSource(_options, certificatePath);
        Demo = new SimulatedMotionSource(_options);
    }

    public WindowsSensorSource BuiltIn { get; }
    public PhoneMotionSource Phone { get; }
    public SimulatedMotionSource Demo { get; }

    public MotionSourceKind Kind => _kind;

    /// <summary>La fuente cuyos datos se están mostrando ahora mismo.</summary>
    public IMotionSource Active => _kind switch
    {
        MotionSourceKind.BuiltInSensor => BuiltIn,
        MotionSourceKind.Phone => Phone,
        MotionSourceKind.Demo => Demo,
        // Automática: si el móvil está enviando datos, se ha conectado a propósito y manda él.
        _ => Phone.Processor.GetState().HasData || !BuiltIn.IsAvailable ? Phone : BuiltIn,
    };

    public CueState GetState() => Active.Processor.GetState();

    public async Task ApplyAsync(AppSettings settings)
    {
        await _applyLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _options.InvertLateral = settings.InvertLateral;
            _options.InvertLongitudinal = settings.InvertLongitudinal;
            Phone.Configure(settings.PhonePort, settings.PhoneToken);
            _kind = settings.Source;

            var useBuiltIn = _kind is MotionSourceKind.Automatic or MotionSourceKind.BuiltInSensor;
            var usePhone = _kind == MotionSourceKind.Phone
                || (_kind == MotionSourceKind.Automatic && (_phoneRequested || !BuiltIn.IsAvailable));
            var useDemo = _kind == MotionSourceKind.Demo;

            await SetRunningAsync(BuiltIn, useBuiltIn && BuiltIn.IsAvailable).ConfigureAwait(false);
            await SetRunningAsync(Phone, usePhone).ConfigureAwait(false);
            await SetRunningAsync(Demo, useDemo).ConfigureAwait(false);
        }
        finally
        {
            _applyLock.Release();
        }
    }

    /// <summary>El usuario quiere conectar el móvil: el servidor se queda en marcha aunque haya sensor propio.</summary>
    public async Task EnsurePhoneAsync()
    {
        _phoneRequested = true;
        await _applyLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await SetRunningAsync(Phone, true).ConfigureAwait(false);
        }
        finally
        {
            _applyLock.Release();
        }
    }

    private static Task SetRunningAsync(IMotionSource source, bool running)
    {
        if (running && !source.IsRunning) return source.StartAsync();
        if (!running && source.IsRunning) return source.StopAsync();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        BuiltIn.Dispose();
        Phone.Dispose();
        Demo.Dispose();
        _applyLock.Dispose();
    }
}
