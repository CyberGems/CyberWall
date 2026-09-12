using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CyberWall.Common.Models;
using CyberWall.Service.Wfp;

namespace CyberWall.UI.Services;

public sealed record ProcessBandwidthUsage(
    string AppPath,
    string DisplayName,
    double DownloadBps,
    double UploadBps,
    long TotalBytesIn,
    long TotalBytesOut
)
{
    public double TotalBps => DownloadBps + UploadBps;
    public string FormattedDownload => NetworkSpeedService.FormatSpeed(DownloadBps);
    public string FormattedUpload => NetworkSpeedService.FormatSpeed(UploadBps);
    public string FormattedTotalTransfer => NetworkSpeedService.FormatBytes(TotalBytesIn + TotalBytesOut);
    public string QuickBlockText => CyberWall.Common.I18n.Strings.T("BandwidthQuickBlock");
    public double UsagePercent { get; set; }
}

public sealed class ProcessBandwidthService : IDisposable
{
    private static readonly Lazy<ProcessBandwidthService> _instance = new(() => new ProcessBandwidthService());
    public static ProcessBandwidthService Instance => _instance.Value;

    private readonly ConcurrentDictionary<string, ProcessBandwidthUsage> _appBandwidths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, (string? Path, DateTime CachedAt)> _pidPathCache = new();
    private readonly ConcurrentDictionary<int, (ulong ReadTransfer, ulong WriteTransfer)> _lastIoCounters = new();

    private volatile bool _running;
    private readonly System.Threading.Timer _aggregationTimer;
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _lastAggregationTime;

    public event Action? BandwidthUpdated;

