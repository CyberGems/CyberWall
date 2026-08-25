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
    private readonly HashSet<string> _pendingHolds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _pendingLock = new();

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
        return Mode == FirewallMode.Ask ? Verdict.Ask : Verdict.Block;
    }

    public void SetVerdict(string appPath, Verdict verdict, bool permanent, ConnectionEvent? ev = null)
    {
        if (ev != null) BlockedLog.Append(ev, verdict);
        lock (_pendingLock) _pendingHolds.Remove(SafeKey(appPath));
        var pid = ev?.ProcessId ?? 0;
        if (verdict == Verdict.Allow) Wfp.AllowApp(appPath, pid);
        else if (verdict == Verdict.Block) Wfp.BlockApp(appPath, pid);
        if (!permanent) return;
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
        lock (_pendingLock) _pendingHolds.Remove(SafeKey(appPath));
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
