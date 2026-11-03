using System.ComponentModel.DataAnnotations;
using System.Net;

namespace TheHouse.Configuration;

public sealed class ProxyOptions
{
    public bool Enabled { get; set; }

    [Range(1, 3)]
    public int ForwardLimit { get; set; } = 1;

    public string[] KnownProxies { get; set; } = [];
    public string[] KnownNetworks { get; set; } = [];

    public bool IsValid() => KnownProxies is not null && KnownNetworks is not null &&
        KnownProxies.All(value => IPAddress.TryParse(value, out var ip) &&
            !ip.Equals(IPAddress.Any) && !ip.Equals(IPAddress.IPv6Any)) &&
        KnownNetworks.All(value => System.Net.IPNetwork.TryParse(value, out var network) &&
            network.PrefixLength > 0) &&
        (!Enabled || KnownProxies.Length + KnownNetworks.Length > 0);
}
