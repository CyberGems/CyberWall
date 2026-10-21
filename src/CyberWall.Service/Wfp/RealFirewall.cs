using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using CyberWall.Common;
using CyberWall.Common.Models;
using Microsoft.Win32;

namespace CyberWall.Service.Wfp;

public static class RealFirewall
{
    // HNetCfg.FwPolicy2 enums (NET_FW_ACTION_TYPE / NET_FW_PROFILE_TYPE2).
    private const int ActionAllow = 1;
    private const int ActionBlock = 2;
    private const int ProfileDomain = 1;
    private const int ProfilePrivate = 2;
    private const int ProfilePublic = 4;

    private static readonly int[] ManagedProfiles = { ProfileDomain, ProfilePrivate, ProfilePublic };

    /// <summary>Prefix owned by CyberWall for every Windows Firewall rule it creates.</summary>
    public const string RulePrefix = "CyberWall-";

    /// <summary>Marker that separates the stable path hash from the executable name in rule names.</summary>
    private const char RuleTagSeparator = '~';

    /// <summary>
    /// Marker of the rules that keep the core services working (DNS, DHCP, NTP,
    /// loopback, Defender, SmartScreen and the WebView2 runtime).
    /// </summary>
    private const string CoreRuleMarker = "-Core-";

    /// <summary>
    /// Inbound action captured before CyberWall switched the machine to default-deny,
    /// so the original configuration can be restored. The outbound action is not
    /// captured on purpose: "block everything" is the state CyberWall applies itself,
    /// so it can never be treated as the user's own configuration.
    /// </summary>
    private static readonly Dictionary<int, int> _originalInboundActions = new();

    public static bool IsAdmin => new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

    /// <summary>
    /// Stable per-path tag. Rule names must be unique per executable, otherwise two
    /// different programs with the same file name (chrome.exe, setup.exe, host.exe...)
    /// overwrite each other's Windows Firewall rule.
    /// </summary>
    internal static string RuleTag(string appPath)
    {
        try
        {
            var normalized = AppRule.Normalize(appPath);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
        }
        catch
        {
            return "00000000";
        }
    }

    private static string RuleName(string kind, string appPath, bool inbound)
    {
        var file = Path.GetFileNameWithoutExtension(appPath);
        var name = $"{RulePrefix}{kind}-{file}{RuleTagSeparator}{RuleTag(appPath)}";
        return inbound ? name + "-in" : name;
    }

    public static bool TryEnableBlockAll()
    {
        if (!IsAdmin) return false;

        var policy = GetFwPolicy();
        if (policy != null)
        {
            CaptureOriginalActions(policy);
            ApplyProfileActions(policy, ActionBlock, ActionBlock);
        }

        var ok = RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,blockoutbound");
        if (!ok && policy == null)
        {
            Debug.WriteLine("RealFirewall: unable to switch the firewall policy to default-deny.");
            return false;
        }

        // Legacy per-application rules are dropped first so they cannot keep blocking
        // programs after the naming scheme changed.
        RemoveLegacyRules();
        EnsureSelfAllowed();
        EnsureCoreServicesAllowed();
        return true;
    }

