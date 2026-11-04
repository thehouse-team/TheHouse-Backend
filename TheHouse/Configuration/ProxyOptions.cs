using System.ComponentModel.DataAnnotations;
using System.Net;

namespace TheHouse.Configuration;

public sealed class ProxyOptions
{
    public bool Enabled { get; set; }

    public bool TrustAzureContainerAppsIngress { get; set; }

    [Range(1, 3)]
    public int ForwardLimit { get; set; } = 1;

    public string[] KnownProxies { get; set; } = [];

    public string[] KnownNetworks { get; set; } = [];

    public bool IsValid()
    {
        if (KnownProxies is null ||
            KnownNetworks is null)
        {
            return false;
        }

        if (!KnownProxies.All(
                value =>
                    IPAddress.TryParse(
                        value,
                        out var ip) &&
                    !ip.Equals(IPAddress.Any) &&
                    !ip.Equals(IPAddress.IPv6Any)))
        {
            return false;
        }

        if (!KnownNetworks.All(
                value =>
                    System.Net.IPNetwork.TryParse(
                        value,
                        out var network) &&
                    network.PrefixLength > 0))
        {
            return false;
        }

        if (!Enabled)
        {
            return !TrustAzureContainerAppsIngress;
        }

        if (TrustAzureContainerAppsIngress)
        {
            // En ACA la frontera de confianza es el ingress administrado.
            // No mezclar este modo con allowlists IP manuales.
            return
                ForwardLimit == 1 &&
                KnownProxies.Length == 0 &&
                KnownNetworks.Length == 0;
        }

        return
            KnownProxies.Length +
            KnownNetworks.Length > 0;
    }
}
