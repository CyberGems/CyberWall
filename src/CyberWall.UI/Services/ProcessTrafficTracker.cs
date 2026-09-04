using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Service.Engine;
using CyberWall.Service.Wfp;

namespace CyberWall.UI.Services;

public enum ProcessActivityLevel
{
    Idle = 0,
    ActiveAllowed = 1,
    BlockedAttempts = 2
}

public sealed record ProcessActivityInfo(
    ProcessActivityLevel Level,
    int ActiveSockets,
    string? LastEndpoint,
    string? LastBlockedEndpoint,
    DateTime LastActivityUtc,
    DateTime LastBlockedUtc,
    double DownloadBps = 0,
    double UploadBps = 0,
    long TotalBytesIn = 0,
    long TotalBytesOut = 0
)
{
    public bool IsActive => Level == ProcessActivityLevel.ActiveAllowed;
    public bool IsBlocked => Level == ProcessActivityLevel.BlockedAttempts;
    public double TotalBps => DownloadBps + UploadBps;
    public bool HasBandwidth => TotalBps > 100;
    public string FormattedSpeed
    {
        get
        {
            if (DownloadBps >= 100 && UploadBps >= 100)
            {
                return $"↓ {NetworkSpeedService.FormatSpeed(DownloadBps)}  ↑ {NetworkSpeedService.FormatSpeed(UploadBps)}";
            }
            if (DownloadBps >= 100)
            {
                return $"↓ {NetworkSpeedService.FormatSpeed(DownloadBps)}";
            }
            if (UploadBps >= 100)
            {
                return $"↑ {NetworkSpeedService.FormatSpeed(UploadBps)}";
            }
            return string.Empty;
        }
    }
}

public sealed class ProcessTrafficTracker : IDisposable
{
    private static readonly Lazy<ProcessTrafficTracker> _instance = new(() => new ProcessTrafficTracker());
    public static ProcessTrafficTracker Instance => _instance.Value;

    private readonly DispatcherTimer _timer;
    private readonly ConcurrentDictionary<string, ProcessActivityState> _activities = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, string?> _pidPathCache = new();
    private DateTime _lastCacheCleanup = DateTime.UtcNow;
    private int[] _activePidsSnapshot = Array.Empty<int>();
    private bool _seeded;

    public event Action? ActivityUpdated;

    public IReadOnlyCollection<int> GetActivePids() => _activePidsSnapshot;

    private sealed class ProcessActivityState
    {
        public int ActiveSockets { get; set; }
        public string? LastEndpoint { get; set; }
        public string? LastBlockedEndpoint { get; set; }
        public DateTime LastActivityUtc { get; set; } = DateTime.MinValue;
        public DateTime LastBlockedUtc { get; set; } = DateTime.MinValue;
    }

    private ProcessTrafficTracker()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        if (!_seeded)
        {
            _seeded = true;
            Task.Run(SeedHistoryFromLog);
        }

        ProcessBandwidthService.Instance.Start();
        ProcessBandwidthService.Instance.BandwidthUpdated += OnBandwidthUpdated;

