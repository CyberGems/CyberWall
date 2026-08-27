using System.Diagnostics;
using System.IO;
using CyberWall.Common;
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
            EnsureCoreServicesAllowed();
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
        }
        catch { }
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
        if (!IsAdmin) return;
        ProcessIdentity.TryGetPackageFamilyName(pid, appPath, out var pfn);
        ProcessIdentity.TryGetPackageSid(pfn, out var sid);
        var paths = GetCompanionBinaries(appPath);
        foreach (var p in paths)
        {
            ApplySingleAllow(p);
        }
        if (!string.IsNullOrEmpty(pfn)) ApplyPackageAllow(pfn);
        EnableMatchingAllowRules(sid, pfn);
        ProcessIdentity.ResumeProcesses(appPath, pid);
    }

    private static void ApplySingleAllow(string appPath)
    {
        try
        {
            var baseName = Path.GetFileNameWithoutExtension(appPath);
            var allowOut = $"CyberWall-Allow-{baseName}";
            var allowIn = $"CyberWall-Allow-{baseName}-in";
            var blockOut = $"CyberWall-Block-{baseName}";
            var blockIn = $"CyberWall-Block-{baseName}-in";

            RemoveRule(blockOut);
            RemoveRule(blockIn);
            RemoveRule(allowOut);
            RemoveRule(allowIn);

            AddAppRule(allowOut, appPath, outbound: true, allow: true);
            AddAppRule(allowIn, appPath, outbound: false, allow: true);
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    public static void BlockApp(string appPath, int pid = 0)
    {
        if (!IsAdmin) return;
        ProcessIdentity.TryGetPackageFamilyName(pid, appPath, out var pfn);
        ProcessIdentity.TryGetPackageSid(pfn, out var sid);
        var paths = GetCompanionBinaries(appPath);
        foreach (var p in paths)
        {
            ApplySingleBlock(p);
        }
        if (!string.IsNullOrEmpty(pfn)) ApplyPackageBlock(pfn);
        DisableMatchingAllowRules(sid, pfn);
        StopLiveTraffic(appPath, pid, pfn);
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
        ProcessIdentity.TryGetPackageSid(pfn, out var sid);
        DisableMatchingAllowRules(sid, pfn);
        if (!string.IsNullOrEmpty(pfn))
            ApplyPackageBlock(pfn);
        ApplySingleBlock(appPath);
        StopLiveTraffic(appPath, pid, pfn);
    }

    /// <summary>
    /// Store apps ignore a pending/block rule on sockets they already have open.
    /// Win32 apps crash if those sockets (especially localhost) are aborted.
    /// </summary>
    private static void StopLiveTraffic(string appPath, int pid, string? pfn)
    {
        if (string.IsNullOrEmpty(pfn)) return;
        HostAppResolver.TerminateHelpers(appPath);
        ProcessIdentity.TerminateTcpConnections(pid, appPath);
        ProcessIdentity.SuspendProcess(pid);
    }

    private static void ApplySingleBlock(string appPath)
    {
        try
        {
            var baseName = Path.GetFileNameWithoutExtension(appPath);
            var allowOut = $"CyberWall-Allow-{baseName}";
            var allowIn = $"CyberWall-Allow-{baseName}-in";
            var blockOut = $"CyberWall-Block-{baseName}";
            var blockIn = $"CyberWall-Block-{baseName}-in";

            RemoveRule(allowOut);
            RemoveRule(allowIn);
            RemoveRule(blockOut);
            RemoveRule(blockIn);

            AddAppRule(blockOut, appPath, outbound: true, allow: false);
            AddAppRule(blockIn, appPath, outbound: false, allow: false);
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
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
                return;
            }
        }
        catch { }
        RunNetsh($"advfirewall firewall delete rule name=\"{name}\"");
    }

    private static void AddAppRule(string name, string appPath, bool outbound, bool allow)
    {
        try
        {
            var tRule = Type.GetTypeFromProgID("HNetCfg.FWRule");
            if (tRule != null && GetFwPolicy() is { } policy && Activator.CreateInstance(tRule) is { } rule)
            {
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

        return result.ToList();
    }

    private static void ApplyPackageAllow(string pfn)
    {
        var allow = PackageRuleName("Allow", pfn);
        var block = PackageRuleName("Block", pfn);
        RunNetsh($"advfirewall firewall delete rule name=\"{block}\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{block}-in\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{allow}\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{allow}-in\"");
        var id = PackageIdForRule(pfn);
        AddPackageRule(allow, id, outbound: true, allow: true);
        AddPackageRule(allow + "-in", id, outbound: false, allow: true);
    }

    private static void ApplyPackageBlock(string pfn)
    {
        var allow = PackageRuleName("Allow", pfn);
        var block = PackageRuleName("Block", pfn);
        RunNetsh($"advfirewall firewall delete rule name=\"{allow}\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{allow}-in\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{block}\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{block}-in\"");
        var id = PackageIdForRule(pfn);
        AddPackageRule(block, id, outbound: true, allow: false);
        AddPackageRule(block + "-in", id, outbound: false, allow: false);
    }

    private static string PackageIdForRule(string pfn)
        => ProcessIdentity.TryGetPackageSid(pfn, out var sid) ? sid : pfn;

    private static void DisableMatchingAllowRules(string? sid, string? pfn)
        => SetMatchingAllowRulesEnabled(sid, pfn, enable: false);

    private static void EnableMatchingAllowRules(string? sid, string? pfn)
        => SetMatchingAllowRulesEnabled(sid, pfn, enable: true);

    private static void SetMatchingAllowRulesEnabled(string? sid, string? pfn, bool enable)
    {
        if (string.IsNullOrEmpty(sid) && string.IsNullOrEmpty(pfn)) return;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\FirewallRules");
            if (key == null) return;

            foreach (var valueName in key.GetValueNames())
            {
                if (key.GetValue(valueName) is not string text) continue;
                if (text.IndexOf("Action=Allow", StringComparison.OrdinalIgnoreCase) < 0) continue;
                bool match =
                    (!string.IsNullOrEmpty(sid) && text.Contains(sid, StringComparison.OrdinalIgnoreCase)) ||
                    (!string.IsNullOrEmpty(pfn) && text.Contains(pfn, StringComparison.OrdinalIgnoreCase));
                if (!match) continue;

                var displayName = ExtractPipeField(text, "Name=");
                if (string.IsNullOrEmpty(displayName)) continue;
                if (displayName.StartsWith("CyberWall-", StringComparison.OrdinalIgnoreCase)) continue;

                var flag = enable ? "yes" : "no";
                RunNetsh($"advfirewall firewall set rule name=\"{displayName}\" new enable={flag}");
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    private static string? ExtractPipeField(string text, string prefix)
    {
        var idx = text.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + prefix.Length;
        var end = text.IndexOf('|', start);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static void RemovePackageRules(string pfn)
    {
        var allow = PackageRuleName("Allow", pfn);
        var block = PackageRuleName("Block", pfn);
        RunNetsh($"advfirewall firewall delete rule name=\"{allow}\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{allow}-in\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{block}\"");
        RunNetsh($"advfirewall firewall delete rule name=\"{block}-in\"");
    }

    private static string PackageRuleName(string kind, string pfn) => $"CyberWall-{kind}-Pkg-{pfn}";

    private static bool AddPackageRule(string name, string packageId, bool outbound, bool allow)
    {
        var dir = outbound ? "out" : "in";
        var action = allow ? "allow" : "block";
        if (RunNetsh($"advfirewall firewall add rule name=\"{name}\" dir={dir} action={action} package=\"{packageId}\" enable=yes profile=any"))
            return true;

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

