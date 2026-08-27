using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Service.Engine;
using CyberWall.Service.Wfp;

namespace CyberWall.UI.Services;

public sealed record ProcessActivityInfo(
    bool IsActive,
    int ActiveSockets,
    string? LastEndpoint,
    DateTime LastActivityUtc
);

public sealed class ProcessTrafficTracker : IDisposable
{
    private static readonly Lazy<ProcessTrafficTracker> _instance = new(() => new ProcessTrafficTracker());
    public static ProcessTrafficTracker Instance => _instance.Value;

    private readonly DispatcherTimer _timer;
    private readonly ConcurrentDictionary<string, ProcessActivityState> _activities = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, string?> _pidPathCache = new();
    private DateTime _lastCacheCleanup = DateTime.UtcNow;
    private bool _seeded;

    public event Action? ActivityUpdated;

    private sealed class ProcessActivityState
    {
        public int ActiveSockets { get; set; }
        public string? LastEndpoint { get; set; }
        public DateTime LastActivityUtc { get; set; } = DateTime.MinValue;
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

        if (!_timer.IsEnabled)
        {
            Poll();
            _timer.Start();
        }
    }

    public void Stop()
    {
        _timer.Stop();
    }

    public void RecordActivity(string appPath, string remoteAddress, int remotePort)
    {
        if (string.IsNullOrWhiteSpace(appPath)) return;
        var state = _activities.GetOrAdd(appPath, _ => new ProcessActivityState());
        lock (state)
        {
            state.LastEndpoint = $"{remoteAddress}:{remotePort}";
            state.LastActivityUtc = DateTime.UtcNow;
        }
    }

    public ProcessActivityInfo GetActivity(string appPath)
    {
        if (string.IsNullOrWhiteSpace(appPath) || !_activities.TryGetValue(appPath, out var state))
        {
            return new ProcessActivityInfo(false, 0, null, DateTime.MinValue);
        }

        lock (state)
        {
            bool isActive = state.ActiveSockets > 0 || (DateTime.UtcNow - state.LastActivityUtc).TotalSeconds < 3.5;
            return new ProcessActivityInfo(isActive, state.ActiveSockets, state.LastEndpoint, state.LastActivityUtc);
        }
    }

    public string FormatTooltip(string appPath)
    {
        var info = GetActivity(appPath);
        var sb = new System.Text.StringBuilder();

        if (info.IsActive)
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
            if (!string.IsNullOrEmpty(info.LastEndpoint))
            {
                sb.Append(Strings.T("ActivityLastEndpoint", info.LastEndpoint));
            }
        }
        else
        {
            sb.AppendLine(Strings.T("ActivityIdleStatus"));
            if (info.LastActivityUtc > DateTime.MinValue)
            {
                var elapsed = DateTime.UtcNow - info.LastActivityUtc;
                var localDt = info.LastActivityUtc.ToLocalTime();
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

                sb.AppendLine(Strings.T("ActivityLastSeenRelative", timeFormatted, relativeStr));

                if (!string.IsNullOrEmpty(info.LastEndpoint))
                {
                    sb.Append(Strings.T("ActivityLastEndpoint", info.LastEndpoint));
                }
            }
            else
            {
                sb.Append(Strings.T("ActivityNoHistory"));
            }
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

                    var state = _activities.GetOrAdd(appPath, _ => new ProcessActivityState());
                    lock (state)
                    {
                        var utc = dt.ToUniversalTime();
                        if (utc > state.LastActivityUtc)
                        {
                            state.LastActivityUtc = utc;
                            state.LastEndpoint = parts[4];
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

            var activeByPath = new Dictionary<string, (int Sockets, string? Endpoint)>(StringComparer.OrdinalIgnoreCase);

            var connections = GetActiveTcpConnections();
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
                    activeByPath[path] = (current.Sockets + 1, current.Endpoint ?? $"{conn.Remote}:{conn.Port}");
                }
            }

            var now = DateTime.UtcNow;

            // Update tracked activities
            foreach (var kvp in activeByPath)
            {
                var state = _activities.GetOrAdd(kvp.Key, _ => new ProcessActivityState());
                lock (state)
                {
                    state.ActiveSockets = kvp.Value.Sockets;
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
                if (r.state is 2 or 5) // SYN_SENT (2) or ESTABLISHED (5)
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
                if (r.remoteAddr == null || r.state is not (2 or 5)) continue;
                var ip = new IPAddress(r.remoteAddr);
                int port = (int)((r.remotePort >> 8) | ((r.remotePort & 0xFF) << 8));
                list.Add(((int)r.owningPid, ip.ToString(), port));
            }
        }
        catch { }
        finally { if (table != 0) Marshal.FreeHGlobal(table); }
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