    private static void CaptureOriginalActions(dynamic policy)
    {
        try
        {
            lock (_originalInboundActions)
            {
                if (_originalInboundActions.Count > 0) return;
                foreach (var profile in ManagedProfiles)
                {
                    var inbound = Convert.ToInt32(policy.DefaultInboundAction[profile]);
                    _originalInboundActions[profile] = inbound;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RealFirewall: could not capture the original firewall policy: {ex.Message}");
        }
    }

    /// <summary>
    /// Actions to restore for a firewall profile.
    ///
    /// The outbound action is always "allow". The captured value cannot be trusted:
    /// when CyberWall did not shut down cleanly the machine was still in the
    /// default-deny state it had applied itself, and restoring that would leave the
    /// host without connectivity as soon as the application rules are removed.
    /// </summary>
    internal static (int Outbound, int Inbound) ResolveRestoredActions(int? capturedOutbound, int? capturedInbound)
        => (ActionAllow, capturedInbound ?? ActionBlock);

    private static void ApplyProfileActions(dynamic policy, int outbound, int inbound)
    {
        foreach (var profile in ManagedProfiles)
        {
            try { policy.DefaultOutboundAction[profile] = outbound; } catch { }
            try { policy.DefaultInboundAction[profile] = inbound; } catch { }
        }
    }

    /// <summary>
    /// Restores the firewall policy captured on enable and verifies the result.
    /// Returns false when the machine could not be restored, which leaves the host
    /// without outbound connectivity and must be surfaced to the user.
    /// </summary>
    public static bool Disable()
    {
        if (!IsAdmin) return false;

        var restored = false;
        try
        {
            RemoveRule("CyberWall-Killswitch-BlockAll-Out");
            RemoveRule("CyberWall-Killswitch-BlockAll-In");

            var policy = GetFwPolicy();
            if (policy != null)
            {
                lock (_originalInboundActions)
                {
                    foreach (var profile in ManagedProfiles)
                    {
                        _originalInboundActions.TryGetValue(profile, out var capturedInbound);
                        var actions = ResolveRestoredActions(null, capturedInbound);
                        try { policy.DefaultOutboundAction[profile] = actions.Outbound; } catch { }
                        try { policy.DefaultInboundAction[profile] = actions.Inbound; } catch { }
                    }
                    _originalInboundActions.Clear();
                }
            }

            restored = RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound");
            if (!restored)
            {
                Debug.WriteLine("RealFirewall: netsh failed to restore the firewall policy.");
            }
            else if (!IsOutboundAllowed(policy))
            {
                Debug.WriteLine("RealFirewall: the firewall policy still blocks outbound traffic after restore.");
                restored = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"RealFirewall: error while disabling: {ex.Message}");
            restored = false;
        }

        if (restored)
        {
            RemoveOwnedRules();
        }
        else
        {
            // The policy could not be restored, so the allow rules are kept: without
            // them the machine would be left with no outbound connectivity at all.
            Debug.WriteLine("RealFirewall: policy not restored, keeping the application rules.");
        }
        return restored;
    }

    /// <summary>
    /// Verifies that the outbound action is really "allow". The check reads the policy
    /// through the firewall API instead of parsing the netsh output, which is localized
    /// on non-English Windows installations.
    /// </summary>
    private static bool IsOutboundAllowed(dynamic? policy)
    {
        if (policy != null)
        {
            try
            {
                foreach (var profile in ManagedProfiles)
                {
                    if (Convert.ToInt32(policy.DefaultOutboundAction[profile]) != ActionAllow)
                        return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"RealFirewall: could not read back the firewall policy: {ex.Message}");
            }
        }

        var output = RunNetshCapture("advfirewall show allprofiles firewallpolicy");
        return !string.IsNullOrWhiteSpace(output)
            && output.Contains("AllowOutbound", StringComparison.OrdinalIgnoreCase);
    }

    public static void EnsureSelfAllowed()
    {
        if (!IsAdmin) return;
        try
        {
            var selfExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(selfExe) && File.Exists(selfExe))
            {
                ApplySingleAllow(selfExe);
            }

            var dir = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                foreach (var exe in Directory.GetFiles(dir, "CyberWall*.exe"))
                {
                    ApplySingleAllow(exe);
                }
            }
        }
        catch { }
    }

