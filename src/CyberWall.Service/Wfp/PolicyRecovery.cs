using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;

namespace CyberWall.Service.Wfp;

/// <summary>
/// Crash recovery for the default-deny firewall policy.
///
/// CyberWall switches the machine-wide profile policy to "block inbound, block outbound".
/// If the process dies (crash, forced kill, power loss) the policy stays applied and the
/// host loses outbound connectivity with no visible cause. To avoid that, a scheduled task
/// runs periodically and restores the policy, but only when no engine is alive.
///
/// Liveness is detected with a heartbeat file written by the running engine. A stale
/// heartbeat means the owner is gone, so the recovery script restores the policy.
/// </summary>
public static class PolicyRecovery
{
    private const string TaskName = "CyberWall Recovery";
    private const string HeartbeatFileName = "engine-heartbeat.txt";
    private const string ScriptFileName = "recovery.ps1";

    /// <summary>How long a heartbeat stays valid. The recovery task runs every 2 minutes.</summary>
    private static readonly TimeSpan HeartbeatMaxAge = TimeSpan.FromMinutes(3);

    public static string DataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberWall");

    public static string HeartbeatPath => Path.Combine(DataDirectory, HeartbeatFileName);
    public static string ScriptPath => Path.Combine(DataDirectory, ScriptFileName);

    /// <summary>Writes the heartbeat that proves an engine is alive and owns the policy.</summary>
    public static void WriteHeartbeat()
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            File.WriteAllText(HeartbeatPath, DateTime.UtcNow.ToString("O"), Encoding.UTF8);
        }
        catch { }
    }

    public static void ClearHeartbeat()
    {
        try
        {
            if (File.Exists(HeartbeatPath)) File.Delete(HeartbeatPath);
        }
        catch { }
    }

    public static bool IsHeartbeatFresh()
    {
        try
        {
            if (!File.Exists(HeartbeatPath)) return false;
            var stamp = File.GetLastWriteTimeUtc(HeartbeatPath);
            return DateTime.UtcNow - stamp <= HeartbeatMaxAge;
        }
        catch { return false; }
    }

    /// <summary>
    /// Arms the recovery task. It is intentionally left armed while protection is on and
    /// removed once protection is cleanly turned off.
    /// </summary>
    public static bool Arm()
    {
        try
        {
            WriteRecoveryScript();
            return RegisterTask();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PolicyRecovery: could not arm the recovery task: {ex.Message}");
            return false;
        }
    }

    public static void Disarm()
    {
        try { UnregisterTask(); } catch { }
    }

    private static void WriteRecoveryScript()
    {
        Directory.CreateDirectory(DataDirectory);
        var script = string.Join(Environment.NewLine, new[]
        {
            "$ErrorActionPreference = 'SilentlyContinue'",
            $"$heartbeat = '{HeartbeatPath}'",
            "if (Test-Path $heartbeat) {",
            "    $age = (Get-Date) - (Get-Item $heartbeat).LastWriteTime",
            $"    if ($age.TotalMinutes -lt {HeartbeatMaxAge.TotalMinutes:0.##}) {{ exit 0 }}",
            "}",
            "netsh advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound | Out-Null",
            "Get-NetFirewallRule -DisplayName 'CyberWall-Block-*' | Remove-NetFirewallRule",
            "Get-NetFirewallRule -DisplayName 'CyberWall-Killswitch-*' | Remove-NetFirewallRule",
            "exit 0",
        });
        File.WriteAllText(ScriptPath, script, Encoding.UTF8);
    }

    private static string BuildTaskXml()
    {
        var escapedScript = SecurityElement.Escape(ScriptPath);
        return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>CyberWall</Author>
    <Description>Restores the Windows Firewall policy if CyberWall stops unexpectedly</Description>
    <URI>\{TaskName}</URI>
  </RegistrationInfo>
  <Principals>
    <Principal id=""Author"">
      <UserId>S-1-5-18</UserId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <ExecutionTimeLimit>PT2M</ExecutionTimeLimit>
    <StartWhenAvailable>false</StartWhenAvailable>
    <Enabled>true</Enabled>
  </Settings>
  <Triggers>
    <TimeTrigger>
      <Repetition>
        <Interval>PT2M</Interval>
        <StopAtDurationEnd>false</StopAtDurationEnd>
      </Repetition>
      <StartBoundary>2020-01-01T00:00:00</StartBoundary>
      <Enabled>true</Enabled>
    </TimeTrigger>
  </Triggers>
  <Actions Context=""Author"">
    <Exec>
      <Command>powershell.exe</Command>
      <Arguments>-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File ""{escapedScript}""</Arguments>
    </Exec>
  </Actions>
</Task>";
    }

    private static bool RegisterTask()
    {
        var xml = BuildTaskXml();
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType != null)
            {
                dynamic service = Activator.CreateInstance(serviceType)!;
                service.Connect();
                dynamic rootFolder = service.GetFolder(@"\");
                // 6 = TASK_CREATE_OR_UPDATE, 5 = TASK_LOGON_SERVICE_ACCOUNT
                rootFolder.RegisterTask(TaskName, xml, 6, null, null, 5, null);
                return true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"PolicyRecovery: COM task registration failed: {ex.Message}");
        }

        return RegisterTaskViaSchtasks(xml);
    }

    private static bool RegisterTaskViaSchtasks(string xml)
    {
        string? tempFile = null;
        try
        {
            tempFile = Path.Combine(Path.GetTempPath(), $"CyberWall_Recovery_{Guid.NewGuid():N}.xml");
            File.WriteAllText(tempFile, xml, Encoding.Unicode);

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("/Create");
            psi.ArgumentList.Add("/TN");
            psi.ArgumentList.Add(TaskName);
            psi.ArgumentList.Add("/XML");
            psi.ArgumentList.Add(tempFile);
            psi.ArgumentList.Add("/F");
            psi.ArgumentList.Add("/RU");
            psi.ArgumentList.Add("SYSTEM");
            psi.ArgumentList.Add("/RL");
            psi.ArgumentList.Add("HIGHEST");

            using var p = Process.Start(psi);
            if (p == null) return false;
            if (!p.WaitForExit(8000)) { try { p.Kill(); } catch { } return false; }
            return p.ExitCode == 0;
        }
        catch { return false; }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private static void UnregisterTask()
    {
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType != null)
            {
                dynamic service = Activator.CreateInstance(serviceType)!;
                service.Connect();
                dynamic rootFolder = service.GetFolder(@"\");
                try { rootFolder.DeleteTask(TaskName, 0); return; } catch { }
            }
        }
        catch { }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("/Delete");
            psi.ArgumentList.Add("/TN");
            psi.ArgumentList.Add(TaskName);
            psi.ArgumentList.Add("/F");

            using var p = Process.Start(psi);
            p?.WaitForExit(8000);
        }
        catch { }
    }
}
