using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using CyberWall.Common;

namespace CyberWall.Service.Wfp;

internal static class HostAppResolver
{
    private static readonly Regex WebViewExeName = new(
        @"--webview-exe-name=([^\s""]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UserDataDir = new(
        @"--user-data-dir=(?:""([^""]+)""|([^\s]+))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool IsSharedRuntime(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var name = Path.GetFileName(path);
        return name.Equals("msedgewebview2.exe", StringComparison.OrdinalIgnoreCase)
            || name.Equals("msedge.exe", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps a WebView2/Edge helper PID back to the host app that launched it.
    /// </summary>
    public static bool TryResolveHost(int pid, string? path, out string hostPath, out int hostPid)
    {
        hostPath = path ?? "";
        hostPid = pid;
        if (!IsSharedRuntime(path)) return false;

        var cmd = GetCommandLine(pid);
        if (string.IsNullOrEmpty(cmd)) return false;

        var exeMatch = WebViewExeName.Match(cmd);
        if (exeMatch.Success)
        {
            var hostName = Path.GetFileNameWithoutExtension(exeMatch.Groups[1].Value);
            foreach (var p in Process.GetProcessesByName(hostName))
            {
                try
                {
                    var img = ProcessIdentity.GetImagePath(p.Id);
                    if (!string.IsNullOrEmpty(img))
                    {
                        hostPath = img;
                        hostPid = p.Id;
                        return true;
                    }
                }
                finally { p.Dispose(); }
            }
        }

        var dirMatch = UserDataDir.Match(cmd);
        if (dirMatch.Success)
        {
            var dir = dirMatch.Groups[1].Success ? dirMatch.Groups[1].Value : dirMatch.Groups[2].Value;
            if (PackagePath.TryGetFamilyName(dir, out _))
            {
                hostPath = dir;
                return true;
            }
            var packages = dir.IndexOf(@"\Packages\", StringComparison.OrdinalIgnoreCase);
            if (packages >= 0)
            {
                var rest = dir[(packages + @"\Packages\".Length)..];
                var pfn = rest.Split('\\')[0];
                if (!string.IsNullOrEmpty(pfn))
                {
                    foreach (var p in Process.GetProcesses())
                    {
                        try
                        {
                            var img = ProcessIdentity.GetImagePath(p.Id);
                            if (img != null && PackagePath.TryGetFamilyName(img, out var fam) &&
                                fam.Equals(pfn, StringComparison.OrdinalIgnoreCase))
                            {
                                hostPath = img;
                                hostPid = p.Id;
                                return true;
                            }
                        }
                        catch { }
                        finally { p.Dispose(); }
                    }
                }
            }
        }

        return false;
    }

    public static List<int> FindHelperPids(string hostAppPath)
    {
        var result = new List<int>();
        var hostName = Path.GetFileName(hostAppPath);
        PackagePath.TryGetFamilyName(hostAppPath, out var pfn);
        try
        {
            foreach (var p in Process.GetProcessesByName("msedgewebview2"))
            {
                try
                {
                    var cmd = GetCommandLine(p.Id);
                    if (string.IsNullOrEmpty(cmd)) continue;
                    if (!string.IsNullOrEmpty(hostName) &&
                        cmd.Contains(hostName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(p.Id);
                        continue;
                    }
                    if (!string.IsNullOrEmpty(pfn) &&
                        cmd.Contains(pfn, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Add(p.Id);
                    }
                }
                finally { p.Dispose(); }
            }
        }
        catch { }

        return result;
    }

    public static void TerminateHelpers(string hostAppPath)
    {
        foreach (var pid in FindHelperPids(hostAppPath))
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.Kill(entireProcessTree: true);
            }
            catch { }
        }
    }

    public static string? GetCommandLine(int pid)
    {
        var h = OpenProcess(0x1000, false, pid);
        if (h == 0) return null;
        try
        {
            NtQueryInformationProcess(h, 60, nint.Zero, 0, out int len);
            if (len <= 0) return null;
            var buf = Marshal.AllocHGlobal(len);
            try
            {
                var st = NtQueryInformationProcess(h, 60, buf, len, out _);
                if (st != 0) return null;
                var us = Marshal.PtrToStructure<UnicodeString>(buf);
                if (us.Buffer == 0 || us.Length == 0) return null;
                return Marshal.PtrToStringUni(us.Buffer, us.Length / 2);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch { return null; }
        finally { CloseHandle(h); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public nint Buffer;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(nint processHandle, int processInformationClass, nint processInformation, int processInformationLength, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);
}
