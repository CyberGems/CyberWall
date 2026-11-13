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

public sealed record TrafficHistoryPoint(
    DateTime Timestamp,
    double DownloadBps,
    double UploadBps
);

public sealed record NetworkAdapterOption(
    string Id,
    string DisplayName,
    bool IsUp,
    bool IsVirtualOrVpn
);

public sealed class NetworkSpeedService
{
    private static readonly Lazy<NetworkSpeedService> _instance = new(() => new NetworkSpeedService());
    public static NetworkSpeedService Instance => _instance.Value;

    private readonly System.Threading.Timer _timer;
    private volatile bool _running;
    private int _samplingLock;
    private readonly object _adapterConfigLock = new();
    private string? _preferredAdapterId;
    private string? _preferredAdapterName;
    private string? _lastAdapterId;
    private long _lastBytesReceived = -1;
    private long _lastBytesSent = -1;
    private long _sessionTotalBytesReceived = 0;
    private long _sessionTotalBytesSent = 0;
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _lastTime;

    private readonly List<TrafficHistoryPoint> _history = new();
    private readonly object _historyLock = new();

    public IReadOnlyList<TrafficHistoryPoint> GetHistory(int maxPoints = 60)
    {
        lock (_historyLock)
        {
            if (_history.Count == 0) return Array.Empty<TrafficHistoryPoint>();
            var skip = Math.Max(0, _history.Count - maxPoints);
            return _history.Skip(skip).ToList();
        }
    }

    public NetworkSpeedSnapshot CurrentSnapshot { get; private set; } = new(0, 0, 0, 0, "None", false);

    public string PreferredAdapterId
    {
        get
        {
            lock (_adapterConfigLock)
                return _preferredAdapterId ?? string.Empty;
        }
    }

    public event Action<NetworkSpeedSnapshot>? SpeedUpdated;
    public event Action<string>? PreferredAdapterChanged;