    public static void EnsureCoreServicesAllowed()
    {
        if (!IsAdmin) return;
        try
        {
            AddFwPortRule("CyberWall-Allow-Core-DNS-UDP", 53, isUdp: true, outbound: true);
            AddFwPortRule("CyberWall-Allow-Core-DNS-TCP", 53, isUdp: false, outbound: true);
            AddFwPortRule("CyberWall-Allow-Core-DHCP-Out", 67, isUdp: true, outbound: true);
            AddFwPortRule("CyberWall-Allow-Core-DHCP-Client-Out", 68, isUdp: true, outbound: true);
            AddFwPortRule("CyberWall-Allow-Core-NTP-Out", 123, isUdp: true, outbound: true);

            // Clean up legacy rule names if any exist
            RemoveRule("CyberWall-Allow-Core-Loopback-Out");
            RemoveRule("CyberWall-Allow-Core-Loopback-In");

            // IPv4 Loopback (127.0.0.0/8 covers all local IPC communication)
            AddFwIpRule("CyberWall-Allow-Core-Loopback4-Out", "127.0.0.0/8", outbound: true);
            AddFwIpRule("CyberWall-Allow-Core-Loopback4-In", "127.0.0.0/8", outbound: false);

            // IPv6 Loopback (::1)
            AddFwIpRule("CyberWall-Allow-Core-Loopback6-Out", "::1", outbound: true);
            AddFwIpRule("CyberWall-Allow-Core-Loopback6-In", "::1", outbound: false);

            // Ensure Windows security, Defender antimalware and SmartScreen are allowed
            EnsureDefenderServicesAllowed();

            // Ensure Microsoft Edge WebView2 runtimes (WhatsApp Desktop, Teams, Office, etc.) are allowed
            EnsureWebView2Allowed();
        }
        catch { }
    }

    public static List<string> GetInstalledWebView2Paths()
    {
        var results = new List<string>();
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        foreach (var pf in new[] { programFilesX86, programFiles })
        {
            if (string.IsNullOrEmpty(pf)) continue;
            var edgeApp = Path.Combine(pf, "Microsoft", "EdgeWebView", "Application");
            if (!Directory.Exists(edgeApp)) continue;

            try
            {
                foreach (var dir in Directory.GetDirectories(edgeApp))
                {
                    var exe = Path.Combine(dir, "msedgewebview2.exe");
                    if (File.Exists(exe))
                    {
                        results.Add(exe);
                    }
                }
            }
            catch { }
        }
        return results;
    }

