using System.Net;

namespace CyberWall.Common;

public static class NetworkEndpoint
{
    private static readonly Dictionary<int, string> WellKnown = new()
    {
        [20] = "FTP",
        [21] = "FTP",
        [22] = "SSH",
        [25] = "SMTP",
        [53] = "DNS",
        [80] = "HTTP",
        [110] = "POP3",
        [123] = "NTP",
        [135] = "RPC",
        [139] = "NetBIOS",
        [143] = "IMAP",
        [443] = "HTTPS",
        [445] = "SMB",
        [465] = "SMTPS",
        [587] = "SMTP",
        [853] = "DoT",
        [993] = "IMAPS",
        [995] = "POP3S",
        [3389] = "RDP",
        [3478] = "STUN",
        [5228] = "HTTPS",
        [5353] = "mDNS",
        [8080] = "HTTP",
        [8443] = "HTTPS",
    };

    public static string ServiceLabel(string protocol, int port)
    {
        if (WellKnown.TryGetValue(port, out var name)) return name;
        return string.IsNullOrWhiteSpace(protocol) ? "TCP" : protocol.ToUpperInvariant();
    }

    public static string FormatPrimary(string protocol, string address, int port, string? host = null)
    {
        var service = ServiceLabel(protocol, port);
        var dest = string.IsNullOrWhiteSpace(host) ? address : host;
        return $"{service} · {dest}:{port}";
    }

    public static string FormatSecondary(string address, int processId, bool hasHost)
    {
        var pid = $"PID {processId}";
        return hasHost ? $"{address} · {pid}" : pid;
    }

    public static async Task<string?> TryResolveHostAsync(string address, CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(address, out var ip)) return null;
        if (IPAddress.IsLoopback(ip)) return "localhost";
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMilliseconds(1500));
            var entry = await Dns.GetHostEntryAsync(address).WaitAsync(cts.Token).ConfigureAwait(false);
            var host = entry.HostName;
            if (string.IsNullOrWhiteSpace(host) || host.Equals(address, StringComparison.OrdinalIgnoreCase))
                return null;
            return host;
        }
        catch
        {
            return null;
        }
    }
}
