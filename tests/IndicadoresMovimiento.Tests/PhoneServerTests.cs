using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using IndicadoresMovimiento.Core;
using IndicadoresMovimiento.Movil;

namespace IndicadoresMovimiento.Tests;

public sealed class PhoneServerTests : IDisposable
{
    private const string Token = "codigo123456";
    private readonly X509Certificate2 _certificate = X509CertificateLoader.LoadPkcs12(LocalCertificate.CreatePfx(), null);
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "im-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _certificate.Dispose();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private HttpClient CreateClient(int port, X509Certificate2? expected = null)
    {
        var expectedHash = (expected ?? _certificate).GetCertHashString();
        var handler = new HttpClientHandler
        {
            UseProxy = false,
            // Se acepta solo nuestro certificado autofirmado, como haría el móvil tras aceptar el aviso.
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null && certificate.GetCertHashString() == expectedHash,
        };
        return new HttpClient(handler) { BaseAddress = new Uri($"https://127.0.0.1:{port}") };
    }

    private static HttpRequestMessage Post(string json, string? token = Token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/m")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (token is not null) request.Headers.Add("X-Token", token);
        return request;
    }

    [Fact]
    public async Task Sirve_la_pagina_y_acepta_solo_lecturas_con_el_codigo_correcto()
    {
        await using var server = new PhoneServer();
        var received = new List<PhoneBatch>();
        server.BatchReceived += (batch, _) => { lock (received) received.Add(batch); };
        await server.StartAsync(0, Token, _certificate);
        using var client = CreateClient(server.Port);

        var page = await client.GetAsync($"/?k={Token}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Indicadores de movimiento", await page.Content.ReadAsStringAsync());
        Assert.Equal("no-referrer", page.Headers.GetValues("Referrer-Policy").Single());

        const string json = """{"ang":0,"s":[[1,0,0,9.8],[1.02,0,0,9.8,0,0,0]]}""";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Post(json, token: null))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Post(json, token: "otro-codigo"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Post("no es json"))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(Post(json))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/otra")).StatusCode);

        lock (received)
        {
            var batch = Assert.Single(received);
            Assert.Equal(2, batch.Samples.Count);
        }
    }

    [Fact]
    public async Task Si_el_puerto_esta_ocupado_usa_el_siguiente()
    {
        using var blocker = new TcpListener(Socket.OSSupportsIPv6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
        if (Socket.OSSupportsIPv6) blocker.Server.DualMode = true;
        blocker.Start();
        var busyPort = ((IPEndPoint)blocker.LocalEndpoint).Port;

        await using var server = new PhoneServer();
        await server.StartAsync(busyPort, Token, _certificate);
        Assert.NotEqual(busyPort, server.Port);
        Assert.InRange(server.Port, busyPort + 1, busyPort + PhoneServer.PortAttempts - 1);
    }

    [Fact]
    public async Task El_movil_como_fuente_calcula_el_movimiento_del_coche()
    {
        using var source = new PhoneMotionSource(new MotionProcessorOptions(), Path.Combine(_directory, "cert.pfx"));
        source.Configure(0, Token);
        await source.StartAsync();
        Assert.Null(source.LastError);
        Assert.True(source.IsRunning);
        Assert.Contains("Esperando", source.StatusText);
        Assert.True(File.Exists(Path.Combine(_directory, "cert.pfx")));

        // Móvil tumbado, parte de arriba hacia delante; el coche acelera a 2 m/s² tras un segundo parado.
        using var saved = LocalCertificate.LoadOrCreate(Path.Combine(_directory, "cert.pfx"));
        using var client = CreateClient(source.Port, saved);
        var t = 0.0;
        for (var packet = 0; packet < 50; packet++)
        {
            var rows = new List<string>();
            for (var i = 0; i < 3; i++)
            {
                t += 1 / 60.0;
                var longitudinal = t > 1 ? 2.0 : 0.0;
                rows.Add(string.Create(CultureInfo.InvariantCulture, $"[{t:0.#####},0,{longitudinal},9.80665]"));
            }
            var response = await client.SendAsync(Post($$"""{"ang":0,"s":[{{string.Join(",", rows)}}]}"""));
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        var state = source.Processor.GetState();
        Assert.True(state.HasData);
        Assert.True(state.Longitudinal > 1.2, $"longitudinal = {state.Longitudinal}");
        Assert.InRange(state.Lateral, -0.2, 0.2);
        Assert.Contains("Recibiendo", source.StatusText);
        Assert.Equal("127.0.0.1", source.ClientAddress);

        await source.StopAsync();
        Assert.False(source.IsRunning);
    }

    [Fact]
    public void El_certificado_se_reutiliza_entre_ejecuciones()
    {
        var path = Path.Combine(_directory, "cert.pfx");
        using var first = LocalCertificate.LoadOrCreate(path);
        using var second = LocalCertificate.LoadOrCreate(path);
        Assert.True(first.HasPrivateKey);
        Assert.Equal(first.Thumbprint, second.Thumbprint);
        Assert.True((first.NotAfter - first.NotBefore).TotalDays <= 398);
        Assert.True(LocalCertificate.Covers(first, [IPAddress.Loopback]));
    }

    [Fact]
    public void Si_cambia_la_red_se_crea_un_certificado_con_la_nueva_direccion()
    {
        var path = Path.Combine(_directory, "cert.pfx");
        var hotspot = IPAddress.Parse("172.20.10.2");
        using var first = LocalCertificate.LoadOrCreate(path);
        Assert.False(LocalCertificate.Covers(first, [hotspot]));

        using var second = LocalCertificate.LoadOrCreate(path, [hotspot]);
        Assert.NotEqual(first.Thumbprint, second.Thumbprint);
        Assert.True(LocalCertificate.Covers(second, [hotspot, IPAddress.Loopback]));

        using var third = LocalCertificate.LoadOrCreate(path, [hotspot]);
        Assert.Equal(second.Thumbprint, third.Thumbprint);
    }
}
