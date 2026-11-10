using System.Diagnostics;
using System.IO;

namespace CyberWall.UI.Services;

internal static class ShellFolderHelper
{
    /// <summary>
    /// Opens the containing folder directly in Windows Explorer.
    /// Runs off the UI thread to prevent any UI freeze and avoids the multi-second
    /// /select command-line delay and redundant duplicate window launches.
    /// </summary>
    public static void OpenFolderAndSelect(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        Task.Run(() =>
        {
            try
            {
                var dir = File.Exists(path) ? Path.GetDirectoryName(path) : (Directory.Exists(path) ? path : null);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                // Direct folder launch (sub-200ms) - opens exactly one instance instantly
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShellFolderHelper error: {ex.Message}");
            }
        });
    }
}

