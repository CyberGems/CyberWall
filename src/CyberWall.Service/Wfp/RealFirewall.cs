using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

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

    public static void AllowApp(string appPath)
    {
        if (!IsAdmin) return;
        var paths = GetCompanionBinaries(appPath);
        foreach (var p in paths)
        {
            ApplySingleAllow(p);
        }
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

    public static void BlockApp(string appPath)
    {
        if (!IsAdmin) return;
        var paths = GetCompanionBinaries(appPath);
        foreach (var p in paths)
        {
            ApplySingleBlock(p);
        }
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

    public static void RemoveApp(string appPath)
    {
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
        return result.ToList();
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

