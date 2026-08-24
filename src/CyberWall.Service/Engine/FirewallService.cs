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

    public FirewallService()
    {
        Monitor.OnNewConnection += ev => OnAskConnection?.Invoke(ev);
    }

    public bool Enable(FirewallMode mode = FirewallMode.Ask)
    {
        Mode = mode == FirewallMode.Disabled ? FirewallMode.Ask : mode;
        var ok = Wfp.TryEnable();
        if (ok) Monitor.Start(this);
        return ok;
    }

    public void Disable() => Wfp.Disable();
    public bool IsMasterOn => IsEnabled;

    public void SetMode(FirewallMode mode)
    {
        if (mode == FirewallMode.Disabled) { Disable(); return; }
        Mode = mode;
        if (!IsEnabled) { Wfp.TryEnable(); Monitor.Start(this); }
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
        if (!permanent) return;
        if (verdict == Verdict.Allow) { Store.Upsert(new AppRule { AppPath = appPath, Verdict = verdict }); Wfp.AllowApp(appPath); }
        else if (verdict == Verdict.Block) { Store.Upsert(new AppRule { AppPath = appPath, Verdict = verdict }); Wfp.BlockApp(appPath); }
    }

    public void RemoveRule(string appPath) { Store.Remove(appPath); Wfp.RemoveApp(appPath); }

    public void Dispose() { Monitor.Dispose(); Wfp.Dispose(); }
}
