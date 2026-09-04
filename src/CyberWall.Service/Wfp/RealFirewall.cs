using System.Diagnostics;
using System.IO;
using CyberWall.Common;
using CyberWall.Common.Models;
using Microsoft.Win32;

namespace CyberWall.Service.Wfp;

public static class RealFirewall
{
    public static bool IsAdmin => new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent()).IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

    public static bool TryEnableBlockAll()
    {
        if (!IsAdmin) return false;
        try
        {
            dynamic policy = Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2")!)!;
            policy.DefaultOutboundAction[2] = 0;
            policy.DefaultOutboundAction[4] = 0;
            policy.DefaultOutboundAction[1] = 0;
            policy.DefaultInboundAction[2] = 0;
            policy.DefaultInboundAction[4] = 0;
            policy.DefaultInboundAction[1] = 0;
            RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,blockoutbound");
            EnsureSelfAllowed();
            EnsureCoreServicesAllowed();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            var ok = RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,blockoutbound");
            EnsureSelfAllowed();
            EnsureCoreServicesAllowed();
            return ok;
        }
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
        }
        catch { }
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

    public static bool Disable()
    {
        if (!IsAdmin) return false;
        try
        {
            RunNetsh("advfirewall firewall delete rule name=\"CyberWall-Killswitch-BlockAll-Out\"");
            RunNetsh("advfirewall firewall delete rule name=\"CyberWall-Killswitch-BlockAll-In\"");
            RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound");
            return true;
        }
        catch { return false; }
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

        if (inVerdict == Verdict.Allow || outVerdict == Verdict.Allow)
        {
            ProcessIdentity.ResumeProcesses(appPath, pid);
        }
        else
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
            var baseName = Path.GetFileNameWithoutExtension(appPath);
            var allowOut = $"CyberWall-Allow-{baseName}";
            var allowIn = $"CyberWall-Allow-{baseName}-in";
            var blockOut = $"CyberWall-Block-{baseName}";
            var blockIn = $"CyberWall-Block-{baseName}-in";

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
        ProcessIdentity.TryGetPackageSid(pfn, out var sid, out var userSid);
        var id = !string.IsNullOrEmpty(sid) ? sid : pfn;

        if (outVerdict == Verdict.Allow)
        {
            RemoveRule(block);
            AddPackageRule(allow, id, userSid, outbound: true, allow: true);
        }
        else
        {
            RemoveRule(allow);
            AddPackageRule(block, id, userSid, outbound: true, allow: false);
        }

        if (inVerdict == Verdict.Allow)
        {
            RemoveRule(block + "-in");
            AddPackageRule(allow + "-in", id, userSid, outbound: false, allow: true);
        }
        else
        {
            RemoveRule(allow + "-in");
            AddPackageRule(block + "-in", id, userSid, outbound: false, allow: false);
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
                var baseName = Path.GetFileNameWithoutExtension(p);
                RemoveRule($"CyberWall-Allow-{baseName}");
                RemoveRule($"CyberWall-Allow-{baseName}-in");
                RemoveRule($"CyberWall-Block-{baseName}");
                RemoveRule($"CyberWall-Block-{baseName}-in");
            }
            catch { }
        }
        if (!string.IsNullOrEmpty(pfn)) RemovePackageRules(pfn);
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
                return;
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }

        var dir = outbound ? "out" : "in";
        var act = allow ? "allow" : "block";
        RunNetsh($"advfirewall firewall add rule name=\"{name}\" dir={dir} action={act} program=\"{appPath}\" enable=yes profile=any");
    }

    private static void AddGlobalBlockRule(string name, bool outbound)
    {
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
        }
        catch { }

        if (PackagePath.TryGetPackageDir(appPath, out var packageDir) && Directory.Exists(packageDir))
        {
            try
            {
                foreach (var exe in Directory.GetFiles(packageDir, "*.exe", SearchOption.AllDirectories))
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
        ProcessIdentity.TryGetPackageSid(pfn, out var sid, out var userSid);
        var id = !string.IsNullOrEmpty(sid) ? sid : pfn;
        AddPackageRule(block, id, userSid, outbound: true, allow: false);
        AddPackageRule(block + "-in", id, userSid, outbound: false, allow: false);
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

        var psDir = outbound ? "Outbound" : "Inbound";
        var psAction = allow ? "Allow" : "Block";
        return RunPowerShell($"New-NetFirewallRule -DisplayName '{EscapePs(name)}' -Direction {psDir} -Action {psAction} -Package '{EscapePs(packageId)}' -Profile Any -ErrorAction SilentlyContinue | Out-Null");
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
            p.WaitForExit(15000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static bool RunNetsh(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            p.WaitForExit(4000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}

