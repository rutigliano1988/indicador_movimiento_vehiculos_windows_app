namespace IndicadoresMovimiento.Core;

/// <summary>Fuente de demostración: un coche simulado que acelera, frena y gira en bucle.</summary>
public sealed class SimulatedMotionSource : IMotionSource
{
    private readonly object _gate = new();
    private readonly DrivingSimulator _simulator = new();
    private CancellationTokenSource? _cts;
    private Task? _loop;

    public SimulatedMotionSource(MotionProcessorOptions options)
    {
        Processor = new MotionProcessor(options);
    }

    public string DisplayName => "Demostración";
    public bool IsAvailable => true;
    public bool IsRunning => _loop is { IsCompleted: false };
    public MotionProcessor Processor { get; }

    public string StatusText => IsRunning
        ? $"Simulando un trayecto en coche: {_simulator.CurrentManeuver.ToLowerInvariant()}."
        : "Detenida.";

    public Task StartAsync()
    {
        lock (_gate)
        {
            if (IsRunning) return Task.CompletedTask;
            Processor.Reset();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _loop = Task.Run(() => RunAsync(token), token);
        }
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? loop;
        CancellationTokenSource? cts;
        lock (_gate)
        {
            cts = _cts;
            _cts = null;
            cts?.Cancel();
            loop = _loop;
            _loop = null;
        }

        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        cts?.Dispose();
    }

    private async Task RunAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(16));
        var start = MonotonicClock.Seconds();
        while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
        {
            Processor.AddSample(_simulator.Next(MonotonicClock.Seconds() - start));
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
