using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using LanTransfer.Core.Options;

namespace LanTransfer.Services;

public sealed class ConnectionUrlProvider
{
    private readonly LanTransferOptions _options;

    public ConnectionUrlProvider(LanTransferOptions options)
    {
        _options = options;
    }

    public string LocalUrl => BuildUrl("localhost");

    public IReadOnlyList<ConnectionUrl> GetConnectionUrls()
    {
        var addresses = new List<AddressCandidate>();

        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                    networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var properties = networkInterface.GetIPProperties();
                var hasGateway = properties.GatewayAddresses.Any(item =>
                    item.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !item.Address.Equals(IPAddress.Any));
                var interfacePriority = networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ? 0 : 1;

                addresses.AddRange(properties.UnicastAddresses
                    .Select(item => item.Address)
                    .Where(IsUsableLanAddress)
                    .Select(address => new AddressCandidate(address, hasGateway ? 0 : 1, interfacePriority)));
            }
        }
        catch (NetworkInformationException)
        {
            // The localhost diagnostic fallback below keeps startup functional.
        }
        catch (SocketException)
        {
            // The localhost diagnostic fallback below keeps startup functional.
        }

        var urls = addresses
            .GroupBy(candidate => candidate.Address)
            .Select(group => group.OrderBy(candidate => candidate.GatewayPriority)
                .ThenBy(candidate => candidate.InterfacePriority)
                .First())
            .OrderBy(candidate => candidate.GatewayPriority)
            .ThenBy(candidate => candidate.InterfacePriority)
            .ThenBy(candidate => candidate.Address.ToString(), StringComparer.Ordinal)
            .Select(candidate => new ConnectionUrl(
                BuildUrl(candidate.Address.ToString()),
                candidate.Address.ToString(),
                true))
            .ToList();

        if (urls.Count == 0)
        {
            urls.Add(new ConnectionUrl(LocalUrl, "localhost", false));
        }

        return urls;
    }

    private string BuildUrl(string host)
    {
        var builder = new UriBuilder(Uri.UriSchemeHttp, host, _options.Port);
        if (!string.IsNullOrWhiteSpace(_options.AccessToken))
        {
            builder.Query = $"token={Uri.EscapeDataString(_options.AccessToken)}";
        }

        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static bool IsUsableLanAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(address))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return !(bytes[0] == 169 && bytes[1] == 254) && !address.Equals(IPAddress.Any);
    }

    private sealed record AddressCandidate(IPAddress Address, int GatewayPriority, int InterfacePriority);
}

public sealed record ConnectionUrl(string Url, string Label, bool IsLanAddress);
