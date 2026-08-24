using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using CyberWall.Common.Models;

namespace CyberWall.Service.Engine;

public sealed class ConnectionMonitor : IDisposable
{
    public event Action<ConnectionEvent>? OnNewConnection;
    private Timer? _timer;
    private readonly HashSet<string> _seen = new();
    private readonly HashSet<int> _ownPids;
    private readonly WfpBlockWatcher _blockWatcher = new();
    private FirewallService? _svc;

    public ConnectionMonitor()
    {
        _ownPids = new HashSet<int> { Environment.ProcessId, Process.GetCurrentProcess().Id };
        _blockWatcher.OnBlockedConnection += HandleBlockedConnection;
    }

    public void Start(FirewallService svc)
    {
        _svc = svc;
        _seen.Clear();
        _timer?.Dispose();
        _blockWatcher.Start();
        // Fast 400ms polling for active/established connections from running processes
        _timer = new Timer(_ => Poll(), null, 200, 400);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _blockWatcher.Stop();
    }

    private void HandleBlockedConnection(ConnectionEvent ev)
    {
        if (_svc == null || !_svc.IsMasterOn) return;
        if (_svc.Mode == FirewallMode.BlockAll) return;

        // Skip if already in rule store
        if (_svc.Store.TryGet(ev.AppPath, out _)) return;

        // Skip own processes
        if (_ownPids.Contains(ev.ProcessId)) return;
        if (ev.AppPath.Contains("CyberWall", StringComparison.OrdinalIgnoreCase)) return;

        OnNewConnection?.Invoke(ev);
    }

    private void Poll()
    {
        if (_svc == null || !_svc.IsMasterOn) return;
        try
        {
            var conns = GetTcpConnections();
            foreach (var c in conns)
            {
                string key = $"{c.Pid}:{c.Remote}:{c.Port}";
                if (!_seen.Add(key)) continue;
                string? path = GetPath(c.Pid);
                if (path == null) continue;
                if (_ownPids.Contains(c.Pid)) continue;
                if (path.Contains("CyberWall", StringComparison.OrdinalIgnoreCase)) continue;
                if (_svc.Store.TryGet(path, out _)) continue;

                var ev = new ConnectionEvent
                {
                    AppPath = path,
                    RemoteAddress = c.Remote,
                    RemotePort = c.Port,
                    Direction = Direction.Outbound,
                    ProcessId = c.Pid,
                    Protocol = "TCP"
                };

                if (_svc.Mode == FirewallMode.BlockAll) continue;
                OnNewConnection?.Invoke(ev);
            }
            if (_seen.Count > 5000) _seen.Clear();
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    private static string? GetPath(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.MainModule?.FileName; } catch { return null; }
    }

    private static List<(int Pid, string Remote, int Port)> GetTcpConnections()
    {
        var list = new List<(int, string, int)>();
        nint table = 0;
        try
        {
            int size = 0;
            GetExtendedTcpTable(nint.Zero, ref size, false, 2, 5, 0);
            table = Marshal.AllocHGlobal(size);
            if (GetExtendedTcpTable(table, ref size, false, 2, 5, 0) != 0) return list;
            int count = Marshal.ReadInt32(table);
            nint row = table + 4;
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            for (int i = 0; i < count; i++)
            {
                var r = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(row + i * rowSize);
                if (r.state is 2 or 5)
                {
                    var ip = new IPAddress(BitConverter.GetBytes(r.remoteAddr));
                    int port = (int)((r.remotePort >> 8) | ((r.remotePort & 0xFF) << 8));
                    list.Add(((int)r.owningPid, ip.ToString(), port));
                }
            }
        }
        finally { if (table != 0) Marshal.FreeHGlobal(table); }
        return list;
    }

    [DllImport("iphlpapi.dll")] static extern uint GetExtendedTcpTable(nint pTable, ref int pdwSize, bool sort, int family, int tableClass, int reserved);
    [StructLayout(LayoutKind.Sequential)] struct MIB_TCPROW_OWNER_PID { public uint state; public uint localAddr; public uint localPort; public uint remoteAddr; public uint remotePort; public uint owningPid; }

    public void Dispose()
    {
        Stop();
        _blockWatcher.Dispose();
    }
}