    private ProcessBandwidthService()
    {
        _aggregationTimer = new System.Threading.Timer(_ => Aggregate(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _stopwatch.Restart();
        _lastAggregationTime = _stopwatch.Elapsed;

        NetworkSpeedService.Instance.Start();

        // Start 1000ms periodic aggregation timer
        _aggregationTimer.Change(1000, 1000);
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _aggregationTimer.Change(Timeout.Infinite, Timeout.Infinite);

        _stopwatch.Stop();
    }

    public ProcessBandwidthUsage? GetBandwidth(string appPath)
    {
        if (string.IsNullOrWhiteSpace(appPath)) return null;
        if (_appBandwidths.TryGetValue(appPath, out var usage)) return usage;

        try
        {
            var norm = AppRule.Normalize(appPath);
            if (_appBandwidths.TryGetValue(norm, out usage)) return usage;
        }
        catch { }

        return null;
    }

    public IReadOnlyList<ProcessBandwidthUsage> GetTopConsumers(int maxCount = 6)
    {
        var items = _appBandwidths.Values
            .Where(b => b.TotalBps > 100) // Filter out negligible/idle noise
            .OrderByDescending(b => b.TotalBps)
            .Take(maxCount)
            .ToList();

        double maxBps = items.Count > 0 ? items.Max(i => i.TotalBps) : 0;
        if (maxBps > 0)
        {
            foreach (var item in items)
            {
                item.UsagePercent = Math.Clamp((item.TotalBps / maxBps) * 100.0, 5.0, 100.0);
            }
        }

        return items;
    }

    private void Aggregate()
    {
        if (!_running) return;

        try
        {
            var nowTime = _stopwatch.Elapsed;
            double elapsedSeconds = (nowTime - _lastAggregationTime).TotalSeconds;
            if (elapsedSeconds < 0.2) return;
            _lastAggregationTime = nowTime;

            // Cache cleanup every 45s
            var nowUtc = DateTime.UtcNow;
            if (_pidPathCache.Count > 200)
            {
                foreach (var kvp in _pidPathCache)
                {
                    if ((nowUtc - kvp.Value.CachedAt).TotalSeconds > 45)
                    {
                        _pidPathCache.TryRemove(kvp.Key, out _);
                    }
                }
            }

            var deltaDownByPath = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var deltaUpByPath = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            // Sample process I/O counters on processes with active established remote sockets
            SampleProcessIoCounters(elapsedSeconds, deltaDownByPath, deltaUpByPath);

            // Reconcile with ground truth physical network card bandwidth
            var netSnap = NetworkSpeedService.Instance.CurrentSnapshot;
            double globalDown = Math.Max(0, netSnap.DownloadBps);
            double globalUp = Math.Max(0, netSnap.UploadBps);

            // Reconcile candidate rates with physical network adapter throughput
            double totalCandidateUp = deltaUpByPath.Values.Sum();
            if (totalCandidateUp > 0)
            {
                if (globalUp > 0 && totalCandidateUp > globalUp)
                {
                    double upScale = globalUp / totalCandidateUp;
                    foreach (var path in deltaUpByPath.Keys.ToList())
                    {
                        deltaUpByPath[path] = Math.Min(globalUp, deltaUpByPath[path] * upScale);
                    }
                }
                else if (globalUp <= 0 && netSnap.IsConnected)
                {
                    // Global upload is virtually 0 B/s; clamp process upload to prevent false I/O spikes
                    foreach (var path in deltaUpByPath.Keys.ToList())
                    {
                        deltaUpByPath[path] = Math.Min(50, deltaUpByPath[path]);
                    }
                }
            }

            double totalCandidateDown = deltaDownByPath.Values.Sum();
            if (totalCandidateDown > 0)
            {
                if (globalDown > 0 && totalCandidateDown > globalDown)
                {
                    double downScale = globalDown / totalCandidateDown;
                    foreach (var path in deltaDownByPath.Keys.ToList())
                    {
                        deltaDownByPath[path] = Math.Min(globalDown, deltaDownByPath[path] * downScale);
                    }
                }
                else if (globalDown <= 0 && netSnap.IsConnected)
                {
                    // Global download is virtually 0 B/s; clamp process download
                    foreach (var path in deltaDownByPath.Keys.ToList())
                    {
                        deltaDownByPath[path] = Math.Min(50, deltaDownByPath[path]);
                    }
                }
            }

            // Update app bandwidth states with smooth decay
            var allKnownPaths = _appBandwidths.Keys.Concat(deltaDownByPath.Keys).Concat(deltaUpByPath.Keys).Distinct().ToList();
            foreach (var path in allKnownPaths)
            {
                bool hasCurrent = deltaDownByPath.TryGetValue(path, out double currentDown) && currentDown >= 100;
                bool hasCurrentUp = deltaUpByPath.TryGetValue(path, out double currentUp) && currentUp >= 100;

                if (hasCurrent || hasCurrentUp)
                {
                    var existing = _appBandwidths.GetValueOrDefault(path);
                    long totalIn = (existing?.TotalBytesIn ?? 0) + (long)(currentDown * elapsedSeconds);
                    long totalOut = (existing?.TotalBytesOut ?? 0) + (long)(currentUp * elapsedSeconds);

                    string displayName;
                    try { displayName = Path.GetFileNameWithoutExtension(path); }
                    catch { displayName = path; }

                    _appBandwidths[path] = new ProcessBandwidthUsage(
                        path,
                        displayName,
                        hasCurrent ? currentDown : 0,
                        hasCurrentUp ? currentUp : 0,
                        totalIn,
                        totalOut
                    );
                }
                else if (_appBandwidths.TryGetValue(path, out var existing))
                {
                    if (existing.TotalBps > 100)
                    {
                        // Rapid decay to 0 when process stops sending/receiving
                        double newDown = existing.DownloadBps * 0.3;
                        double newUp = existing.UploadBps * 0.3;
                        if ((newDown + newUp) < 100)
                        {
                            newDown = 0;
                            newUp = 0;
                        }

                        _appBandwidths[path] = existing with
                        {
                            DownloadBps = newDown,
                            UploadBps = newUp
                        };
                    }
                    else if (existing.TotalBps > 0)
                    {
                        _appBandwidths[path] = existing with { DownloadBps = 0, UploadBps = 0 };
                    }
                }
            }

            BandwidthUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProcessBandwidthService.Aggregate error: {ex.Message}");
        }
    }

    private void SampleProcessIoCounters(double elapsed, Dictionary<string, double> downMap, Dictionary<string, double> upMap)
    {
        // Query active processes with established non-loopback network sockets
        var activePids = ProcessTrafficTracker.Instance.GetActivePids();
        if (activePids.Count == 0)
        {
            _lastIoCounters.Clear();
            return;
        }

        var activePidsSet = new HashSet<int>(activePids);

        // Remove cached PIDs that are no longer active network sockets
        foreach (var key in _lastIoCounters.Keys)
        {
            if (!activePidsSet.Contains(key))
            {
                _lastIoCounters.TryRemove(key, out _);
            }
        }

        foreach (var pid in activePids)
        {
            if (pid <= 4) continue;
            try
            {
                using var proc = Process.GetProcessById(pid);
                if (proc.HasExited) continue;

                if (GetProcessIoCounters(proc.Handle, out var io))
                {
                    if (_lastIoCounters.TryGetValue(pid, out var last))
                    {
                        if (io.ReadTransferCount >= last.ReadTransfer && io.WriteTransferCount >= last.WriteTransfer)
                        {
                            ulong deltaRead = io.ReadTransferCount - last.ReadTransfer;
                            ulong deltaWrite = io.WriteTransferCount - last.WriteTransfer;

                            if (deltaRead > 64 || deltaWrite > 64)
                            {
                                var path = ResolvePidToPath(pid);
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    if (deltaRead > 64)
                                        downMap[path] = downMap.GetValueOrDefault(path) + (deltaRead / elapsed);
                                    if (deltaWrite > 64)
                                        upMap[path] = upMap.GetValueOrDefault(path) + (deltaWrite / elapsed);
                                }
                            }
                        }
                    }
                    _lastIoCounters[pid] = (io.ReadTransferCount, io.WriteTransferCount);
                }
            }
            catch { }
        }
    }

    private string? ResolvePidToPath(int pid)
    {
        if (_pidPathCache.TryGetValue(pid, out var cached))
        {
            return cached.Path;
        }

        string? path = null;
        try
        {
            path = ProcessIdentity.GetImagePath(pid);
            if (path != null && HostAppResolver.TryResolveHost(pid, path, out var hostPath, out _))
            {
                path = hostPath;
            }
        }
        catch { }

        _pidPathCache[pid] = (path, DateTime.UtcNow);
        return path;
    }

    #region Win32 P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    public struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

    #endregion

    public void Dispose()
    {
        Stop();
        _aggregationTimer.Dispose();
    }
}
