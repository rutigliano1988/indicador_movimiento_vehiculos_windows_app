namespace IndicadoresMovimiento.App.Services;

/// <summary>
/// Evita abrir la aplicación dos veces. Si ya está abierta, la segunda copia le pide
/// que muestre sus ajustes y se cierra.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _signal;
    private RegisteredWaitHandle? _registration;

    public SingleInstance(string id)
    {
        _mutex = new Mutex(initiallyOwned: true, $@"Local\{id}", out var createdNew);
        IsFirstInstance = createdNew;
        _signal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{id}-mostrar");
    }

    public bool IsFirstInstance { get; }

    public void SignalFirstInstance() => _signal.Set();

    public void ListenForSignals(Action onSignal) =>
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _signal, (_, _) => onSignal(), null, Timeout.Infinite, executeOnlyOnce: false);

    public void Dispose()
    {
        _registration?.Unregister(null);
        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Se liberó desde otro hilo: Windows lo libera igualmente al cerrar el proceso.
            }
        }
        _mutex.Dispose();
        _signal.Dispose();
    }
}
