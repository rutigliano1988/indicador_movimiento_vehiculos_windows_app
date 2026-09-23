using System.Net;
using System.Security.Cryptography.X509Certificates;
using IndicadoresMovimiento.Core;

namespace IndicadoresMovimiento.Movil;

/// <summary>Usa un móvil como sensor: su navegador envía el acelerómetro por la red local.</summary>
public sealed class PhoneMotionSource : IMotionSource
{
    private readonly PhoneServer _server = new();
    private readonly string _certificatePath;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private X509Certificate2? _certificate;
    private int _preferredPort = AppSettings.DefaultPhonePort;
    private string _token = "";
    private volatile string? _error;
    private volatile string? _clientAddress;
    private double _lastBatch = double.NegativeInfinity;

    /// <param name="options">Parámetros del procesado (compartidos con la configuración de la aplicación).</param>
    /// <param name="certificatePath">Dónde guardar el certificado autofirmado.</param>
    public PhoneMotionSource(MotionProcessorOptions options, string certificatePath)
    {
        Processor = new MotionProcessor(options);
        _certificatePath = certificatePath;
        _server.BatchReceived += OnBatch;
    }

    public string DisplayName => "Móvil";
    public bool IsAvailable => true;
    public bool IsRunning => _server.IsRunning;
    public MotionProcessor Processor { get; }

    /// <summary>Puerto en el que escucha (0 si no está en marcha).</summary>
    public int Port => _server.IsRunning ? _server.Port : 0;

    /// <summary>IP del último móvil que envió datos.</summary>
    public string? ClientAddress => _clientAddress;

    /// <summary>Error del último intento de arranque, si lo hubo.</summary>
    public string? LastError => _error;

    /// <summary>Segundos desde el último paquete recibido (infinito si nunca llegó ninguno).</summary>
    public double SecondsSinceLastBatch => MonotonicClock.Seconds() - Volatile.Read(ref _lastBatch);

    /// <summary>Puerto preferido y código secreto. El código se aplica al momento; el puerto en el próximo arranque.</summary>
    public void Configure(int preferredPort, string token)
    {
        _preferredPort = preferredPort;
        _token = token;
        _server.Token = token;
    }

    /// <summary>Direcciones que se pueden abrir en el móvil, la más probable primero.</summary>
    public IReadOnlyList<(LocalAddress Address, string Url)> GetConnectionUrls() =>
        IsRunning
            ? NetworkAddresses.GetCandidates().Select(a => (a, BuildUrl(a.Address))).ToList()
            : [];

    public string BuildUrl(IPAddress address) =>
        $"https://{address}:{Port}/?k={Uri.EscapeDataString(_token)}";

    public string StatusText
    {
        get
        {
            if (_error is { } error) return $"No se pudo preparar la conexión con el móvil: {error}";
            if (!IsRunning) return "Desactivado.";
            var since = SecondsSinceLastBatch;
            if (double.IsInfinity(since)) return "Esperando a que el móvil se conecte.";
            if (since < 1.5)
            {
                var rate = Processor.GetDiagnostics().SampleRate;
                return $"Recibiendo datos del móvil ({rate:0} lecturas por segundo).";
            }
            return $"El móvil dejó de enviar datos hace {FormatAge(since)}. " +
                   "Comprueba que la página siga abierta en el móvil con la pantalla encendida.";
        }
    }

    public async Task StartAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_server.IsRunning) return;
            _error = null;
            await PrepareCertificateAsync().ConfigureAwait(false);
            await _server.StartAsync(_preferredPort, _token, _certificate!).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _error = e.Message;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    /// <summary>
    /// Si el ordenador ha cambiado de red (p. ej. se ha conectado al punto de acceso del móvil),
    /// rehace el certificado para que incluya la nueva dirección y reinicia el servidor.
    /// </summary>
    public async Task RefreshCertificateIfNeededAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!_server.IsRunning || _certificate is null) return;
            if (LocalCertificate.Covers(_certificate, RequiredAddresses())) return;

            var port = _server.Port;
            await _server.StopAsync().ConfigureAwait(false);
            await PrepareCertificateAsync().ConfigureAwait(false);
            await _server.StartAsync(port, _token, _certificate!).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _error = e.Message;
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task PrepareCertificateAsync()
    {
        var required = RequiredAddresses();
        if (_certificate is not null && LocalCertificate.Covers(_certificate, required)) return;
        var certificate = await Task.Run(() => LocalCertificate.LoadOrCreate(_certificatePath, required)).ConfigureAwait(false);
        _certificate?.Dispose();
        _certificate = certificate;
    }

    /// <summary>Direcciones reales (no virtuales) que el certificado debe incluir.</summary>
    private static List<IPAddress> RequiredAddresses() =>
        NetworkAddresses.GetCandidates().Where(a => !a.IsLikelyVirtual).Select(a => a.Address).ToList();

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            await _server.StopAsync().ConfigureAwait(false);
            Volatile.Write(ref _lastBatch, double.NegativeInfinity);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private void OnBatch(PhoneBatch batch, string? client)
    {
        _clientAddress = client;
        Volatile.Write(ref _lastBatch, MonotonicClock.Seconds());
        foreach (var sample in batch.Samples) Processor.AddSample(batch.ToMotionSample(sample));
    }

    private static string FormatAge(double seconds) => seconds switch
    {
        < 90 => $"{seconds:0} s",
        < 5400 => $"{seconds / 60:0} min",
        _ => $"{seconds / 3600:0} h",
    };

    public void Dispose()
    {
        _server.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _certificate?.Dispose();
    }
}
