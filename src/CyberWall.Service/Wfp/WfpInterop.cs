using System.Runtime.InteropServices;

namespace CyberWall.Service.Wfp;

/// <summary>
/// Minimal WFP session interop. CyberWall uses the Windows Filtering Platform to
/// confirm the filtering engine is reachable; the rules themselves are applied through
/// the Windows Firewall COM API, so no filter is ever installed from here.
/// </summary>
internal static class WfpInterop
{
    public const string Fwpuclnt = "fwpuclnt.dll";

    [DllImport(Fwpuclnt, SetLastError = true)]
    public static extern uint FwpmEngineOpen0(string? serverName, uint authnService, nint authIdentity, in FWPM_SESSION0 session, out nint engineHandle);

    [DllImport(Fwpuclnt)] public static extern uint FwpmEngineClose0(nint engineHandle);
    [DllImport(Fwpuclnt)] public static extern uint FwpmTransactionBegin0(nint engineHandle, uint flags);
    [DllImport(Fwpuclnt)] public static extern uint FwpmTransactionCommit0(nint engineHandle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FWPM_SESSION0
    {
        public Guid sessionKey;
        public FWPM_DISPLAY_DATA0 displayData;
        public uint flags;
        public uint txnWaitTimeoutInMSec;
        public uint processId;
        public FWP_BYTE_BLOB sid;
        public string? username;
        public bool kernelMode;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct FWPM_DISPLAY_DATA0 { public string? name; public string? description; }

    [StructLayout(LayoutKind.Sequential)] public struct FWP_BYTE_BLOB { public uint size; public nint data; }
}
