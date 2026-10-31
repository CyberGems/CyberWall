using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CyberWall.UI.Services;

internal static class DpiAwarenessHelper
{
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new(-4);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    /// <summary>
    /// Enforces Per-Monitor V2 DPI awareness and cleans up legacy AppCompat registry flags
    /// (such as HIGHDPIAWARE or DPIUNAWARE) that force Windows to lock CyberWall to 96 DPI System mode.
    /// </summary>
    public static void Initialize()
    {
        try
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063))
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
        }
        catch { }

        CleanupLegacyAppCompatFlags();
    }

    /// <summary>
    /// Removes legacy HIGHDPIAWARE or DPIUNAWARE entries from HKLM/HKCU AppCompatFlags Layers
    /// that were left by older installer versions and prevent PerMonitorV2 scaling on high-DPI monitors.
    /// </summary>
    public static void CleanupLegacyAppCompatFlags()
    {
        try
        {
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string installedExe = Path.Combine(programFiles, "CyberWall", "CyberWall.exe");

            var pathsToCheck = new[] { currentExe, installedExe }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var hives = new[] { Registry.LocalMachine, Registry.CurrentUser };

            foreach (var hive in hives)
            {
                try
                {
                    using var layersKey = hive.OpenSubKey(
                        @"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",
                        writable: true);
                    if (layersKey == null) continue;

                    foreach (var path in pathsToCheck)
                    {
                        if (layersKey.GetValue(path) is string value)
                        {
                            if (value.Contains("HIGHDPIAWARE", StringComparison.OrdinalIgnoreCase) ||
                                value.Contains("DPIUNAWARE", StringComparison.OrdinalIgnoreCase))
                            {
                                var cleaned = value
                                    .Replace("HIGHDPIAWARE", "", StringComparison.OrdinalIgnoreCase)
                                    .Replace("DPIUNAWARE", "", StringComparison.OrdinalIgnoreCase)
                                    .Trim();

                                if (string.IsNullOrWhiteSpace(cleaned) || cleaned == "~")
                                {
                                    layersKey.DeleteValue(path, false);
                                }
                                else
                                {
                                    layersKey.SetValue(path, cleaned);
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
