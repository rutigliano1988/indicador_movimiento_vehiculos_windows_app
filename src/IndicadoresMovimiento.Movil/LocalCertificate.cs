using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace IndicadoresMovimiento.Movil;

/// <summary>
/// Certificado autofirmado para servir la página del móvil por HTTPS. Los navegadores solo dejan
/// leer los sensores de movimiento en páginas seguras, así que hace falta aunque la conexión sea local.
/// Se guarda en disco para que el móvil no tenga que aceptar un certificado nuevo cada vez.
/// </summary>
public static class LocalCertificate
{
    private const X509KeyStorageFlags KeyStorageFlags = X509KeyStorageFlags.UserKeySet;

    /// <summary>
    /// Carga el certificado guardado o crea uno nuevo si no existe, está a punto de caducar o no incluye
    /// alguna de las direcciones indicadas (Safari en iPhone es estricto con los certificados).
    /// </summary>
    public static X509Certificate2 LoadOrCreate(string path, IEnumerable<IPAddress>? requiredAddresses = null)
    {
        var required = requiredAddresses?.ToList() ?? [];
        try
        {
            if (File.Exists(path))
            {
                var existing = X509CertificateLoader.LoadPkcs12FromFile(path, null, KeyStorageFlags);
                if (existing.HasPrivateKey && existing.NotAfter > DateTime.Now.AddDays(14) && Covers(existing, required))
                    return existing;
                existing.Dispose();
            }
        }
        catch (Exception e) when (e is CryptographicException or IOException or UnauthorizedAccessException)
        {
            // Dañado o ilegible: se crea uno nuevo.
        }

        var pfx = CreatePfx(required);
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, pfx);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Sin poder guardarlo, se usa solo en esta sesión.
        }

        return X509CertificateLoader.LoadPkcs12(pfx, null, KeyStorageFlags);
    }

    /// <summary>Si el certificado incluye todas las direcciones IP indicadas.</summary>
    public static bool Covers(X509Certificate2 certificate, IEnumerable<IPAddress> addresses)
    {
        var names = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>()
            .SelectMany(e => e.EnumerateIPAddresses())
            .ToHashSet();
        return addresses.All(names.Contains);
    }

    internal static byte[] CreatePfx(IEnumerable<IPAddress>? extraAddresses = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Indicadores de movimiento (conexion local)", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        var addresses = new HashSet<IPAddress> { IPAddress.Loopback };
        addresses.UnionWith(NetworkAddresses.GetCandidates().Select(a => a.Address));
        if (extraAddresses is not null) addresses.UnionWith(extraAddresses);
        foreach (var address in addresses) names.AddIpAddress(address);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1")], false)); // autenticación de servidor
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        // Apple rechaza certificados de servidor con más de 398 días de validez.
        var notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        using var certificate = request.CreateSelfSigned(notBefore, notBefore.AddDays(397));
        return certificate.Export(X509ContentType.Pfx);
    }
}
