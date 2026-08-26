using System.Diagnostics;
using CyberWall.Common.Models;
using CyberWall.Service.Rules;

namespace CyberWall.Service.Wfp;

public sealed class WfpEngine : IDisposable
{
    private nint _engine;
    private bool _enabled;
    private bool _realBlock;

    public bool IsEnabled => _enabled;
    public bool IsRealBlock => _realBlock;

    public bool TryEnable()
    {
        if (_enabled) return true;
        try
        {
            var session = new WfpInterop.FWPM_SESSION0
            {
                sessionKey = Guid.NewGuid(),
                displayData = new() { name = "CyberWall", description = "CyberWall WFP session" },
                flags = 1,
            };
            var r = WfpInterop.FwpmEngineOpen0(null, 0, 0, in session, out _engine);
            if (r != 0) Debug.WriteLine($"FwpmEngineOpen0 0x{r:X} fallback netsh");
            else
            {
                r = WfpInterop.FwpmTransactionBegin0(_engine, 0);
                if (r == 0) WfpInterop.FwpmTransactionCommit0(_engine);
            }
            _realBlock = RealFirewall.TryEnableBlockAll();
            _enabled = true;
            return true;
        }
        catch (Exception ex) { Debug.WriteLine(ex); _realBlock = RealFirewall.TryEnableBlockAll(); _enabled = true; return true; }
    }

    public void Disable()
    {
        if (!_enabled) return;
        try { if (_engine != 0) WfpInterop.FwpmEngineClose0(_engine); } catch { }
        _engine = 0;
        _enabled = false;
        if (_realBlock) { RealFirewall.Disable(); _realBlock = false; }
    }

    public void AllowApp(string path, int pid = 0) { if (_enabled) RealFirewall.AllowApp(path, pid); }
    public void BlockApp(string path, int pid = 0) { if (_enabled) RealFirewall.BlockApp(path, pid); }
    public void HoldApp(string path, int pid = 0) { if (_enabled) RealFirewall.HoldApp(path, pid); }
    public void RemoveApp(string path, int pid = 0) => RealFirewall.RemoveApp(path, pid);
    public void SetKillswitch(bool enable) { if (_enabled) RealFirewall.SetKillswitch(enable); }

    public Verdict Classify(string appPath, Direction dir, RuleStore store)
    {
        if (!_enabled) return Verdict.Allow;
        if (store.TryGet(appPath, out var rule)) return rule.Verdict;
        return Verdict.Ask;
    }

    public void Dispose() => Disable();
}
