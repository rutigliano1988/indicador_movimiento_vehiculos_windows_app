using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using IndicadoresMovimiento.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IndicadoresMovimiento.Movil;

/// <summary>
/// Pequeño servidor HTTPS en la red local: sirve la página para el móvil y recibe sus lecturas.
/// </summary>
/// <remarks>
/// Se usan peticiones POST normales en lugar de WebSocket porque Safari en iPhone no permite
/// WebSocket con certificados autofirmados, aunque el usuario haya aceptado el aviso.
/// </remarks>
public sealed class PhoneServer : IAsyncDisposable
{
    public const int PortAttempts = 10;
    private const int MaxBodyBytes = 256 * 1024;

    private static readonly Lazy<byte[]> Page = new(LoadPage);

    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private WebApplication? _app;
    private volatile byte[] _token = [];

    /// <summary>Se recibe un paquete válido (en un hilo del servidor). El texto es la IP del móvil.</summary>
    public event Action<PhoneBatch, string?>? BatchReceived;

    public int Port { get; private set; }

    public bool IsRunning => _app is not null;

    /// <summary>Código que el móvil debe enviar. Se puede cambiar con el servidor en marcha.</summary>
    public string Token
    {
        get => Encoding.UTF8.GetString(_token);
        set => _token = Encoding.UTF8.GetBytes(value ?? "");
    }

    /// <summary>Arranca en <paramref name="preferredPort"/> o, si está ocupado, en alguno de los siguientes.</summary>
    /// <remarks>Con el puerto 0 el sistema elige uno libre (útil en pruebas).</remarks>
    public async Task StartAsync(int preferredPort, string token, X509Certificate2 certificate,
        CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_app is not null) return;
            Token = token;

            Exception? lastError = null;
            var attempts = preferredPort == 0 ? 1 : PortAttempts;
            for (var i = 0; i < attempts; i++)
            {
                var app = Build(preferredPort == 0 ? 0 : preferredPort + i, certificate);
                try
                {
                    await app.StartAsync(cancellationToken).ConfigureAwait(false);
                    _app = app;
                    Port = ReadBoundPort(app);
                    return;
                }
                catch (Exception e) when (e is IOException or SocketException or InvalidOperationException)
                {
                    lastError = e;
                    await app.DisposeAsync().ConfigureAwait(false);
                }
            }

            throw new IOException(
                $"No se pudo abrir ningún puerto entre el {preferredPort} y el {preferredPort + attempts - 1}.", lastError);
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_app is null) return;
            var app = _app;
            _app = null;
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await app.StopAsync(timeout.Token).ConfigureAwait(false);
            }
            finally
            {
                await app.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _lifecycle.Dispose();
    }

    private WebApplication Build(int port, X509Certificate2 certificate)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PhoneServer).Assembly.GetName().Name,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseKestrelHttpsConfiguration();
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Limits.MaxRequestBodySize = MaxBodyBytes;
            kestrel.ListenAnyIP(port, listen => listen.UseHttps(certificate));
        });

        var app = builder.Build();
        app.Run(HandleAsync);
        return app;
    }

    private static int ReadBoundPort(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()?.Addresses;
        foreach (var address in addresses ?? [])
        {
            // Kestrel puede devolver "https://[::]:47800"; se cambia el host para que Uri lo entienda.
            var normalized = address.Replace("[::]", "localhost").Replace("0.0.0.0", "localhost");
            if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri)) return uri.Port;
        }
        return 0;
    }

    private async Task HandleAsync(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;
        response.Headers.CacheControl = "no-store";
        response.Headers.XContentTypeOptions = "nosniff";
        var path = request.Path.Value ?? "/";

        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
        {
            if (path is "/" or "/index.html")
            {
                // La dirección lleva el código secreto: que no se envíe a otras webs.
                response.Headers["Referrer-Policy"] = "no-referrer";
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength = Page.Value.Length;
                if (HttpMethods.IsGet(request.Method)) await response.Body.WriteAsync(Page.Value, context.RequestAborted);
                return;
            }

            if (path == "/ping")
            {
                response.StatusCode = IsAuthorized(request) ? StatusCodes.Status204NoContent : StatusCodes.Status401Unauthorized;
                return;
            }

            response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (HttpMethods.IsPost(request.Method) && path == "/m")
        {
            if (!IsAuthorized(request))
            {
                response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            byte[] body;
            try
            {
                using var buffer = new MemoryStream();
                await request.Body.CopyToAsync(buffer, context.RequestAborted);
                body = buffer.ToArray();
            }
            catch (BadHttpRequestException)
            {
                response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                return;
            }

            if (!PhoneBatchParser.TryParse(body, out var batch) || batch is null)
            {
                response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            BatchReceived?.Invoke(batch, context.Connection.RemoteIpAddress?.MapToIPv4().ToString());
            response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        response.StatusCode = StatusCodes.Status405MethodNotAllowed;
    }

    private bool IsAuthorized(HttpRequest request)
    {
        var provided = request.Headers["X-Token"].ToString();
        if (string.IsNullOrEmpty(provided)) provided = request.Query["k"].ToString();
        var expected = _token;
        return expected.Length > 0 && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), expected);
    }

    private static byte[] LoadPage()
    {
        using var stream = typeof(PhoneServer).Assembly.GetManifestResourceStream("movil.html")
            ?? throw new InvalidOperationException("Falta la página del móvil en los recursos.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
