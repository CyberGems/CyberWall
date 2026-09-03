using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace CyberWall.UI.Services;

public static class StartupHelper
{
    private const string TaskName = "CyberWall";
    private const string LegacyRunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "CyberWall";

    public static bool IsStartupEnabled()
    {
        try
        {
            // 1. Primary check: Task Scheduler task exists
            if (IsScheduledTaskPresent())
                return true;

            // 2. Fallback check: Legacy Registry Run key
            using var key = Registry.CurrentUser.OpenSubKey(LegacyRunKeyPath, writable: false);
            if (key != null)
            {
                var val = key.GetValue(AppName) as string;
                if (!string.IsNullOrWhiteSpace(val))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static void SetStartupEnabled(bool enable, bool startMinimized = true)
    {
        try
        {
            // Clean up any legacy registry Run entries to avoid duplicate/conflicting startup attempts
            CleanupLegacyRunKeys();

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    // Elevated apps require a Scheduled Task with HIGHEST runlevel to start at logon without UAC prompts
                    CreateScheduledTask(exePath, startMinimized);
                }
            }
            else
            {
                DeleteScheduledTask();
            }
        }
        catch { }
    }

    /// <summary>
    /// Self-healing synchronization: if startup is enabled, ensures the scheduled task is configured
    /// with the correct XML settings (Normal priority, no battery/timeout restrictions, and correct arguments).
    /// </summary>
    public static void EnsureTaskConfigured(bool startMinimized)
    {
        try
        {
            if (!IsStartupEnabled()) return;

            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;

            CreateScheduledTask(exePath, startMinimized);
            CleanupLegacyRunKeys();
        }
        catch { }
    }

    private static bool IsScheduledTaskPresent()
    {
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType != null)
            {
                dynamic service = Activator.CreateInstance(serviceType)!;
                service.Connect();
                dynamic rootFolder = service.GetFolder(@"\");
                try
                {
                    dynamic task = rootFolder.GetTask(TaskName);
                    if (task != null) return true;
                }
                catch { }
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
            psi.ArgumentList.Add("/Query");
            psi.ArgumentList.Add("/TN");
            psi.ArgumentList.Add(TaskName);

            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(3000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void CreateScheduledTask(string exePath, bool startMinimized = true)
    {
        try
        {
            var xml = BuildTaskXml(exePath, startMinimized);

            // 1. Try Windows Task Scheduler COM API (fastest, in-process, zero CLI parsing glitches)
            if (TryRegisterViaCom(xml))
                return;

            // 2. Fallback to schtasks.exe using native /XML parameter
            TryRegisterViaSchtasks(xml);
        }
        catch { }
    }

    private static string BuildTaskXml(string exePath, bool startMinimized)
    {
        var escapedPath = SecurityElement.Escape(exePath);
        var argsXml = startMinimized ? "<Arguments>--minimized</Arguments>" : string.Empty;

        return $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Author>CyberWall</Author>
    <Description>CyberWall Application Firewall Startup</Description>
    <URI>\{TaskName}</URI>
  </RegistrationInfo>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>4</Priority>
  </Settings>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Actions Context=""Author"">
    <Exec>
      <Command>{escapedPath}</Command>
      {argsXml}
    </Exec>
  </Actions>
</Task>";
    }

    private static bool TryRegisterViaCom(string xml)
    {
        try
        {
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType == null) return false;
            dynamic service = Activator.CreateInstance(serviceType)!;
            service.Connect();
            dynamic rootFolder = service.GetFolder(@"\");
            // 6 = TASK_CREATE_OR_UPDATE, 3 = TASK_LOGON_INTERACTIVE_TOKEN
            rootFolder.RegisterTask(TaskName, xml, 6, null, null, 3, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryRegisterViaSchtasks(string xml)
    {
        string? tempFile = null;
        try
        {
            tempFile = Path.Combine(Path.GetTempPath(), $"CyberWall_Task_{Guid.NewGuid():N}.xml");
            File.WriteAllText(tempFile, xml, System.Text.Encoding.Unicode);

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

            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(5000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (tempFile != null && File.Exists(tempFile))
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    private static void DeleteScheduledTask()
    {
        try
        {
            // 1. Try COM deletion
            var serviceType = Type.GetTypeFromProgID("Schedule.Service");
            if (serviceType != null)
            {
                dynamic service = Activator.CreateInstance(serviceType)!;
                service.Connect();
                dynamic rootFolder = service.GetFolder(@"\");
                try
                {
                    rootFolder.DeleteTask(TaskName, 0);
                    return;
                }
                catch { }
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
            p?.WaitForExit(5000);
        }
        catch { }
    }

    private static void CleanupLegacyRunKeys()
    {
        try
        {
            using var hkcu = Registry.CurrentUser.OpenSubKey(LegacyRunKeyPath, writable: true);
            hkcu?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch { }

        try
        {
            using var hklm = Registry.LocalMachine.OpenSubKey(LegacyRunKeyPath, writable: true);
            hklm?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch { }

        try
        {
            var startupLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "CyberWall.lnk");
            if (File.Exists(startupLnk))
            {
                File.Delete(startupLnk);
            }
        }
        catch { }
    }
}