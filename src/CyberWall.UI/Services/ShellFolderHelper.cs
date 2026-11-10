using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CyberWall.UI.Services;

internal static class ShellFolderHelper
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr ILCreateFromPathW(string pszPath);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, IntPtr[]? apidl, uint dwFlags);

    [DllImport("shell32.dll", ExactSpelling = true)]
    private static extern void ILFree(IntPtr pidl);

    /// <summary>
    /// Opens the containing folder and selects the file in Windows Explorer.
    /// Uses SHOpenFolderAndSelectItems via COM to leverage the running Explorer instance,
    /// avoiding the multi-second startup delay of launching a separate explorer.exe process.
    /// Runs off the UI thread to prevent any UI freeze.
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

                // Attempt native in-process shell selection via STA thread
                if (File.Exists(path))
                {
                    bool success = false;
                    var staThread = new Thread(() =>
                    {
                        try
                        {
                            IntPtr pidl = ILCreateFromPathW(path);
                            if (pidl != IntPtr.Zero)
                            {
                                try
                                {
                                    if (SHOpenFolderAndSelectItems(pidl, 0, null, 0) == 0)
                                    {
                                        success = true;
                                    }
                                }
                                finally
                                {
                                    ILFree(pidl);
                                }
                            }
                        }
                        catch { }
                    });
                    staThread.SetApartmentState(ApartmentState.STA);
                    staThread.Start();
                    if (staThread.Join(400) && success)
                    {
                        return;
                    }
                }

                // Instant direct folder launch (sub-200ms) avoiding the multi-second /select delay
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ShellFolderHelper error: {ex.Message}");
            }
        });
    }
}

