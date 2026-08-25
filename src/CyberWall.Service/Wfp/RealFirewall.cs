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
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            var ok = RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,blockoutbound");
            EnsureSelfAllowed();
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

    public static bool Disable()
    {
        if (!IsAdmin) return false;
        try
        {
            RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound");
            return true;
        }
        catch { return false; }
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
            RunNetsh($"advfirewall firewall delete rule name=\"CyberWall-Block-{baseName}\"");
            RunNetsh($"advfirewall firewall delete rule name=\"CyberWall-Block-{baseName}-in\"");
            var name = $"CyberWall-Allow-{baseName}";
            RunNetsh($"advfirewall firewall delete rule name=\"{name}\"");
            RunNetsh($"advfirewall firewall delete rule name=\"{name}-in\"");
            RunNetsh($"advfirewall firewall add rule name=\"{name}\" dir=out action=allow program=\"{appPath}\" enable=yes profile=any");
            RunNetsh($"advfirewall firewall add rule name=\"{name}-in\" dir=in action=allow program=\"{appPath}\" enable=yes profile=any");
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
        HostAppResolver.TerminateHelpers(appPath);
        ProcessIdentity.TerminateTcpConnections(pid, appPath);
        if (!string.IsNullOrEmpty(pfn)) ProcessIdentity.SuspendProcess(pid);
    }

    /// <summary>
    /// Pending block while the user is asked. Store apps are keyed by AppContainer SID;
    /// Windows' auto-allow rule must be disabled or traffic keeps flowing, including
    /// connections that were already established before the popup.
    /// </summary>
    public static void HoldApp(string appPath, int pid = 0)
    {
        if (!IsAdmin) return;
        ProcessIdentity.TryGetPackageFamilyName(pid, appPath, out var pfn);
        ProcessIdentity.TryGetPackageSid(pfn, out var sid);
        DisableMatchingAllowRules(sid, pfn);
        if (!string.IsNullOrEmpty(pfn)) ApplyPackageBlock(pfn);
        ApplySingleBlock(appPath);
        HostAppResolver.TerminateHelpers(appPath);
        ProcessIdentity.TerminateTcpConnections(pid, appPath);
        if (!string.IsNullOrEmpty(pfn)) ProcessIdentity.SuspendProcess(pid);
    }

    private static void ApplySingleBlock(string appPath)
    {
        try
        {
            var name = $"CyberWall-Allow-{Path.GetFileNameWithoutExtension(appPath)}";
            RunNetsh($"advfirewall firewall delete rule name=\"{name}\"");
            RunNetsh($"advfirewall firewall delete rule name=\"{name}-in\"");
            var bname = $"CyberWall-Block-{Path.GetFileNameWithoutExtension(appPath)}";
            RunNetsh($"advfirewall firewall delete rule name=\"{bname}\"");
            RunNetsh($"advfirewall firewall delete rule name=\"{bname}-in\"");
            RunNetsh($"advfirewall firewall add rule name=\"{bname}\" dir=out action=block program=\"{appPath}\" enable=yes profile=any");
            RunNetsh($"advfirewall firewall add rule name=\"{bname}-in\" dir=in action=block program=\"{appPath}\" enable=yes profile=any");
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
                RunNetsh($"advfirewall firewall delete rule name=\"CyberWall-Allow-{baseName}\"");
                RunNetsh($"advfirewall firewall delete rule name=\"CyberWall-Allow-{baseName}-in\"");
                RunNetsh($"advfirewall firewall delete rule name=\"CyberWall-Block-{baseName}\"");
                RunNetsh($"advfirewall firewall delete rule name=\"CyberWall-Block-{baseName}-in\"");
            }
            catch { }
        }
        if (!string.IsNullOrEmpty(pfn)) RemovePackageRules(pfn);
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

