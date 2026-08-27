using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows.Threading;

namespace CyberWall.UI.Services;

public sealed record NetworkSpeedSnapshot(
    double DownloadBps,
    double UploadBps,
    long TotalBytesReceived,
    long TotalBytesSent,
    string AdapterName,
    bool IsConnected
);

public sealed class NetworkSpeedService
{
    private static readonly Lazy<NetworkSpeedService> _instance = new(() => new NetworkSpeedService());
    public static NetworkSpeedService Instance => _instance.Value;

    private readonly DispatcherTimer _timer;
    private long _lastBytesReceived = -1;
    private long _lastBytesSent = -1;
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _lastTime;
    private long _sessionReceivedBase = -1;
    private long _sessionSentBase = -1;

    public NetworkSpeedSnapshot CurrentSnapshot { get; private set; } = new(0, 0, 0, 0, "None", false);

    public event Action<NetworkSpeedSnapshot>? SpeedUpdated;

    private NetworkSpeedService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _timer.Tick += (_, _) => Sample();
    }

    public void Start()
    {
        if (!_timer.IsEnabled)
        {
            _stopwatch.Restart();
            _lastTime = _stopwatch.Elapsed;
            Sample(isInitial: true);
            _timer.Start();
        }
    }

    public void Stop()
    {
        _timer.Stop();
        _stopwatch.Stop();
    }

    private void Sample(bool isInitial = false)
    {
        try
        {
            long currentBytesIn = 0;
            long currentBytesOut = 0;
            bool hasActive = false;

            var nics = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var nic in nics)
            {
                if (nic.OperationalStatus != OperationalStatus.Up)
                    continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    continue;

                var stats = nic.GetIPStatistics();
                currentBytesIn += stats.BytesReceived;
                currentBytesOut += stats.BytesSent;
                hasActive = true;
            }

            string primaryAdapter = hasActive ? DetectPrimaryAdapterName(nics) : "None";

            if (_sessionReceivedBase < 0 && hasActive)
            {
                _sessionReceivedBase = currentBytesIn;
                _sessionSentBase = currentBytesOut;
            }

            var now = _stopwatch.Elapsed;
            var elapsedSec = (now - _lastTime).TotalSeconds;
            _lastTime = now;

            double downBps = 0;
            double upBps = 0;

            if (!isInitial && _lastBytesReceived >= 0 && elapsedSec > 0.1)
            {
                var diffIn = Math.Max(0, currentBytesIn - _lastBytesReceived);
                var diffOut = Math.Max(0, currentBytesOut - _lastBytesSent);
                downBps = diffIn / elapsedSec;
                upBps = diffOut / elapsedSec;
            }

            _lastBytesReceived = currentBytesIn;
            _lastBytesSent = currentBytesOut;

            long sessionIn = _sessionReceivedBase >= 0 ? Math.Max(0, currentBytesIn - _sessionReceivedBase) : 0;
            long sessionOut = _sessionSentBase >= 0 ? Math.Max(0, currentBytesOut - _sessionSentBase) : 0;

            CurrentSnapshot = new NetworkSpeedSnapshot(
                downBps,
                upBps,
                sessionIn,
                sessionOut,
                hasActive ? primaryAdapter : "Offline",
                hasActive
            );

            SpeedUpdated?.Invoke(CurrentSnapshot);
        }
        catch
        {
            // Ignore transient network stack queries
        }
    }

    public static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec < 1024)
            return $"{bytesPerSec:0} B/s";
        if (bytesPerSec < 1024 * 1024)
            return $"{bytesPerSec / 1024.0:0.0} KB/s";
        if (bytesPerSec < 1024 * 1024 * 1024)
            return $"{bytesPerSec / (1024.0 * 1024.0):0.00} MB/s";
        return $"{bytesPerSec / (1024.0 * 1024.0 * 1024.0):0.00} GB/s";
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:0.0} KB";
        if (bytes < 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.00} GB";
    }

    private static string DetectPrimaryAdapterName(NetworkInterface[] nics)
    {
        try
        {
            // 1. Direct OS routing table probe via UDP connect (instantaneous, non-blocking, kernel local route resolution)
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint ep && !ep.Address.Equals(IPAddress.Any))
            {
                var matchedNic = nics.FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    n.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                    n.GetIPProperties().UnicastAddresses.Any(u => u.Address.Equals(ep.Address)));

                if (matchedNic != null && !string.IsNullOrWhiteSpace(matchedNic.Description))
                {
                    return matchedNic.Description;
                }
            }
        }
        catch
        {
            // Fallback to heuristic scoring below
        }

        // 2. Intelligent scoring heuristic fallback (gateway, physical type, non-virtual, traffic volume)
        NetworkInterface? bestNic = null;
        int bestScore = int.MinValue;

        foreach (var nic in nics)
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            int score = 0;
            var desc = nic.Description?.ToLowerInvariant() ?? string.Empty;
            var name = nic.Name?.ToLowerInvariant() ?? string.Empty;

            // Heavily penalize virtual/VPN adapters
            bool isVirtualOrVpn = desc.Contains("virtual") || desc.Contains("vpn") || desc.Contains("radmin")
                || desc.Contains("tap-") || desc.Contains("tap ") || desc.Contains("vmware") || desc.Contains("virtualbox")
                || desc.Contains("npcap") || desc.Contains("hyper-v") || desc.Contains("host-only") || desc.Contains("bluetooth")
                || desc.Contains("pcap") || desc.Contains("wsl") || desc.Contains("vethernet") || desc.Contains("wireguard")
                || desc.Contains("zerotier") || desc.Contains("tailscale") || name.Contains("radmin") || name.Contains("vpn")
                || name.Contains("tap") || name.Contains("vmware") || name.Contains("vbox") || name.Contains("wsl");

            if (isVirtualOrVpn)
            {
                score -= 1000;
            }

            try
            {
                var ipProps = nic.GetIPProperties();
                var hasIpv4Gateway = ipProps?.GatewayAddresses?.Any(g =>
                    g.Address != null &&
                    g.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !g.Address.Equals(IPAddress.Any) &&
                    !g.Address.ToString().StartsWith("0.")) == true;

                if (hasIpv4Gateway)
                {
                    score += 2000;
                }

                if (ipProps?.UnicastAddresses?.Any(u =>
                    u.Address != null &&
                    u.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !u.Address.Equals(IPAddress.Any) &&
                    !u.Address.ToString().StartsWith("169.254.") &&
                    !u.Address.ToString().StartsWith("127.")) == true)
                {
                    score += 500;
                }
            }
            catch { }

            if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            {
                score += 300;
            }
            else if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet && !isVirtualOrVpn)
            {
                score += 300;
            }

            try
            {
                var stats = nic.GetIPStatistics();
                var mbTransferred = (stats.BytesReceived + stats.BytesSent) / (1024.0 * 1024.0);
                score += Math.Min(400, (int)(mbTransferred * 2));
            }
            catch { }

            if (score > bestScore)
            {
                bestScore = score;
                bestNic = nic;
            }
        }

        if (bestNic != null && !string.IsNullOrWhiteSpace(bestNic.Description))
        {
            return bestNic.Description;
        }

        return "Ethernet / Wi-Fi";
    }
}