        if (!_timer.IsEnabled)
        {
            Poll();
            _timer.Start();
        }
    }

    public void Stop()
    {
        _timer.Stop();
        ProcessBandwidthService.Instance.BandwidthUpdated -= OnBandwidthUpdated;
        ProcessBandwidthService.Instance.Stop();
    }

    private void OnBandwidthUpdated()
    {
        ActivityUpdated?.Invoke();
    }

    public void RecordActivity(string appPath, string remoteAddress, int remotePort, bool isBlocked = false)
    {
        if (string.IsNullOrWhiteSpace(appPath)) return;
        if (string.IsNullOrWhiteSpace(remoteAddress) || remoteAddress == "0.0.0.0" || remoteAddress == "::") return;

        var state = _activities.GetOrAdd(appPath, _ => new ProcessActivityState());
        lock (state)
        {
            if (isBlocked)
            {
                state.LastBlockedEndpoint = $"{remoteAddress}:{remotePort}";
                state.LastBlockedUtc = DateTime.UtcNow;
            }
            else
            {
                state.LastEndpoint = $"{remoteAddress}:{remotePort}";
                state.LastActivityUtc = DateTime.UtcNow;
            }
        }
    }

    public ProcessActivityInfo GetActivity(string appPath, Verdict verdict = Verdict.Allow)
    {
        var bw = ProcessBandwidthService.Instance.GetBandwidth(appPath);
        double downBps = bw?.DownloadBps ?? 0;
        double upBps = bw?.UploadBps ?? 0;
        long totalIn = bw?.TotalBytesIn ?? 0;
        long totalOut = bw?.TotalBytesOut ?? 0;
        bool hasActiveBandwidth = (downBps + upBps) > 100;

        if (string.IsNullOrWhiteSpace(appPath) || !_activities.TryGetValue(appPath, out var state))
        {
            var level = (verdict == Verdict.Allow && hasActiveBandwidth) ? ProcessActivityLevel.ActiveAllowed : ProcessActivityLevel.Idle;
            return new ProcessActivityInfo(
                level,
                0,
                null,
                null,
                DateTime.MinValue,
                DateTime.MinValue,
                downBps,
                upBps,
                totalIn,
                totalOut);
        }

        lock (state)
        {
            ProcessActivityLevel level;
            if (verdict == Verdict.Block)
            {
                // In Block list: Orange LED if WFP intercepted connection attempts in the last 4 seconds
                bool hasRecentBlocked = (DateTime.UtcNow - state.LastBlockedUtc).TotalSeconds < 4.0;
                level = hasRecentBlocked ? ProcessActivityLevel.BlockedAttempts : ProcessActivityLevel.Idle;
            }
            else
            {
                // In Allow list: Green LED if active established sockets, recent permitted data flow, or active throughput
                bool hasActiveSockets = state.ActiveSockets > 0;
                bool hasRecentAllowed = (DateTime.UtcNow - state.LastActivityUtc).TotalSeconds < 3.5;
                level = (hasActiveSockets || hasRecentAllowed || hasActiveBandwidth) ? ProcessActivityLevel.ActiveAllowed : ProcessActivityLevel.Idle;
            }

            return new ProcessActivityInfo(
                level,
                state.ActiveSockets,
                state.LastEndpoint,
                state.LastBlockedEndpoint,
                state.LastActivityUtc,
                state.LastBlockedUtc,
                downBps,
                upBps,
                totalIn,
                totalOut);
        }
    }

    public string FormatTooltip(string appPath, Verdict verdict = Verdict.Allow)
    {
        var info = GetActivity(appPath, verdict);
        var sb = new System.Text.StringBuilder();

        if (info.Level == ProcessActivityLevel.BlockedAttempts)
        {
            sb.AppendLine(Strings.T("ActivityBlockedStatus"));
            sb.AppendLine(Strings.T("ActivityBlockedDesc"));
            if (info.LastBlockedUtc > DateTime.MinValue)
            {
                var localTime = info.LastBlockedUtc.ToLocalTime().ToString("HH:mm:ss");
                sb.AppendLine(Strings.T("ActivityLastBlockedSeenActive", localTime));
            }
            var endpoint = info.LastBlockedEndpoint ?? info.LastEndpoint;
            if (!string.IsNullOrEmpty(endpoint) && !endpoint.StartsWith("0.0.0.0"))
            {
                sb.Append(Strings.T("ActivityBlockedEndpoint", endpoint));
            }
        }
        else if (info.Level == ProcessActivityLevel.ActiveAllowed)
        {
            sb.AppendLine(Strings.T("ActivityActiveStatus"));
            if (info.ActiveSockets > 0)
            {
                sb.AppendLine(Strings.T("ActivityConnections", info.ActiveSockets));
            }
            if (info.LastActivityUtc > DateTime.MinValue)
            {
                var localTime = info.LastActivityUtc.ToLocalTime().ToString("HH:mm:ss");
                sb.AppendLine(Strings.T("ActivityLastSeenActive", localTime));
            }
            if (!string.IsNullOrEmpty(info.LastEndpoint) && !info.LastEndpoint.StartsWith("0.0.0.0"))
            {
                sb.Append(Strings.T("ActivityLastEndpoint", info.LastEndpoint));
            }
        }
        else
        {
            sb.AppendLine(Strings.T("ActivityIdleStatus"));
            var mostRecent = (verdict == Verdict.Block && info.LastBlockedUtc > DateTime.MinValue)
                ? info.LastBlockedUtc
                : (info.LastActivityUtc > DateTime.MinValue ? info.LastActivityUtc : info.LastBlockedUtc);

            bool wasBlocked = verdict == Verdict.Block && info.LastBlockedUtc > DateTime.MinValue;

            if (mostRecent > DateTime.MinValue)
            {
                var elapsed = DateTime.UtcNow - mostRecent;
                var localDt = mostRecent.ToLocalTime();
                string timeFormatted = localDt.Date == DateTime.Today
                    ? localDt.ToString("HH:mm:ss")
                    : localDt.ToString("yyyy-MM-dd HH:mm");

                string relativeStr;
                if (elapsed.TotalSeconds < 10)
                    relativeStr = Strings.T("TimeJustNow");
                else if (elapsed.TotalSeconds < 60)
                    relativeStr = Strings.T("TimeSecondsAgo", (int)elapsed.TotalSeconds);
                else if (elapsed.TotalMinutes < 60)
                    relativeStr = Strings.T("TimeMinutesAgo", (int)elapsed.TotalMinutes);
                else if (elapsed.TotalHours < 24)
                    relativeStr = Strings.T("TimeHoursAgo", (int)elapsed.TotalHours);
                else
                    relativeStr = Strings.T("TimeDaysAgo", (int)elapsed.TotalDays);

                if (wasBlocked)
                {
                    sb.AppendLine(Strings.T("ActivityLastBlockedRelative", timeFormatted, relativeStr));
                    var ep = info.LastBlockedEndpoint ?? info.LastEndpoint;
                    if (!string.IsNullOrEmpty(ep) && !ep.StartsWith("0.0.0.0"))
                    {
                        sb.Append(Strings.T("ActivityBlockedEndpoint", ep));
                    }
                }
                else
                {
                    sb.AppendLine(Strings.T("ActivityLastSeenRelative", timeFormatted, relativeStr));
                    if (!string.IsNullOrEmpty(info.LastEndpoint) && !info.LastEndpoint.StartsWith("0.0.0.0"))
                    {
                        sb.Append(Strings.T("ActivityLastEndpoint", info.LastEndpoint));
                    }
                }
            }
            else
            {
                sb.Append(Strings.T("ActivityNoHistory"));
            }
        }

        if (info.HasBandwidth)
        {
            sb.AppendLine();
            sb.Append($"{Strings.T("BandwidthSpeed")}: {info.FormattedSpeed}");
        }
        if (info.TotalBytesIn > 0 || info.TotalBytesOut > 0)
        {
            sb.AppendLine();
            sb.Append($"{Strings.T("BandwidthSessionTotal")}: {NetworkSpeedService.FormatBytes(info.TotalBytesIn + info.TotalBytesOut)}");
        }

        return sb.ToString().TrimEnd();
    }

    private void SeedHistoryFromLog()
    {
        try
        {
            var logPath = BlockedLog.LogPath;
            if (!File.Exists(logPath)) return;

            foreach (var line in File.ReadLines(logPath))
            {
                try
                {
                    var parts = line.Split('|', StringSplitOptions.TrimEntries);
                    if (parts.Length < 5) continue;
                    if (!DateTime.TryParse(parts[0], out var dt)) continue;
                    var appPath = parts[3];
                    if (string.IsNullOrWhiteSpace(appPath)) continue;
                    var endpoint = parts[4];
                    if (endpoint.StartsWith("0.0.0.0") || endpoint.StartsWith("::")) continue;

                    var state = _activities.GetOrAdd(appPath, _ => new ProcessActivityState());
                    lock (state)
                    {
                        var utc = dt.ToUniversalTime();
                        if (utc > state.LastBlockedUtc)
                        {
                            state.LastBlockedUtc = utc;
                            state.LastBlockedEndpoint = endpoint;
                        }
                    }
                }
                catch { }
            }
            ActivityUpdated?.Invoke();
        }
        catch { }
    }

    private void Poll()
    {
        try
        {
            // Clean PID cache every 30s
            if ((DateTime.UtcNow - _lastCacheCleanup).TotalSeconds > 30)
            {
                _pidPathCache.Clear();
                _lastCacheCleanup = DateTime.UtcNow;
            }

            var activeByPath = new Dictionary<string, (int Established, string? Endpoint)>(StringComparer.OrdinalIgnoreCase);

            var connections = GetActiveTcpConnections();
            _activePidsSnapshot = connections.Select(c => c.Pid).Distinct().ToArray();
            foreach (var conn in connections)
            {
                if (!_pidPathCache.TryGetValue(conn.Pid, out var path))
                {
                    path = ProcessIdentity.GetImagePath(conn.Pid);
                    if (path != null && HostAppResolver.TryResolveHost(conn.Pid, path, out var hostPath, out _))
                    {
                        path = hostPath;
                    }
                    _pidPathCache[conn.Pid] = path;
                }

                if (string.IsNullOrWhiteSpace(path)) continue;

                if (!activeByPath.TryGetValue(path, out var current))
                {
                    activeByPath[path] = (1, $"{conn.Remote}:{conn.Port}");
                }
                else
                {
                    activeByPath[path] = (
                        current.Established + 1,
                        current.Endpoint ?? $"{conn.Remote}:{conn.Port}");
                }
            }

            var now = DateTime.UtcNow;

            // Update tracked activities
            foreach (var kvp in activeByPath)
            {
                var state = _activities.GetOrAdd(kvp.Key, _ => new ProcessActivityState());
                lock (state)
                {
                    state.ActiveSockets = kvp.Value.Established;
                    state.LastEndpoint = kvp.Value.Endpoint;
                    state.LastActivityUtc = now;
                }
            }

            // Zero out sockets for processes no longer in active table
            foreach (var kvp in _activities)
            {
                if (!activeByPath.ContainsKey(kvp.Key))
                {
                    lock (kvp.Value)
                    {
                        kvp.Value.ActiveSockets = 0;
                    }
                }
            }

            ActivityUpdated?.Invoke();
        }
        catch { }
    }

    private static List<(int Pid, string Remote, int Port)> GetActiveTcpConnections()
    {
        var list = new List<(int, string, int)>();
        CollectTcp4(list);
        CollectTcp6(list);
        return list;
    }

    private static void CollectTcp4(List<(int, string, int)> list)
    {
        nint table = 0;
        try
        {
            int size = 0;
            // Class 5: MIB_TCP_TABLE_OWNER_PID
            GetExtendedTcpTable(nint.Zero, ref size, false, 2, 5, 0);
            if (size <= 0) return;
            table = Marshal.AllocHGlobal(size);
            if (GetExtendedTcpTable(table, ref size, false, 2, 5, 0) != 0) return;
            int count = Marshal.ReadInt32(table);
            nint row = table + 4;
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            for (int i = 0; i < count; i++)
            {
                var r = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(row + i * rowSize);
                // Only count MIB_TCP_STATE_ESTAB (5) with a valid remote IP
                if (r.state == 5 && r.remoteAddr != 0)
                {
                    var ip = new IPAddress(BitConverter.GetBytes(r.remoteAddr));
                    int port = (int)((r.remotePort >> 8) | ((r.remotePort & 0xFF) << 8));
                    list.Add(((int)r.owningPid, ip.ToString(), port));
                }
            }
        }
        catch { }
        finally { if (table != 0) Marshal.FreeHGlobal(table); }
    }

    private static void CollectTcp6(List<(int, string, int)> list)
    {
        nint table = 0;
        try
        {
            int size = 0;
            // Class 5: MIB_TCP6TABLE_OWNER_PID
            GetExtendedTcpTable(nint.Zero, ref size, false, 23, 5, 0);
            if (size <= 0) return;
            table = Marshal.AllocHGlobal(size);
            if (GetExtendedTcpTable(table, ref size, false, 23, 5, 0) != 0) return;
            int count = Marshal.ReadInt32(table);
            nint row = table + 4;
            int rowSize = Marshal.SizeOf<MIB_TCP6ROW_OWNER_PID>();
            for (int i = 0; i < count; i++)
            {
                var r = Marshal.PtrToStructure<MIB_TCP6ROW_OWNER_PID>(row + i * rowSize);
                // Only count MIB_TCP_STATE_ESTAB (5)
                if (r.state == 5 && r.remoteAddr != null && !IsIPv6Zero(r.remoteAddr))
                {
                    var ip = new IPAddress(r.remoteAddr);
                    int port = (int)((r.remotePort >> 8) | ((r.remotePort & 0xFF) << 8));
                    list.Add(((int)r.owningPid, ip.ToString(), port));
                }
            }
        }
        catch { }
        finally { if (table != 0) Marshal.FreeHGlobal(table); }
    }

    private static bool IsIPv6Zero(byte[] bytes)
    {
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] != 0) return false;
        }
        return true;
    }

    [DllImport("iphlpapi.dll")] private static extern uint GetExtendedTcpTable(nint pTable, ref int pdwSize, bool sort, int family, int tableClass, int reserved);
    [StructLayout(LayoutKind.Sequential)] private struct MIB_TCPROW_OWNER_PID { public uint state; public uint localAddr; public uint localPort; public uint remoteAddr; public uint remotePort; public uint owningPid; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCP6ROW_OWNER_PID
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] localAddr;
        public uint localScopeId;
        public uint localPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] remoteAddr;
        public uint remoteScopeId;
        public uint remotePort;
        public uint state;
        public uint owningPid;
    }

    public void Dispose()
    {
        Stop();
    }
}
