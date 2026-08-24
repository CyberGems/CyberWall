using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CyberWall.Service.Wfp;

internal static class RealFirewall
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
            return true;
        }
        catch (Exception ex) { Debug.WriteLine(ex); return RunNetsh("advfirewall set allprofiles firewallpolicy blockinbound,blockoutbound"); }
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
        try
        {
            var name = $"CyberWall-Allow-{Path.GetFileNameWithoutExtension(appPath)}";
            RunNetsh($"advfirewall firewall delete rule name=\"{name}\"");
            RunNetsh($"advfirewall firewall add rule name=\"{name}\" dir=out action=allow program=\"{appPath}\" enable=yes profile=any");
            RunNetsh($"advfirewall firewall add rule name=\"{name}-in\" dir=in action=allow program=\"{appPath}\" enable=yes profile=any");
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    public static void BlockApp(string appPath)
    {
        if (!IsAdmin) return;
        try
        {
            var name = $"CyberWall-Allow-{Path.GetFileNameWithoutExtension(appPath)}";
            RunNetsh($"advfirewall firewall delete rule name=\"{name}\"");
            RunNetsh($"advfirewall firewall delete rule name=\"{name}-in\"");
            var bname = $"CyberWall-Block-{Path.GetFileNameWithoutExtension(appPath)}";
            RunNetsh($"advfirewall firewall delete rule name=\"{bname}\"");
            RunNetsh($"advfirewall firewall add rule name=\"{bname}\" dir=out action=block program=\"{appPath}\" enable=yes profile=any");
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    public static void RemoveApp(string appPath)
    {
        try
        {
            var baseName = Path.GetFileNameWithoutExtension(appPath);
            RunNetsh($"advfirewall firewall delete rule name=\"CyberWall-Allow-{baseName}\"");
            RunNetsh($"advfirewall firewall delete rule name=\"CyberWall-Allow-{baseName}-in\"");
            RunNetsh($"advfirewall firewall delete rule name=\"CyberWall-Block-{baseName}\"");
        }
        catch { }
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
