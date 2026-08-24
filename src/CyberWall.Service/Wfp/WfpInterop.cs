using System.Runtime.InteropServices;

namespace CyberWall.Service.Wfp;

internal static class WfpInterop
{
    public const string Fwpuclnt = "fwpuclnt.dll";

    [DllImport(Fwpuclnt, SetLastError = true)]
    public static extern uint FwpmEngineOpen0(string? serverName, uint authnService, nint authIdentity, in FWPM_SESSION0 session, out nint engineHandle);

    [DllImport(Fwpuclnt)] public static extern uint FwpmEngineClose0(nint engineHandle);
    [DllImport(Fwpuclnt)] public static extern uint FwpmTransactionBegin0(nint engineHandle, uint flags);
    [DllImport(Fwpuclnt)] public static extern uint FwpmTransactionCommit0(nint engineHandle);
    [DllImport(Fwpuclnt)] public static extern uint FwpmTransactionAbort0(nint engineHandle);

    [DllImport(Fwpuclnt)] public static extern uint FwpmFilterAdd0(nint engineHandle, in FWPM_FILTER0 filter, nint sd, out ulong filterId);
    [DllImport(Fwpuclnt)] public static extern uint FwpmFilterDeleteById0(nint engineHandle, ulong filterId);

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

    [StructLayout(LayoutKind.Sequential)] public struct FWPM_FILTER0 { public Guid filterKey; public FWPM_DISPLAY_DATA0 displayData; public uint flags; public Guid providerKey; public FWP_BYTE_BLOB providerData; public Guid layerKey; public Guid subLayerKey; public FWP_VALUE0 weight; public uint numFilterConditions; public nint filterCondition; public FWPM_ACTION0 action; public Guid calloutKey; public ulong filterId; public FWP_VALUE0 effectiveWeight; }

    [StructLayout(LayoutKind.Sequential)] public struct FWP_VALUE0 { public uint type; public ulong uint64; }

    [StructLayout(LayoutKind.Sequential)] public struct FWPM_ACTION0 { public uint type; public Guid filterType; }
}
