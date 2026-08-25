using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CyberWall.Common;

namespace CyberWall.Service.Wfp;

internal static class ProcessIdentity
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int AppmodelErrorNoPackage = 15700;
    private const int ErrorInsufficientBuffer = 122;
    private const uint TcpStateDeleteTcb = 12;
    private const uint TcpStateSynSent = 2;
    private const uint TcpStateEstablished = 5;

    public static string? GetImagePath(int pid)
    {
        if (pid <= 0) return null;
        var h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (h == 0) return FallbackMainModule(pid);
        try
        {
            var size = 32768;
            var sb = new StringBuilder(size);
            if (QueryFullProcessImageName(h, 0, sb, ref size) && size > 0)
                return sb.ToString();
        }
        finally { CloseHandle(h); }

        return FallbackMainModule(pid);
    }

    public static bool TryGetPackageFamilyName(int pid, string? path, out string familyName)
    {
        familyName = "";
        if (pid > 0 && TryGetPackageFamilyNameFromPid(pid, out familyName))
            return true;
        return PackagePath.TryGetFamilyName(path, out familyName);
    }

    /// <summary>
    /// Store firewall rules key off the AppContainer SID (S-1-15-2-...), not the family name.
    /// </summary>
    public static bool TryGetPackageSid(string? packageFamilyName, out string sid)
    {
        sid = "";
        if (string.IsNullOrWhiteSpace(packageFamilyName)) return false;
        if (packageFamilyName.StartsWith("S-1-15-2-", StringComparison.OrdinalIgnoreCase))
        {
            sid = packageFamilyName;
            return true;
        }

        var hr = DeriveAppContainerSidFromAppContainerName(packageFamilyName, out var psid);
        if (hr != 0 || psid == 0) return false;
        try
        {
            if (!ConvertSidToStringSid(psid, out var str) || str == 0) return false;
            try
            {
                sid = Marshal.PtrToStringUni(str) ?? "";
                return !string.IsNullOrEmpty(sid);
            }
            finally { LocalFree(str); }
        }
        finally { FreeSid(psid); }
    }

    private static readonly HashSet<int> _suspended = new();
    private static readonly object _suspendLock = new();

    public static void TerminateTcpConnections(int pid, string? appPath)
    {
        var pids = new HashSet<int>();
        if (pid > 0) pids.Add(pid);

        if (!string.IsNullOrEmpty(appPath))
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(appPath);
                if (!string.IsNullOrEmpty(name))
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try { pids.Add(p.Id); } finally { p.Dispose(); }
                    }
                }
            }
            catch { }
        }

        foreach (var id in pids)
        {
            DeleteTcpRowsForPid(id);
            TryRemoveNetTcpConnection(id);
        }
    }

    public static void SuspendProcess(int pid)
    {
        if (pid <= 0) return;
        lock (_suspendLock)
        {
            if (!_suspended.Add(pid)) return;
        }
        var h = OpenProcess(0x0800, false, pid);
        if (h == 0) h = OpenProcess(0x0010 | 0x0800, false, pid);
        if (h == 0)
        {
            lock (_suspendLock) _suspended.Remove(pid);
            return;
        }
        try { NtSuspendProcess(h); }
        catch { lock (_suspendLock) _suspended.Remove(pid); }
        finally { CloseHandle(h); }
    }

    public static void ResumeProcesses(string? appPath, int pid)
    {
        var pids = new HashSet<int>();
        if (pid > 0) pids.Add(pid);
        if (!string.IsNullOrEmpty(appPath))
        {
            try
            {
                var name = Path.GetFileNameWithoutExtension(appPath);
                if (!string.IsNullOrEmpty(name))
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try { pids.Add(p.Id); } finally { p.Dispose(); }
                    }
                }
            }
            catch { }
        }

        foreach (var id in pids)
        {
            lock (_suspendLock) _suspended.Remove(id);
            var h = OpenProcess(0x0800, false, id);
            if (h == 0) continue;
            try { NtResumeProcess(h); }
            catch { }
            finally { CloseHandle(h); }
        }
    }

    [DllImport("ntdll.dll")]
    private static extern uint NtSuspendProcess(nint processHandle);

    [DllImport("ntdll.dll")]
    private static extern uint NtResumeProcess(nint processHandle);

    private static bool TryGetPackageFamilyNameFromPid(int pid, out string familyName)
    {
        familyName = "";
        var h = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (h == 0) return false;
        try
        {
            uint len = 0;
            var hr = GetPackageFamilyName(h, ref len, nint.Zero);
            if (hr == AppmodelErrorNoPackage) return false;
            if (hr != ErrorInsufficientBuffer && hr != 0) return false;
            if (len == 0) return false;
            var sb = new StringBuilder((int)len);
            hr = GetPackageFamilyName(h, ref len, sb);
            if (hr != 0) return false;
            familyName = sb.ToString();
            return !string.IsNullOrEmpty(familyName);
        }
        catch { return false; }
        finally { CloseHandle(h); }
    }

    private static string? FallbackMainModule(int pid)
    {
        try { using var p = Process.GetProcessById(pid); return p.MainModule?.FileName; }
        catch { return null; }
    }

    private static void DeleteTcpRowsForPid(int pid)
    {
        nint table = 0;
        try
        {
            int size = 0;
            GetExtendedTcpTable(nint.Zero, ref size, false, 2, 5, 0);
            if (size <= 0) return;
            table = Marshal.AllocHGlobal(size);
            if (GetExtendedTcpTable(table, ref size, false, 2, 5, 0) != 0) return;
            int count = Marshal.ReadInt32(table);
            nint row = table + 4;
            int rowSize = Marshal.SizeOf<MIB_TCPROW_OWNER_PID>();
            for (int i = 0; i < count; i++)
            {
                var r = Marshal.PtrToStructure<MIB_TCPROW_OWNER_PID>(row + i * rowSize);
                if ((int)r.owningPid != pid) continue;
                if (r.state != TcpStateSynSent && r.state != TcpStateEstablished) continue;
                var del = new MIB_TCPROW
                {
                    state = TcpStateDeleteTcb,
                    localAddr = r.localAddr,
                    localPort = r.localPort,
                    remoteAddr = r.remoteAddr,
                    remotePort = r.remotePort
                };
                SetTcpEntry(ref del);
            }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
        finally { if (table != 0) Marshal.FreeHGlobal(table); }
    }

    private static void TryRemoveNetTcpConnection(int pid)
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
            psi.ArgumentList.Add($"Get-NetTCPConnection -OwningProcess {pid} -ErrorAction SilentlyContinue | Remove-NetTCPConnection -Confirm:$false -ErrorAction SilentlyContinue");
            using var p = Process.Start(psi);
            p?.WaitForExit(8000);
        }
        catch { }
    }

    [DllImport("userenv.dll", CharSet = CharSet.Unicode)]
    private static extern int DeriveAppContainerSidFromAppContainerName(string pszAppContainerName, out nint ppsidAppContainerSid);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool ConvertSidToStringSid(nint sid, out nint stringSid);

    [DllImport("advapi32.dll")]
    private static extern nint FreeSid(nint pSid);

    [DllImport("kernel32.dll")]
    private static extern nint LocalFree(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(nint hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetPackageFamilyName")]
    private static extern int GetPackageFamilyName(nint hProcess, ref uint packageFamilyNameLength, nint packageFamilyName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetPackageFamilyName")]
    private static extern int GetPackageFamilyName(nint hProcess, ref uint packageFamilyNameLength, StringBuilder packageFamilyName);

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(nint pTable, ref int pdwSize, bool sort, int family, int tableClass, int reserved);

    [DllImport("iphlpapi.dll")]
    private static extern uint SetTcpEntry(ref MIB_TCPROW pTcpRow);

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW_OWNER_PID
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
        public uint owningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MIB_TCPROW
    {
        public uint state;
        public uint localAddr;
        public uint localPort;
        public uint remoteAddr;
        public uint remotePort;
    }
}