    private NetworkSpeedService()
    {
        _timer = new System.Threading.Timer(_ => Sample(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void SetPreferredAdapter(string? adapterId, string? fallbackDisplayName = null)
    {
        var normalizedId = string.IsNullOrWhiteSpace(adapterId) ? string.Empty : adapterId.Trim();
        var normalizedName = string.IsNullOrWhiteSpace(fallbackDisplayName) ? string.Empty : fallbackDisplayName.Trim();
        bool changed;
        lock (_adapterConfigLock)
        {
            changed = !string.Equals(_preferredAdapterId ?? string.Empty, normalizedId, StringComparison.OrdinalIgnoreCase);
            _preferredAdapterId = string.IsNullOrEmpty(normalizedId) ? null : normalizedId;
            _preferredAdapterName = string.IsNullOrEmpty(normalizedName) ? null : normalizedName;
        }

        if (!changed) return;

        lock (_historyLock)
        {
            _history.Clear();
        }

        _lastAdapterId = null;
        _lastBytesReceived = -1;
        _lastBytesSent = -1;
        _lastTime = _stopwatch.Elapsed;
        Sample(isInitial: true);
        PreferredAdapterChanged?.Invoke(normalizedId);
    }

    public IReadOnlyList<NetworkAdapterOption> GetSelectableAdapters()
    {
        try
        {
            return BuildSelectableAdapters(NetworkInterface.GetAllNetworkInterfaces());
        }
        catch
        {
            return Array.Empty<NetworkAdapterOption>();
        }
    }

    public void Start()
    {
        if (!_running)
        {
            _running = true;
            _stopwatch.Restart();
            _lastTime = _stopwatch.Elapsed;
            Sample(isInitial: true);
            _timer.Change(1000, 1000);
        }
    }

    public void Stop()
    {
        _running = false;
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        _stopwatch.Stop();
    }

    private void Sample(bool isInitial = false)
    {
        if (!_running && !isInitial) return;
        if (Interlocked.Exchange(ref _samplingLock, 1) == 1) return;

        try
        {
            string? preferredId;
            string? preferredName;
            lock (_adapterConfigLock)
            {
                preferredId = _preferredAdapterId;
                preferredName = _preferredAdapterName;
            }

            var nics = NetworkInterface.GetAllNetworkInterfaces();
            var primaryNic = SelectTargetAdapter(nics, preferredId);

            if (primaryNic == null)
            {
                _lastAdapterId = null;
                _lastBytesReceived = -1;
                _lastBytesSent = -1;

                var offlineAdapterName = "Offline";
                if (!string.IsNullOrWhiteSpace(preferredId))
                {
                    var pinnedNic = nics.FirstOrDefault(n => string.Equals(n.Id, preferredId, StringComparison.OrdinalIgnoreCase));
                    var knownName = pinnedNic != null
                        ? GetAdapterDisplayName(pinnedNic)
                        : preferredName;
                    if (!string.IsNullOrWhiteSpace(knownName))
                    {
                        offlineAdapterName = Common.I18n.Strings.T("NetworkAdapterDisconnected", knownName);
                    }
                }

                CurrentSnapshot = new NetworkSpeedSnapshot(
                    0,
                    0,
                    _sessionTotalBytesReceived,
                    _sessionTotalBytesSent,
                    offlineAdapterName,
                    false
                );
                SpeedUpdated?.Invoke(CurrentSnapshot);
                return;
            }

            var stats = primaryNic.GetIPStatistics();
            long currentBytesIn = stats.BytesReceived;
            long currentBytesOut = stats.BytesSent;
            string adapterName = GetAdapterDisplayName(primaryNic);

            if (!string.IsNullOrWhiteSpace(preferredId))
            {
                lock (_adapterConfigLock)
                {
                    _preferredAdapterName = adapterName;
                }
            }

            var now = _stopwatch.Elapsed;
            var elapsedSec = (now - _lastTime).TotalSeconds;
            _lastTime = now;

            double downBps = 0;
            double upBps = 0;

            bool isNewAdapter = _lastAdapterId != primaryNic.Id;

            if (isNewAdapter)
            {
                _lastAdapterId = primaryNic.Id;
                _lastBytesReceived = currentBytesIn;
                _lastBytesSent = currentBytesOut;
            }
            else if (!isInitial && _lastBytesReceived >= 0 && elapsedSec > 0.1)
            {
                var diffIn = Math.Max(0, currentBytesIn - _lastBytesReceived);
                var diffOut = Math.Max(0, currentBytesOut - _lastBytesSent);
                downBps = diffIn / elapsedSec;
                upBps = diffOut / elapsedSec;

                _sessionTotalBytesReceived += diffIn;
                _sessionTotalBytesSent += diffOut;

                _lastBytesReceived = currentBytesIn;
                _lastBytesSent = currentBytesOut;
            }
            else
            {
                _lastBytesReceived = currentBytesIn;
                _lastBytesSent = currentBytesOut;
            }

            CurrentSnapshot = new NetworkSpeedSnapshot(
                downBps,
                upBps,
                _sessionTotalBytesReceived,
                _sessionTotalBytesSent,
                adapterName,
                true
            );

            lock (_historyLock)
            {
                _history.Add(new TrafficHistoryPoint(DateTime.UtcNow, downBps, upBps));
                if (_history.Count > 720)
                {
                    _history.RemoveRange(0, _history.Count - 720);
                }
            }

            SpeedUpdated?.Invoke(CurrentSnapshot);
        }
        catch
        {
            // Ignore transient network stack queries
        }
        finally
        {
            Interlocked.Exchange(ref _samplingLock, 0);
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

    private static string GetAdapterDisplayName(NetworkInterface nic) =>
        !string.IsNullOrWhiteSpace(nic.Description) ? nic.Description : nic.Name;

    internal static NetworkInterface? SelectTargetAdapter(NetworkInterface[] nics, string? preferredAdapterId)
    {
        if (!string.IsNullOrWhiteSpace(preferredAdapterId))
        {
            return nics.FirstOrDefault(n =>
                string.Equals(n.Id, preferredAdapterId, StringComparison.OrdinalIgnoreCase) &&
                n.OperationalStatus == OperationalStatus.Up &&
                !IsFilterInterface(n, requireUnicastIp: false));
        }

        return DetectPrimaryAdapter(nics);
    }

    internal static IReadOnlyList<NetworkAdapterOption> BuildSelectableAdapters(IEnumerable<NetworkInterface> nics)
    {
        var list = new List<NetworkAdapterOption>();
        foreach (var nic in nics)
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                continue;

            bool isUp = nic.OperationalStatus == OperationalStatus.Up;
            if (IsFilterInterface(nic, requireUnicastIp: isUp))
                continue;

            var desc = nic.Description ?? string.Empty;
            if (desc.Contains("WAN Miniport", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("Kernel Debug", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("Teredo", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("6to4", StringComparison.OrdinalIgnoreCase) ||
                desc.Contains("IP-HTTPS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            list.Add(new NetworkAdapterOption(
                nic.Id,
                GetAdapterDisplayName(nic),
                isUp,
                IsVirtualOrVpnAdapter(nic)));
        }

        return list
            .OrderByDescending(a => a.IsUp)
            .ThenBy(a => a.IsVirtualOrVpn)
            .ThenBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsFilterInterface(NetworkInterface nic, bool requireUnicastIp = true)
    {
        var desc = nic.Description ?? string.Empty;
        var name = nic.Name ?? string.Empty;

        // Common NDIS Lightweight Filter (LWF) driver patterns
        if (desc.Contains("LightWeight Filter", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("Packet Scheduler", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("NDIS Light-Weight", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("WFP Native MAC Layer", StringComparison.OrdinalIgnoreCase) ||
            desc.Contains("WFP 802.3 MAC Layer", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("LightWeight Filter", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Packet Scheduler", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("-WFP", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("QoS Packet Scheduler", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!requireUnicastIp)
            return false;

        try
        {
            // All functional IP network adapters must have at least one unicast IP address.
            // NDIS filter drivers and raw bindings lack unicast IP configuration.
            var ipProps = nic.GetIPProperties();
            if (ipProps == null || ipProps.UnicastAddresses.Count == 0)
            {
                return true;
            }
        }
        catch
        {
            return true;
        }

        return false;
    }

    private static bool IsVirtualOrVpnAdapter(NetworkInterface nic)
    {
        if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            return true;

        var desc = nic.Description?.ToLowerInvariant() ?? string.Empty;
        var name = nic.Name?.ToLowerInvariant() ?? string.Empty;

        return desc.Contains("virtual") || desc.Contains("vpn") || desc.Contains("radmin")
            || desc.Contains("tap-") || desc.Contains("tap ") || desc.Contains("vmware") || desc.Contains("virtualbox")
            || desc.Contains("npcap") || desc.Contains("hyper-v") || desc.Contains("host-only") || desc.Contains("bluetooth")
            || desc.Contains("pcap") || desc.Contains("wsl") || desc.Contains("vethernet") || desc.Contains("wireguard")
            || desc.Contains("zerotier") || desc.Contains("tailscale") || name.Contains("radmin") || name.Contains("vpn")
            || name.Contains("tap") || name.Contains("vmware") || name.Contains("vbox") || name.Contains("wsl");
    }

    private static NetworkInterface? DetectPrimaryAdapter(NetworkInterface[] nics)
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
                    !IsFilterInterface(n) &&
                    n.GetIPProperties().UnicastAddresses.Any(u => u.Address.Equals(ep.Address)));

                if (matchedNic != null)
                {
                    return matchedNic;
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
            if (IsFilterInterface(nic))
                continue;

            int score = 0;

            // Heavily penalize virtual/VPN adapters
            bool isVirtualOrVpn = IsVirtualOrVpnAdapter(nic);

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

        return bestNic;
    }
}
