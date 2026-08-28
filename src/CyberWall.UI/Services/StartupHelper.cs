using System.Diagnostics;
using System.IO;
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

    public static void SetStartupEnabled(bool enable)
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
                    CreateScheduledTask(exePath);
                }
            }
            else
            {
                DeleteScheduledTask();
            }
        }
        catch { }
    }

    private static bool IsScheduledTaskPresent()
    {
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

    private static void CreateScheduledTask(string exePath)
    {
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
            psi.ArgumentList.Add("/Create");
            psi.ArgumentList.Add("/TN");
            psi.ArgumentList.Add(TaskName);
            psi.ArgumentList.Add("/TR");
            psi.ArgumentList.Add($"\"{exePath}\"");
            psi.ArgumentList.Add("/SC");
            psi.ArgumentList.Add("ONLOGON");
            psi.ArgumentList.Add("/RL");
            psi.ArgumentList.Add("HIGHEST");
            psi.ArgumentList.Add("/F");

            using var p = Process.Start(psi);
            p?.WaitForExit(5000);
        }
        catch { }
    }

    private static void DeleteScheduledTask()
    {
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