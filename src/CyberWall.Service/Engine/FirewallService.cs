using System.Diagnostics;
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
    public event Action<ConnectionEvent>? OnBlockedActivity;
    public event Action<ConnectionEvent>? OnAllowedActivity;
    private readonly HashSet<string> _pendingHolds = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _pendingLock = new();
    private readonly Dictionary<string, (Verdict Verdict, int Pid, DateTime ExpiresUtc)> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sessionLock = new();
    private readonly HashSet<string> _reappliedAllows = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(10);
    private Timer? _heartbeat;
    private bool _ownsEngine;

    /// <summary>
    /// True when another host (the Windows service) owns the engine, so this instance
    /// must not touch the firewall.
    /// </summary>
    public bool OwnedByAnotherHost { get; private set; }

    public FirewallService()
    {
        Monitor.OnNewConnection += ev => OnAskConnection?.Invoke(ev);
    }

    public bool Enable(FirewallMode mode = FirewallMode.Ask)
    {
        if (!EngineOwnership.TryAcquire())
        {
            OwnedByAnotherHost = true;
            return false;
        }

        OwnedByAnotherHost = false;
        _ownsEngine = true;

        Mode = mode == FirewallMode.Disabled ? FirewallMode.Ask : mode;
        var ok = Wfp.TryEnable();
        if (ok)
        {
            lock (_sessionLock) _reappliedAllows.Clear();
            RealFirewall.EnsureSelfAllowed();
            BlockedLog.TryDeleteTempFile();
            StartHeartbeat();

            if (Mode == FirewallMode.Killswitch)
            {
                Wfp.SetKillswitch(true);
            }
            else
            {
                // Preload all saved allow rules into memory synchronously in microseconds (< 0.1ms)
                // so running applications are immediately recognized as allowed by ConnectionMonitor
                var rulesToSync = new List<AppRule>();
                lock (_sessionLock)
                {
                    _reappliedAllows.Clear();
                    foreach (var rule in Store.All)
                    {
                        if (rule.EffectiveOutboundVerdict == Verdict.Allow || rule.EffectiveInboundVerdict == Verdict.Allow)
                        {
                            var key = SafeKey(rule.AppPath);
                            _reappliedAllows.Add(key);
                            rulesToSync.Add(rule);
                        }
                    }
                }

                // Verify and synchronize Windows Firewall rules asynchronously in background
                // so MainWindow renders instantly (< 100ms)
                _ = Task.Run(() =>
                {
                    foreach (var rule in rulesToSync)
                    {
                        try
                        {
                            Wfp.ApplyAppRule(rule.AppPath, rule.EffectiveInboundVerdict, rule.EffectiveOutboundVerdict);
                        }
                        catch { }
                    }
                });
            }
            Monitor.Start(this);
        }
        return ok;
    }

    /// <summary>
    /// Turns the engine off. Returns false when the firewall policy could not be
    /// restored, which leaves the host without outbound connectivity.
    /// </summary>
    public bool Disable()
    {
        // Only the owner of the engine may restore the firewall policy. Another host
        // (the Windows service) is in charge of it in that case.
        if (!_ownsEngine) return true;
        _ownsEngine = false;

        if (Mode == FirewallMode.Killswitch)
        {
            Wfp.SetKillswitch(false);
        }
        Monitor.Stop();
        var restored = Wfp.Disable();

        if (restored)
        {
            // The machine is back to a usable state, the watchdog is not needed anymore.
            StopHeartbeat();
        }
        else
        {
            // The policy could not be restored. The watchdog stays armed so it can
            // restore it a few minutes later, and the heartbeat stops so the task
            // does not consider the engine alive.
            StopHeartbeatTimer();
            Debug.WriteLine("FirewallService: the firewall policy was not restored, the recovery task stays armed.");
        }

        EngineOwnership.Release();
        return restored;
    }
    public bool IsMasterOn => IsEnabled;

    /// <summary>
    /// Reapplies the allow rules of the store to Windows Firewall. Used by the Windows
    /// service to pick up the decisions taken in the desktop application.
    /// </summary>
    public void ReapplyAllowRules()
    {
        if (!_ownsEngine) return;
        foreach (var rule in Store.All)
        {
            if (rule.EffectiveOutboundVerdict != Verdict.Allow && rule.EffectiveInboundVerdict != Verdict.Allow)
                continue;
            try
            {
                Wfp.ApplyAppRule(rule.AppPath, rule.EffectiveInboundVerdict, rule.EffectiveOutboundVerdict);
            }
            catch { }
        }
    }

    /// <summary>
    /// Keeps the recovery watchdog informed that an engine is alive, and keeps the
    /// scheduled task armed so a crash cannot leave the host without connectivity.
    /// </summary>
    private void StartHeartbeat()
    {
        StopHeartbeat();
        try
        {
            PolicyRecovery.Arm();
            PolicyRecovery.WriteHeartbeat();
            _heartbeat = new Timer(_ =>
            {
                try { PolicyRecovery.WriteHeartbeat(); } catch { }
            }, null, 30_000, 30_000);
        }
        catch { }
    }

    private void StopHeartbeat()
    {
        StopHeartbeatTimer();
        try
        {
            PolicyRecovery.ClearHeartbeat();
            PolicyRecovery.Disarm();
        }
        catch { }
    }

    /// <summary>Stops the heartbeat timer without disarming the recovery task.</summary>
    private void StopHeartbeatTimer()
    {
        try { _heartbeat?.Dispose(); } catch { }
        _heartbeat = null;
    }

    public void SetMode(FirewallMode mode)
    {
        if (mode == FirewallMode.Disabled) { Disable(); return; }
        var oldMode = Mode;
        Mode = mode;
        if (!IsEnabled)
        {
            Wfp.TryEnable();
            Monitor.Start(this);
        }

        if (Mode == FirewallMode.Killswitch)
        {
            lock (_sessionLock) _reappliedAllows.Clear();
            Wfp.SetKillswitch(true);
        }
        else if (oldMode == FirewallMode.Killswitch)
        {
            Wfp.SetKillswitch(false);
        }
    }

    public void NotifyUnknownBlocked(ConnectionEvent ev) => OnUnknownBlocked?.Invoke(ev);

    public void RecordBlockedActivity(ConnectionEvent ev)
    {
        BlockedLog.Append(ev, Verdict.Block);
        OnBlockedActivity?.Invoke(ev);
    }

    public void RecordAllowedActivity(ConnectionEvent ev)
    {
        BlockedLog.Append(ev, Verdict.Allow);
        OnAllowedActivity?.Invoke(ev);
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
        }
        catch
        {
            if (first)
            {
                lock (_pendingLock) _pendingHolds.Remove(key);
            }
        }
    }

    public void ReenforceAllow(string appPath, int pid)
    {
        if (!IsEnabled || Mode == FirewallMode.Killswitch) return;
        var key = SafeKey(appPath);
        bool first;
        lock (_sessionLock) first = _reappliedAllows.Add(key);
        if (first) Wfp.AllowApp(appPath, pid);
    }

    public void ReenforceBlock(string appPath, int pid)
    {
        if (!IsEnabled) return;
        ProcessIdentity.TryGetPackageFamilyName(pid, appPath, out var pfn);
        if (string.IsNullOrEmpty(pfn) && !PackagePath.TryGetFamilyName(appPath, out pfn))
            return;
        HostAppResolver.TerminateHelpers(appPath);
        ProcessIdentity.TerminateTcpConnections(pid, appPath);
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
        if (verdict == Verdict.Allow)
        {
            lock (_sessionLock) _reappliedAllows.Add(key);
        }
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
            Direction = Direction.Both,
            InboundVerdict = verdict,
            OutboundVerdict = verdict,
            PackageFamilyName = string.IsNullOrEmpty(pfn) ? null : pfn
        });
    }

    public void UpdateRule(string appPath, Verdict inVerdict, Verdict outVerdict, int pid = 0)
    {
        var key = SafeKey(appPath);
        lock (_pendingLock) _pendingHolds.Remove(key);
        lock (_sessionLock) _sessions.Remove(key);

        if (inVerdict == Verdict.Allow || outVerdict == Verdict.Allow)
        {
            lock (_sessionLock) _reappliedAllows.Add(key);
        }
        else
        {
            lock (_sessionLock) _reappliedAllows.Remove(key);
        }

        Wfp.ApplyAppRule(appPath, inVerdict, outVerdict, pid);

        ProcessIdentity.TryGetPackageFamilyName(pid, appPath, out var pfn);
        var overallVerdict = (inVerdict == Verdict.Allow || outVerdict == Verdict.Allow) ? Verdict.Allow : Verdict.Block;
        var direction = (inVerdict == Verdict.Allow && outVerdict == Verdict.Allow) || (inVerdict == Verdict.Block && outVerdict == Verdict.Block)
            ? Direction.Both
            : (inVerdict == Verdict.Allow ? Direction.Inbound : Direction.Outbound);

        Store.Upsert(new AppRule
        {
            AppPath = appPath,
            Verdict = overallVerdict,
            Direction = direction,
            InboundVerdict = inVerdict,
            OutboundVerdict = outVerdict,
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

    public void ClearAllRules()
    {
        var all = Store.All.ToList();
        foreach (var r in all)
        {
            RemoveRule(r.AppPath);
        }
    }

    private static string SafeKey(string appPath)
    {
        try { return AppRule.Normalize(appPath); }
        catch { return appPath; }
    }

    public void Dispose()
    {
        Disable();
        Monitor.Dispose();
        Wfp.Dispose();
    }
}
