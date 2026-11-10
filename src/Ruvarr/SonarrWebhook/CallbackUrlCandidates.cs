using System.Net;
using System.Net.Sockets;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Ruvarr.SonarrWebhook;

internal interface ICallbackUrlCandidates
{
    IReadOnlyList<Uri> Discover(Uri sonarrBaseAddress);
}

// Ruvarr cannot know which of its addresses Sonarr can reach, so it offers candidates in order of
// likelihood and lets Sonarr's notification test pick the first that works.
internal sealed class CallbackUrlCandidates(IServer server, ILogger<CallbackUrlCandidates> logger) : ICallbackUrlCandidates
{
    public IReadOnlyList<Uri> Discover(Uri sonarrBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(sonarrBaseAddress);

        ICollection<string> addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses ?? [];
        int? port = ListeningHttpPort(addresses);
        if (port is null)
        {
            logger.LogWarning("Ruvarr is not listening on any plain HTTP address ({Addresses}); no Sonarr webhook callback URL can be offered", string.Join(", ", addresses));
            return [];
        }

        List<Uri> candidates = [];

        // The local address of a socket routed toward Sonarr is the address Sonarr sees Ruvarr from
        // on a shared network, e.g. the container's bridge IP.
        if (LocalAddressToward(sonarrBaseAddress) is IPAddress localAddress)
        {
            candidates.Add(CallbackUrl(localAddress.ToString(), port.Value));
        }

        candidates.Add(CallbackUrl(Dns.GetHostName(), port.Value));

        return [.. candidates.Distinct()];
    }

    private static int? ListeningHttpPort(IEnumerable<string> addresses) =>
        addresses
            .Select(BindingAddress.Parse)
            .FirstOrDefault(a => a.Scheme == Uri.UriSchemeHttp)?
            .Port;

    private IPAddress? LocalAddressToward(Uri sonarrBaseAddress)
    {
        try
        {
            // Connecting a UDP socket only selects a route; no packet is sent.
            using Socket socket = new(SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(sonarrBaseAddress.DnsSafeHost, sonarrBaseAddress.Port);
            IPAddress address = ((IPEndPoint)socket.LocalEndPoint!).Address;
            return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        }
        catch (SocketException ex)
        {
            logger.LogWarning(ex, "Could not open a socket toward Sonarr host {Host} to discover Ruvarr's local address", sonarrBaseAddress.Host);
            return null;
        }
    }

    private static Uri CallbackUrl(string host, int port) =>
        new UriBuilder(Uri.UriSchemeHttp, host, port, SonarrWebhookEndpoint.Path).Uri;
}
