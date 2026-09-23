using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace IndicadoresMovimiento.Movil;

/// <summary>Una dirección IPv4 de este ordenador a la que puede conectarse el móvil.</summary>
/// <param name="IsPhoneHotspot">La red parece el punto de acceso personal de un iPhone (172.20.10.x).</param>
/// <param name="IsLikelyVirtual">Adaptador de máquina virtual, VPN, etc.: casi nunca es la red del móvil.</param>
public sealed record LocalAddress(IPAddress Address, string InterfaceName, bool IsPhoneHotspot, bool IsLikelyVirtual = false);

public static class NetworkAddresses
{
    // Adaptadores que casi nunca son la red compartida con el móvil.
    private static readonly string[] VirtualHints =
    [
        "virtual", "vethernet", "hyper-v", "vmware", "virtualbox", "wsl", "docker", "vpn",
        "tap-", "tunnel", "bluetooth", "zerotier", "tailscale", "wireguard", "npcap", "loopback",
    ];

    /// <summary>Direcciones candidatas, la más probable primero.</summary>
    public static IReadOnlyList<LocalAddress> GetCandidates()
    {
        var found = new List<(LocalAddress Address, int Priority)>();
        NetworkInterface[] interfaces;
        try
        {
            interfaces = NetworkInterface.GetAllNetworkInterfaces();
        }
        catch (NetworkInformationException)
        {
            return [];
        }

        foreach (var ni in interfaces)
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

            var text = (ni.Name + " " + ni.Description).ToLowerInvariant();
            var isVirtual = VirtualHints.Any(text.Contains);

            IPInterfaceProperties properties;
            try
            {
                properties = ni.GetIPProperties();
            }
            catch (NetworkInformationException)
            {
                continue;
            }

            foreach (var unicast in properties.UnicastAddresses)
            {
                var ip = unicast.Address;
                if (ip.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(ip)) continue;
                var bytes = ip.GetAddressBytes();
                if (bytes[0] == 169 && bytes[1] == 254) continue; // sin DHCP

                var hotspot = bytes[0] == 172 && bytes[1] == 20 && bytes[2] == 10;
                var priority = hotspot ? 0
                    : isVirtual ? 5
                    : ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 1
                    : IsPrivate(bytes) ? 2
                    : 3;
                found.Add((new LocalAddress(ip, ni.Name, hotspot, isVirtual && !hotspot), priority));
            }
        }

        return found
            .OrderBy(f => f.Priority)
            .ThenBy(f => f.Address.Address.ToString(), StringComparer.Ordinal)
            .Select(f => f.Address)
            .ToList();
    }

    private static bool IsPrivate(byte[] b) =>
        b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
}
