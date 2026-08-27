using System.Diagnostics;
using System.Net.NetworkInformation;
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
            string primaryAdapter = "Ethernet / Wi-Fi";
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

                if (primaryAdapter == "Ethernet / Wi-Fi" && !string.IsNullOrWhiteSpace(nic.Description))
                {
                    primaryAdapter = nic.Description;
                }
            }

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
}
