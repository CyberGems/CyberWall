using CyberWall.Common;
using CyberWall.Common.Models;
using CyberWall.Service.Rules;
using CyberWall.Service.Wfp;

namespace CyberWall.Service.Engine;

public sealed class FirewallService : IDisposable
{
    public readonly WfpEngine Wfp = new();
    public readonly RuleStore Store = new();
    public readonly ConnectionMonitor Monitor = new();
    public FirewallMode Mode { get; private set; } = FirewallMode.Ask;
    public bool IsEnabled => Wfp.IsEnabled;
    public event Action<ConnectionEvent>? OnAskConnection;
    public event Action<ConnectionEvent>? OnUnknownBlocked;
    private readonly HashSet<string> _pendingHolds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _pendingLock = new();
    private readonly Dictionary<string, (Verdict Verdict, int Pid, DateTime ExpiresUtc)> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sessionLock = new();
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(10);

    public FirewallService()
    {
        Monitor.OnNewConnection += ev => OnAskConnection?.Invoke(ev);
    }

    public bool Enable(FirewallMode mode = FirewallMode.Ask)
    {
        Mode = mode == FirewallMode.Disabled ? FirewallMode.Ask : mode;
        var ok = Wfp.TryEnable();
        if (ok)
        {
            RealFirewall.EnsureSelfAllowed();
            Monitor.Start(this);
        }
        return ok;
    }

    public void Disable()
    {
        Monitor.Stop();
        Wfp.Disable();
    }
    public bool IsMasterOn => IsEnabled;

    public void SetMode(FirewallMode mode)
    {
        if (mode == FirewallMode.Disabled) { Disable(); return; }
        Mode = mode;
        if (!IsEnabled) { Wfp.TryEnable(); Monitor.Start(this); }
    }

    public void NotifyUnknownBlocked(ConnectionEvent ev) => OnUnknownBlocked?.Invoke(ev);

    public void HoldPending(ConnectionEvent ev)
    {
        if (!IsEnabled) return;
        var key = SafeKey(ev.AppPath);
        bool first;
        lock (_pendingLock) first = _pendingHolds.Add(key);
        try
        {
            if (first) Wfp.HoldApp(ev.AppPath, ev.ProcessId);
            else ProcessIdentity.TerminateTcpConnections(ev.ProcessId, ev.AppPath);
        }
        catch
        {
            if (first)
            {
                lock (_pendingLock) _pendingHolds.Remove(key);
            }
        }
    }

    public void ReenforceBlock(string appPath, int pid)
    {
        if (!IsEnabled) return;
        HostAppResolver.TerminateHelpers(appPath);
        ProcessIdentity.TerminateTcpConnections(pid, appPath);
        if (PackagePath.TryGetFamilyName(appPath, out _))
            ProcessIdentity.SuspendProcess(pid);
    }

    public Verdict Decide(ConnectionEvent ev)
    {
        if (!IsEnabled) return Verdict.Allow;
        var v = Wfp.Classify(ev.AppPath, ev.Direction, Store);
        if (v != Verdict.Ask) return v;
        if (TryGetSession(ev.AppPath, ev.ProcessId, out var session)) return session;
        return Mode == FirewallMode.Ask ? Verdict.Ask : Verdict.Block;
    }

    public bool TryGetSession(string appPath, int pid, out Verdict verdict)
    {
        var key = SafeKey(appPath);
        lock (_sessionLock)
        {
            if (!_sessions.TryGetValue(key, out var s))
            {
                verdict = default;
                return false;
            }
            if (DateTime.UtcNow > s.ExpiresUtc)
            {
                _sessions.Remove(key);
                verdict = default;
                return false;
            }
            if (s.Pid != 0 && pid != 0 && s.Pid != pid)
            {
                _sessions.Remove(key);
                verdict = default;
                return false;
            }
            verdict = s.Verdict;
            return true;
        }
    }

    public void SetVerdict(string appPath, Verdict verdict, bool permanent, ConnectionEvent? ev = null)
    {
        if (ev != null) BlockedLog.Append(ev, verdict);
        var key = SafeKey(appPath);
        lock (_pendingLock) _pendingHolds.Remove(key);
        var pid = ev?.ProcessId ?? 0;
        if (verdict == Verdict.Allow) Wfp.AllowApp(appPath, pid);
        else if (verdict == Verdict.Block) Wfp.BlockApp(appPath, pid);
        if (!permanent)
        {
            lock (_sessionLock)
                _sessions[key] = (verdict, pid, DateTime.UtcNow.Add(SessionTtl));
            return;
        }
        lock (_sessionLock) _sessions.Remove(key);
        ProcessIdentity.TryGetPackageFamilyName(pid, appPath, out var pfn);
        Store.Upsert(new AppRule
        {
            AppPath = appPath,
            Verdict = verdict,
            PackageFamilyName = string.IsNullOrEmpty(pfn) ? null : pfn
        });
    }

    public void RemoveRule(string appPath)
    {
        var key = SafeKey(appPath);
        lock (_pendingLock) _pendingHolds.Remove(key);
        lock (_sessionLock) _sessions.Remove(key);
        Store.Remove(appPath);
        Wfp.RemoveApp(appPath);
    }

    private static string SafeKey(string appPath)
    {
        try { return AppRule.Normalize(appPath); }
        catch { return appPath; }
    }

    public void Dispose() { Monitor.Dispose(); Wfp.Dispose(); }
}