    private static void EnsureWebView2Allowed()
    {
        try
        {
            foreach (var exe in GetInstalledWebView2Paths())
            {
                ApplySingleAllow(exe);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private static void EnsureDefenderServicesAllowed()
    {
        try
        {
            var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var smartScreen = Path.Combine(systemDir, "smartscreen.exe");
            if (File.Exists(smartScreen))
            {
                ApplySingleAllow(smartScreen);
            }

            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var defenderPlatform = Path.Combine(programData, "Microsoft", "Windows Defender", "Platform");
            if (Directory.Exists(defenderPlatform))
            {
                var latestVerDir = Directory.GetDirectories(defenderPlatform)
                    .OrderByDescending(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (latestVerDir != null)
                {
                    foreach (var exeName in new[] { "MpDefenderCoreService.exe", "MsMpEng.exe", "NisSrv.exe", "MpCmdRun.exe" })
                    {
                        var exePath = Path.Combine(latestVerDir, exeName);
                        if (File.Exists(exePath))
                        {
                            ApplySingleAllow(exePath);
                        }
                    }
                }
            }

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var winDef = Path.Combine(programFiles, "Windows Defender", "MsMpEng.exe");
            if (File.Exists(winDef))
            {
                ApplySingleAllow(winDef);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public static void SetKillswitch(bool enable)
    {
        if (!IsAdmin) return;
        try
        {
            if (enable)
            {
                RemoveRule("CyberWall-Killswitch-BlockAll-Out");
                RemoveRule("CyberWall-Killswitch-BlockAll-In");
                AddGlobalBlockRule("CyberWall-Killswitch-BlockAll-Out", outbound: true);
                AddGlobalBlockRule("CyberWall-Killswitch-BlockAll-In", outbound: false);
                ProcessIdentity.TerminateAllNonSelfConnections();
            }
            else
            {
                RemoveRule("CyberWall-Killswitch-BlockAll-Out");
                RemoveRule("CyberWall-Killswitch-BlockAll-In");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    public static void AllowApp(string appPath, int pid = 0)
    {
        ApplyAppRule(appPath, Verdict.Allow, Verdict.Allow, pid);
    }

    public static void BlockApp(string appPath, int pid = 0)
    {
        ApplyAppRule(appPath, Verdict.Block, Verdict.Block, pid);
    }

    public static void ApplyAppRule(string appPath, Verdict inVerdict, Verdict outVerdict, int pid = 0)
    {
        if (!IsAdmin) return;
        ProcessIdentity.TryGetPackageFamilyName(pid, appPath, out var pfn);
        var paths = GetCompanionBinaries(appPath);
        foreach (var p in paths)
        {
            ApplySingleCustomRule(p, inVerdict, outVerdict);
        }
        if (!string.IsNullOrEmpty(pfn)) ApplyPackageRuleWithDirection(pfn, inVerdict, outVerdict);

        if (inVerdict != Verdict.Allow && outVerdict != Verdict.Allow)
        {
            StopLiveTraffic(appPath, pid, pfn);
        }
    }

    private static void ApplySingleAllow(string appPath)
    {
        ApplySingleCustomRule(appPath, Verdict.Allow, Verdict.Allow);
    }

    private static void ApplySingleBlock(string appPath)
    {
        ApplySingleCustomRule(appPath, Verdict.Block, Verdict.Block);
    }

    private static void ApplySingleCustomRule(string appPath, Verdict inVerdict, Verdict outVerdict)
    {
        try
        {
            var allowOut = RuleName("Allow", appPath, inbound: false);
            var allowIn = RuleName("Allow", appPath, inbound: true);
            var blockOut = RuleName("Block", appPath, inbound: false);
            var blockIn = RuleName("Block", appPath, inbound: true);

            if (outVerdict == Verdict.Allow)
            {
                RemoveRule(blockOut);
                AddAppRule(allowOut, appPath, outbound: true, allow: true);
            }
            else
            {
                RemoveRule(allowOut);
                AddAppRule(blockOut, appPath, outbound: true, allow: false);
            }

            if (inVerdict == Verdict.Allow)
            {
                RemoveRule(blockIn);
                AddAppRule(allowIn, appPath, outbound: false, allow: true);
            }
            else
            {
                RemoveRule(allowIn);
                AddAppRule(blockIn, appPath, outbound: false, allow: false);
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    private static void ApplyPackageRuleWithDirection(string pfn, Verdict inVerdict, Verdict outVerdict)
    {
        var allow = PackageRuleName("Allow", pfn);
        var block = PackageRuleName("Block", pfn);
        if (!ProcessIdentity.TryGetPackageSid(pfn, out var sid, out var userSid) || string.IsNullOrEmpty(sid))
        {
            // AppContainer rules only apply to apps with an AppContainer SID (S-1-15-2-...).
            // Centennial Win32 packages (like WhatsApp Desktop) run full-trust and are governed by Win32 application path rules.
            return;
        }

        if (outVerdict == Verdict.Allow)
        {
            RemoveRule(block);
            AddPackageRule(allow, sid, userSid, outbound: true, allow: true);
        }
        else
        {
            RemoveRule(allow);
            AddPackageRule(block, sid, userSid, outbound: true, allow: false);
        }

        if (inVerdict == Verdict.Allow)
        {
            RemoveRule(block + "-in");
            AddPackageRule(allow + "-in", sid, userSid, outbound: false, allow: true);
        }
        else
        {
            RemoveRule(allow + "-in");
            AddPackageRule(block + "-in", sid, userSid, outbound: false, allow: false);
        }
    }

    /// <summary>
    /// Pending block while the user is asked. Store apps keep leaking on
    /// already-established sockets, so those still get their TCP torn down.
    /// Win32 apps must not — aborting their sockets (including localhost)
    /// crashes tools that are waiting on the permission popup.
    /// </summary>
    public static void HoldApp(string appPath, int pid = 0)
    {
        if (!IsAdmin) return;
        ProcessIdentity.TryGetPackageFamilyName(pid, appPath, out var pfn);
        if (!string.IsNullOrEmpty(pfn))
            ApplyPackageBlock(pfn);
        ApplySingleBlock(appPath);
    }

    /// <summary>
    /// Terminate helper processes (e.g. WebView2/Edge helpers) for blocked apps.
    /// </summary>
    private static void StopLiveTraffic(string appPath, int pid, string? pfn)
    {
        if (string.IsNullOrEmpty(pfn)) return;
        HostAppResolver.TerminateHelpers(appPath);
    }

    public static void RemoveApp(string appPath, int pid = 0)
    {
        ProcessIdentity.TryGetPackageFamilyName(pid, appPath, out var pfn);
        var paths = GetCompanionBinaries(appPath);
        foreach (var p in paths)
        {
            try
            {
                RemoveRule(RuleName("Allow", p, inbound: false));
                RemoveRule(RuleName("Allow", p, inbound: true));
                RemoveRule(RuleName("Block", p, inbound: false));
                RemoveRule(RuleName("Block", p, inbound: true));
            }
            catch { }
        }
        if (!string.IsNullOrEmpty(pfn)) RemovePackageRules(pfn);
    }

    /// <summary>
    /// Removes every Windows Firewall rule owned by CyberWall. Used when protection is
    /// turned off and on uninstall, so blocked programs are never left behind.
    ///
    /// The core service rules are left in place: they restore the local traffic
    /// (127.0.0.1) the applications rely on, and removing them breaks applications
    /// that talk to localhost even though the machine is back to its normal
    /// configuration. The recovery script keeps them for the same reason.
    /// </summary>
    public static int RemoveOwnedRules()
    {
        var removed = 0;
        try
        {
            var policy = GetFwPolicy();
            if (policy != null)
            {
                var names = new List<string>();
                foreach (dynamic rule in policy.Rules)
                {
                    try
                    {
                        var name = (string?)rule.Name;
                        if (IsRemovableOwnedRule(name))
                            names.Add(name!);
                    }
                    catch { }
                }
                foreach (var name in names)
                {
                    try { policy.Rules.Remove(name); removed++; } catch { }
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }

        if (removed == 0)
        {
            removed = RemoveOwnedRulesViaPowerShell();
        }
        return removed;
    }

    /// <summary>
    /// Tells whether a Windows Firewall rule created by CyberWall can be removed
    /// when the protection is turned off.
    ///
    /// The core service rules are preserved. They were added to fix the network
    /// interruption of the applications that talk to localhost, and Windows
    /// Firewall needs an explicit allow rule for that traffic on Windows 11.
    /// </summary>
    internal static bool IsRemovableOwnedRule(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (!name.StartsWith(RulePrefix, StringComparison.OrdinalIgnoreCase)) return false;
        return !name.Contains(CoreRuleMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static int RemoveOwnedRulesViaPowerShell()
    {
        var output = RunPowerShellCapture(
            "$r = Get-NetFirewallRule -DisplayName 'CyberWall-*' -ErrorAction SilentlyContinue | " +
            $"Where-Object {{ $_.DisplayName -notlike '*{CoreRuleMarker}*' }}; " +
            "if ($r) { $n = @($r).Count; $r | Remove-NetFirewallRule -ErrorAction SilentlyContinue; $n } else { 0 }");
        return int.TryParse(output?.Trim(), out var count) ? count : 0;
    }

    /// <summary>
    /// Removes the per-application rules created by previous versions, which used the bare
    /// executable name and therefore collided between programs sharing the same file name.
    /// </summary>
    private static void RemoveLegacyRules()
    {
        try
        {
            var policy = GetFwPolicy();
            if (policy == null) return;
            var names = new List<string>();
            foreach (dynamic rule in policy.Rules)
            {
                try
                {
                    var name = (string?)rule.Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!name.StartsWith(RulePrefix + "Allow-", StringComparison.OrdinalIgnoreCase) &&
                        !name.StartsWith(RulePrefix + "Block-", StringComparison.OrdinalIgnoreCase)) continue;
                    if (name.Contains(RuleTagSeparator)) continue;            // current naming scheme
                    if (name.Contains("-Pkg-", StringComparison.OrdinalIgnoreCase)) continue; // package rules
                    if (name.Contains("-Core-", StringComparison.OrdinalIgnoreCase)) continue; // core service rules
                    names.Add(name);
                }
                catch { }
            }
            foreach (var name in names)
            {
                try { policy.Rules.Remove(name); } catch { }
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    private static dynamic? GetFwPolicy()
    {
        try
        {
            var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2");
            return type != null ? Activator.CreateInstance(type) : null;
        }
        catch
        {
            return null;
        }
    }

    private static void RemoveRule(string name)
    {
        try
        {
            dynamic? policy = GetFwPolicy();
            if (policy != null)
            {
                policy.Rules.Remove(name);
            }
        }
        catch { }
    }

    private static void AddAppRule(string name, string appPath, bool outbound, bool allow)
    {
        try
        {
            var tRule = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (tRule != null && GetFwPolicy() is { } policy && Activator.CreateInstance(tRule) is { } rule)
            {
                try { policy.Rules.Remove(name); } catch { }
                ((dynamic)rule).Name = name;
                ((dynamic)rule).ApplicationName = appPath;
                ((dynamic)rule).Action = allow ? 1 : 0; // 1 = Allow, 0 = Block
                ((dynamic)rule).Direction = outbound ? 2 : 1; // 2 = Out, 1 = In
                ((dynamic)rule).Profiles = 0x7FFFFFFF;
                ((dynamic)rule).Enabled = true;
                ((dynamic)policy).Rules.Add(rule);
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    private static void AddGlobalBlockRule(string name, bool outbound)
    {
        RemoveRule(name);
        try
        {
            var tRule = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (tRule != null && GetFwPolicy() is { } policy && Activator.CreateInstance(tRule) is { } rule)
            {
                ((dynamic)rule).Name = name;
                ((dynamic)rule).Action = 0; // Block
                ((dynamic)rule).Direction = outbound ? 2 : 1; // 2 = Out, 1 = In
                ((dynamic)rule).Profiles = 0x7FFFFFFF;
                ((dynamic)rule).Enabled = true;
                ((dynamic)policy).Rules.Add(rule);
                return;
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }

        var dir = outbound ? "out" : "in";
        RunNetsh($"advfirewall firewall add rule name=\"{name}\" dir={dir} action=block enable=yes profile=any");
    }

    private static void AddFwIpRule(string name, string ipAddresses, bool outbound)
    {
        RemoveRule(name);
        try
        {
            var tRule = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (tRule != null && GetFwPolicy() is { } policy && Activator.CreateInstance(tRule) is { } rule)
            {
                ((dynamic)rule).Name = name;
                ((dynamic)rule).RemoteAddresses = ipAddresses;
                ((dynamic)rule).Action = 1; // Allow
                ((dynamic)rule).Direction = outbound ? 2 : 1; // 2 = Out, 1 = In
                ((dynamic)rule).Profiles = 0x7FFFFFFF;
                ((dynamic)rule).Enabled = true;
                ((dynamic)policy).Rules.Add(rule);
                return;
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }

        var dir = outbound ? "out" : "in";
        if (RunNetsh($"advfirewall firewall add rule name=\"{name}\" dir={dir} action=allow remoteip=\"{ipAddresses}\" enable=yes profile=any"))
            return;

        var psDir = outbound ? "Outbound" : "Inbound";
        RunPowerShell($"New-NetFirewallRule -DisplayName '{EscapePs(name)}' -Direction {psDir} -Action Allow -RemoteAddress '{EscapePs(ipAddresses)}' -Profile Any -ErrorAction SilentlyContinue | Out-Null");
    }

    private static void AddFwPortRule(string name, int port, bool isUdp, bool outbound)
    {
        RemoveRule(name);
        try
        {
            var tRule = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (tRule != null && GetFwPolicy() is { } policy && Activator.CreateInstance(tRule) is { } rule)
            {
                ((dynamic)rule).Name = name;
                ((dynamic)rule).Protocol = isUdp ? 17 : 6; // 17 = UDP, 6 = TCP
                ((dynamic)rule).RemotePorts = port.ToString();
                ((dynamic)rule).Action = 1; // Allow
                ((dynamic)rule).Direction = outbound ? 2 : 1; // 2 = Out, 1 = In
                ((dynamic)rule).Profiles = 0x7FFFFFFF;
                ((dynamic)rule).Enabled = true;
                ((dynamic)policy).Rules.Add(rule);
                return;
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }

        var proto = isUdp ? "UDP" : "TCP";
        var dir = outbound ? "out" : "in";
        RunNetsh($"advfirewall firewall add rule name=\"{name}\" dir={dir} action=allow protocol={proto} remoteport={port} enable=yes profile=any");
    }

    private static List<string> GetCompanionBinaries(string appPath)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { appPath };
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(appPath);
            bool isGit = fileName.StartsWith("git", StringComparison.OrdinalIgnoreCase) ||
                         appPath.Contains(@"\Git\", StringComparison.OrdinalIgnoreCase) ||
                         appPath.Contains(@"/Git/", StringComparison.OrdinalIgnoreCase);

            if (isGit)
            {
                // Find Git root directory
                var dir = Path.GetDirectoryName(appPath);
                var cur = dir != null ? new DirectoryInfo(dir) : null;
                DirectoryInfo? gitRoot = null;

                while (cur != null && cur.Parent != null)
                {
                    if (cur.Name.Equals("Git", StringComparison.OrdinalIgnoreCase) ||
                        Directory.Exists(Path.Combine(cur.FullName, "cmd")) && Directory.Exists(Path.Combine(cur.FullName, "mingw64")) ||
                        Directory.Exists(Path.Combine(cur.FullName, "libexec", "git-core")))
                    {
                        gitRoot = cur;
                        break;
                    }
                    cur = cur.Parent;
                }

                if (gitRoot != null)
                {
                    var candidates = new[]
                    {
                        Path.Combine(gitRoot.FullName, "cmd", "git.exe"),
                        Path.Combine(gitRoot.FullName, "bin", "git.exe"),
                        Path.Combine(gitRoot.FullName, "mingw64", "bin", "git.exe"),
                        Path.Combine(gitRoot.FullName, "mingw64", "bin", "curl.exe"),
                        Path.Combine(gitRoot.FullName, "mingw64", "libexec", "git-core", "git-remote-https.exe"),
                        Path.Combine(gitRoot.FullName, "mingw64", "libexec", "git-core", "git-remote-http.exe"),
                        Path.Combine(gitRoot.FullName, "mingw64", "libexec", "git-core", "git-remote-ftp.exe"),
                        Path.Combine(gitRoot.FullName, "mingw64", "libexec", "git-core", "git-remote-ftps.exe"),
                        Path.Combine(gitRoot.FullName, "usr", "bin", "ssh.exe"),
                        Path.Combine(gitRoot.FullName, "usr", "bin", "curl.exe"),
                    };

                    foreach (var c in candidates)
                    {
                        if (File.Exists(c)) result.Add(c);
                    }
                }
                else if (dir != null)
                {
                    // Fallback: search sibling executables in same directory
                    var gitCoreSiblings = new[] { "git.exe", "git-remote-https.exe", "git-remote-http.exe", "ssh.exe" };
                    foreach (var s in gitCoreSiblings)
                    {
                        var candidate = Path.Combine(dir, s);
                        if (File.Exists(candidate)) result.Add(candidate);
                    }
                }
            }
            // If app is WhatsApp or uses WebView2, add installed WebView2 runtime paths
            bool isWhatsApp = fileName.Equals("WhatsApp.Root", StringComparison.OrdinalIgnoreCase) ||
                              fileName.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase) ||
                              appPath.IndexOf("WhatsApp", StringComparison.OrdinalIgnoreCase) >= 0;

            if (isWhatsApp)
            {
                foreach (var wv in GetInstalledWebView2Paths())
                {
                    result.Add(wv);
                }
            }
        }
        catch { }

        if (PackagePath.TryGetPackageDir(appPath, out var packageDir) && Directory.Exists(packageDir))
        {
            try
            {
                foreach (var exe in Directory.GetFiles(packageDir, "*.exe", SearchOption.TopDirectoryOnly))
                    result.Add(exe);
            }
            catch { }
        }

        if (appPath.IndexOf(@"\Microsoft\Windows Defender\Platform\", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            try
            {
                var dir = Path.GetDirectoryName(appPath);
                if (dir != null && Directory.Exists(dir))
                {
                    foreach (var exe in Directory.GetFiles(dir, "*.exe"))
                        result.Add(exe);
                }
            }
            catch { }
        }

        return result.ToList();
    }

    private static void ApplyPackageBlock(string pfn)
    {
        var allow = PackageRuleName("Allow", pfn);
        var block = PackageRuleName("Block", pfn);
        RemoveRule(allow);
        RemoveRule(allow + "-in");
        if (ProcessIdentity.TryGetPackageSid(pfn, out var sid, out var userSid) && !string.IsNullOrEmpty(sid))
        {
            AddPackageRule(block, sid, userSid, outbound: true, allow: false);
            AddPackageRule(block + "-in", sid, userSid, outbound: false, allow: false);
        }
    }

    private static void RemovePackageRules(string pfn)
    {
        var allow = PackageRuleName("Allow", pfn);
        var block = PackageRuleName("Block", pfn);
        RemoveRule(allow);
        RemoveRule(allow + "-in");
        RemoveRule(block);
        RemoveRule(block + "-in");
    }

    private static string PackageRuleName(string kind, string pfn) => $"CyberWall-{kind}-Pkg-{pfn}";

    private static bool AddPackageRule(string name, string packageId, string? userOwnerSid, bool outbound, bool allow)
    {
        if (string.IsNullOrWhiteSpace(packageId) || !packageId.StartsWith("S-1-15-2-", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var tRule = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (tRule != null && GetFwPolicy() is { } policy && Activator.CreateInstance(tRule) is { } rule)
            {
                try { policy.Rules.Remove(name); } catch { }
                ((dynamic)rule).Name = name;
                ((dynamic)rule).LocalAppPackageId = packageId;
                if (!string.IsNullOrEmpty(userOwnerSid))
                {
                    try { ((dynamic)rule).LocalUserOwner = userOwnerSid; } catch { }
                }
                ((dynamic)rule).Action = allow ? 1 : 0; // 1 = Allow, 0 = Block
                ((dynamic)rule).Direction = outbound ? 2 : 1; // 2 = Out, 1 = In
                ((dynamic)rule).Profiles = 0x7FFFFFFF;
                ((dynamic)rule).Enabled = true;
                ((dynamic)policy).Rules.Add(rule);
                return true;
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }

        return false;
    }

    private static string EscapePs(string value) => value.Replace("'", "''");

    private static bool RunPowerShell(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(command);
            using var p = Process.Start(psi);
            if (p == null) return false;
            if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } return false; }
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static bool RunNetsh(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return false; }
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static string? RunNetshCapture(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return null; }
            return output;
        }
        catch { return null; }
    }

    private static string? RunPowerShellCapture(string command)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(command);
            using var p = Process.Start(psi);
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } return null; }
            return output;
        }
        catch { return null; }
    }
}

