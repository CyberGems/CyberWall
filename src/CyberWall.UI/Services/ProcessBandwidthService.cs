using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CyberWall.Common.Models;
using CyberWall.Service.Wfp;

namespace CyberWall.UI.Services;

public sealed record ProcessBandwidthUsage(
    string AppPath,
    string DisplayName,
    double DownloadBps,
    double UploadBps,
    long TotalBytesIn,
    long TotalBytesOut
)
{
    public double TotalBps => DownloadBps + UploadBps;
    public string FormattedDownload => NetworkSpeedService.FormatSpeed(DownloadBps);
    public string FormattedUpload => NetworkSpeedService.FormatSpeed(UploadBps);
    public string FormattedTotalTransfer => NetworkSpeedService.FormatBytes(TotalBytesIn + TotalBytesOut);
    public string QuickBlockText => CyberWall.Common.I18n.Strings.T("BandwidthQuickBlock");
    public double UsagePercent { get; set; }
}

public sealed class ProcessBandwidthService : IDisposable
{
    private static readonly Lazy<ProcessBandwidthService> _instance = new(() => new ProcessBandwidthService());
    public static ProcessBandwidthService Instance => _instance.Value;

    private readonly ConcurrentDictionary<int, long> _pendingBytesSent = new();
    private readonly ConcurrentDictionary<int, long> _pendingBytesRecv = new();
    private readonly ConcurrentDictionary<string, ProcessBandwidthUsage> _appBandwidths = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<int, (string? Path, DateTime CachedAt)> _pidPathCache = new();
    private readonly ConcurrentDictionary<int, (ulong ReadTransfer, ulong WriteTransfer)> _lastIoCounters = new();

    private Thread? _etwThread;
    private ulong _sessionHandle;
    private ulong _traceHandle = ulong.MaxValue;
    private volatile bool _running;
    private volatile bool _etwActive;
    private readonly System.Threading.Timer _aggregationTimer;
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _lastAggregationTime;
    private EventRecordCallback? _recordCallback; // Prevent GC of delegate

    public event Action? BandwidthUpdated;

    private const string SessionName = "CyberWallKernelNetSession";
    private static readonly Guid KernelNetworkGuid = new("7DD42A49-5329-4832-8DFD-43D979153A88");

    private ProcessBandwidthService()
    {
        _aggregationTimer = new System.Threading.Timer(_ => Aggregate(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _stopwatch.Restart();
        _lastAggregationTime = _stopwatch.Elapsed;

        // Attempt to launch ETW kernel network trace
        try
        {
            _recordCallback = EventRecordCallbackHandler;
            _etwThread = new Thread(RunEtwWorker)
            {
                IsBackground = true,
                Name = "CyberWall-KernelNetEtw",
                Priority = ThreadPriority.AboveNormal
            };
            _etwThread.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProcessBandwidthService: ETW initialization error: {ex.Message}");
            _etwActive = false;
        }

        // Start 1000ms periodic aggregation timer
        _aggregationTimer.Change(1000, 1000);
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _aggregationTimer.Change(Timeout.Infinite, Timeout.Infinite);

        StopEtw();
        _stopwatch.Stop();
    }

    public ProcessBandwidthUsage? GetBandwidth(string appPath)
    {
        if (string.IsNullOrWhiteSpace(appPath)) return null;
        if (_appBandwidths.TryGetValue(appPath, out var usage)) return usage;

        try
        {
            var norm = AppRule.Normalize(appPath);
            if (_appBandwidths.TryGetValue(norm, out usage)) return usage;
        }
        catch { }

        return null;
    }

    public IReadOnlyList<ProcessBandwidthUsage> GetTopConsumers(int maxCount = 6)
    {
        var items = _appBandwidths.Values
            .Where(b => b.TotalBps > 100) // Filter out negligible/idle noise
            .OrderByDescending(b => b.TotalBps)
            .Take(maxCount)
            .ToList();

        double maxBps = items.Count > 0 ? items.Max(i => i.TotalBps) : 0;
        if (maxBps > 0)
        {
            foreach (var item in items)
            {
                item.UsagePercent = Math.Clamp((item.TotalBps / maxBps) * 100.0, 5.0, 100.0);
            }
        }

        return items;
    }

    private void Aggregate()
    {
        if (!_running) return;

        try
        {
            var nowTime = _stopwatch.Elapsed;
            double elapsedSeconds = (nowTime - _lastAggregationTime).TotalSeconds;
            if (elapsedSeconds < 0.2) return;
            _lastAggregationTime = nowTime;

            // Cache cleanup every 45s
            var nowUtc = DateTime.UtcNow;
            if (_pidPathCache.Count > 200)
            {
                foreach (var kvp in _pidPathCache)
                {
                    if ((nowUtc - kvp.Value.CachedAt).TotalSeconds > 45)
                    {
                        _pidPathCache.TryRemove(kvp.Key, out _);
                    }
                }
            }

            var deltaDownByPath = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var deltaUpByPath = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

            if (_etwActive)
            {
                // Process ETW gathered data
                var pids = _pendingBytesRecv.Keys.Concat(_pendingBytesSent.Keys).Distinct().ToList();
                foreach (var pid in pids)
                {
                    long recvBytes = _pendingBytesRecv.TryRemove(pid, out var r) ? r : 0;
                    long sentBytes = _pendingBytesSent.TryRemove(pid, out var s) ? s : 0;

                    if (recvBytes <= 0 && sentBytes <= 0) continue;

                    var path = ResolvePidToPath(pid);
                    if (string.IsNullOrWhiteSpace(path)) continue;

                    double downRate = recvBytes / elapsedSeconds;
                    double upRate = sentBytes / elapsedSeconds;

                    deltaDownByPath[path] = deltaDownByPath.GetValueOrDefault(path) + downRate;
                    deltaUpByPath[path] = deltaUpByPath.GetValueOrDefault(path) + upRate;
                }
            }
            else
            {
                // Fallback: Use Process I/O counters on active network PIDs
                SampleProcessIoCounters(elapsedSeconds, deltaDownByPath, deltaUpByPath);
            }

            // Update app bandwidth states with smooth decay
            var allKnownPaths = _appBandwidths.Keys.Concat(deltaDownByPath.Keys).Concat(deltaUpByPath.Keys).Distinct().ToList();
            foreach (var path in allKnownPaths)
            {
                bool hasCurrent = deltaDownByPath.TryGetValue(path, out double currentDown);
                bool hasCurrentUp = deltaUpByPath.TryGetValue(path, out double currentUp);

                if (hasCurrent || hasCurrentUp)
                {
                    var existing = _appBandwidths.GetValueOrDefault(path);
                    long totalIn = (existing?.TotalBytesIn ?? 0) + (long)(currentDown * elapsedSeconds);
                    long totalOut = (existing?.TotalBytesOut ?? 0) + (long)(currentUp * elapsedSeconds);

                    string displayName;
                    try { displayName = Path.GetFileNameWithoutExtension(path); }
                    catch { displayName = path; }

                    _appBandwidths[path] = new ProcessBandwidthUsage(
                        path,
                        displayName,
                        currentDown,
                        currentUp,
                        totalIn,
                        totalOut
                    );
                }
                else if (_appBandwidths.TryGetValue(path, out var existing))
                {
                    if (existing.TotalBps > 50)
                    {
                        // Rapid decay to 0 when process stops sending/receiving
                        _appBandwidths[path] = existing with
                        {
                            DownloadBps = Math.Max(0, existing.DownloadBps * 0.3),
                            UploadBps = Math.Max(0, existing.UploadBps * 0.3)
                        };
                    }
                    else if (existing.TotalBps > 0)
                    {
                        _appBandwidths[path] = existing with { DownloadBps = 0, UploadBps = 0 };
                    }
                }
            }

            BandwidthUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProcessBandwidthService.Aggregate error: {ex.Message}");
        }
    }

    private void SampleProcessIoCounters(double elapsed, Dictionary<string, double> downMap, Dictionary<string, double> upMap)
    {
        // Query active processes with established network sockets
        var activePids = ProcessTrafficTracker.Instance.GetActivePids();
        foreach (var pid in activePids)
        {
            if (pid <= 4) continue;
            try
            {
                using var proc = Process.GetProcessById(pid);
                if (proc.HasExited) continue;

                if (GetProcessIoCounters(proc.Handle, out var io))
                {
                    if (_lastIoCounters.TryGetValue(pid, out var last))
                    {
                        if (io.ReadTransferCount >= last.ReadTransfer && io.WriteTransferCount >= last.WriteTransfer)
                        {
                            ulong deltaRead = io.ReadTransferCount - last.ReadTransfer;
                            ulong deltaWrite = io.WriteTransferCount - last.WriteTransfer;

                            if (deltaRead > 0 || deltaWrite > 0)
                            {
                                var path = ResolvePidToPath(pid);
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    downMap[path] = downMap.GetValueOrDefault(path) + (deltaRead / elapsed);
                                    upMap[path] = upMap.GetValueOrDefault(path) + (deltaWrite / elapsed);
                                }
                            }
                        }
                    }
                    _lastIoCounters[pid] = (io.ReadTransferCount, io.WriteTransferCount);
                }
            }
            catch { }
        }
    }

    private string? ResolvePidToPath(int pid)
    {
        if (_pidPathCache.TryGetValue(pid, out var cached))
        {
            return cached.Path;
        }

        string? path = null;
        try
        {
            path = ProcessIdentity.GetImagePath(pid);
            if (path != null && HostAppResolver.TryResolveHost(pid, path, out var hostPath, out _))
            {
                path = hostPath;
            }
        }
        catch { }

        _pidPathCache[pid] = (path, DateTime.UtcNow);
        return path;
    }

    #region ETW Engine

    private void RunEtwWorker()
    {
        int totalSize = Marshal.SizeOf<EVENT_TRACE_PROPERTIES>() + ((SessionName.Length + 1) * sizeof(char)) + 128;
        IntPtr pProperties = Marshal.AllocHGlobal(totalSize);

        try
        {
            // Zero memory
            for (int i = 0; i < totalSize; i++) Marshal.WriteByte(pProperties, i, 0);

            var props = new EVENT_TRACE_PROPERTIES
            {
                Wnode = new WNODE_HEADER
                {
                    BufferSize = (uint)totalSize,
                    Flags = 0x00020000 // WNODE_FLAG_TRACED_GUID
                },
                LogFileMode = 0x00000100, // EVENT_TRACE_REAL_TIME_MODE
                LoggerNameOffset = (uint)Marshal.SizeOf<EVENT_TRACE_PROPERTIES>()
            };

            Marshal.StructureToPtr(props, pProperties, false);

            // Clean up any stale session with the same name
            ControlTraceW(0, SessionName, pProperties, 1 /* EVENT_TRACE_CONTROL_STOP */);

            // Start ETW trace
            uint status = StartTraceW(out _sessionHandle, SessionName, pProperties);
            if (status != 0)
            {
                Debug.WriteLine($"ProcessBandwidthService: StartTraceW failed with status {status}. Switching to fallback.");
                _etwActive = false;
                return;
            }

            // Enable Microsoft-Windows-Kernel-Network provider
            var guid = KernelNetworkGuid;
            status = EnableTraceEx2(
                _sessionHandle,
                ref guid,
                1, // EVENT_CONTROL_CODE_ENABLE_PROVIDER
                4, // TRACE_LEVEL_INFORMATION
                0x30, // KERNEL_NETWORK_KEYWORD_IPV4 | KERNEL_NETWORK_KEYWORD_IPV6
                0,
                0,
                IntPtr.Zero);

            if (status != 0)
            {
                Debug.WriteLine($"ProcessBandwidthService: EnableTraceEx2 returned {status}.");
            }

            var logfile = new EVENT_TRACE_LOGFILEW
            {
                LoggerName = SessionName,
                ProcessTraceMode = 0x00000100 /* PROCESS_TRACE_MODE_REAL_TIME */ | 0x10000000 /* PROCESS_TRACE_MODE_EVENT_RECORD */,
                EventRecordCallback = _recordCallback!
            };

            _traceHandle = OpenTraceW(ref logfile);
            if (_traceHandle == ulong.MaxValue)
            {
                Debug.WriteLine("ProcessBandwidthService: OpenTraceW returned INVALID_PROCESSTRACE_HANDLE.");
                _etwActive = false;
                return;
            }

            _etwActive = true;
            Debug.WriteLine("ProcessBandwidthService: ETW Kernel Network session successfully activated.");

            // Blocking call: processes real-time events until CloseTrace is called
            var handles = new[] { _traceHandle };
            ProcessTrace(handles, 1, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProcessBandwidthService: ETW worker encountered exception: {ex.Message}");
            _etwActive = false;
        }
        finally
        {
            if (pProperties != IntPtr.Zero) Marshal.FreeHGlobal(pProperties);
            _etwActive = false;
        }
    }

    private void EventRecordCallbackHandler(ref EVENT_RECORD record)
    {
        try
        {
            int pid = (int)record.EventHeader.ProcessId;
            if (pid <= 4) return;

            byte opcode = record.EventHeader.EventDescriptor.Opcode;
            ushort eventId = record.EventHeader.EventDescriptor.Id;

            // Opcode mappings for Microsoft-Windows-Kernel-Network
            // 10: TcpSend IPv4, 11: TcpRecv IPv4
            // 26: TcpSend IPv6, 27: TcpRecv IPv6
            // 42: UdpSend IPv4, 43: UdpRecv IPv4
            // 49/58: UdpSend IPv6, 59: UdpRecv IPv6
            bool isSend = (opcode == 10 || opcode == 26 || opcode == 42 || opcode == 49 || opcode == 58 || eventId == 10 || eventId == 42);
            bool isRecv = (opcode == 11 || opcode == 27 || opcode == 43 || opcode == 59 || eventId == 11 || eventId == 43);

            if (!isSend && !isRecv) return;

            int payloadBytes = 0;
            if (record.UserDataLength >= 8 && record.UserData != IntPtr.Zero)
            {
                int val1 = Marshal.ReadInt32(record.UserData);
                int val2 = Marshal.ReadInt32(record.UserData + 4);

                // In Kernel Network MOF events, field 0 is often PID and field 1 is transfer size
                if (val1 == pid && val2 > 0 && val2 <= 2_000_000)
                {
                    payloadBytes = val2;
                }
                else if (val1 > 0 && val1 <= 2_000_000)
                {
                    payloadBytes = val1;
                }
                else
                {
                    payloadBytes = (int)record.UserDataLength;
                }
            }
            else
            {
                payloadBytes = (int)record.UserDataLength;
            }

            if (payloadBytes <= 0) return;

            if (isSend)
            {
                _pendingBytesSent.AddOrUpdate(pid, payloadBytes, (_, current) => current + payloadBytes);
            }
            else
            {
                _pendingBytesRecv.AddOrUpdate(pid, payloadBytes, (_, current) => current + payloadBytes);
            }
        }
        catch { }
    }

    private void StopEtw()
    {
        try
        {
            if (_traceHandle != ulong.MaxValue && _traceHandle != 0)
            {
                CloseTrace(_traceHandle);
                _traceHandle = ulong.MaxValue;
            }

            int totalSize = Marshal.SizeOf<EVENT_TRACE_PROPERTIES>() + ((SessionName.Length + 1) * sizeof(char)) + 128;
            IntPtr pProperties = Marshal.AllocHGlobal(totalSize);
            try
            {
                for (int i = 0; i < totalSize; i++) Marshal.WriteByte(pProperties, i, 0);
                var props = new EVENT_TRACE_PROPERTIES
                {
                    Wnode = new WNODE_HEADER { BufferSize = (uint)totalSize },
                    LoggerNameOffset = (uint)Marshal.SizeOf<EVENT_TRACE_PROPERTIES>()
                };
                Marshal.StructureToPtr(props, pProperties, false);
                ControlTraceW(_sessionHandle, SessionName, pProperties, 1 /* EVENT_TRACE_CONTROL_STOP */);
            }
            finally
            {
                Marshal.FreeHGlobal(pProperties);
            }
        }
        catch { }
    }

    #endregion

    #region Win32 P/Invoke

    [StructLayout(LayoutKind.Sequential)]
    public struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetProcessIoCounters(IntPtr hProcess, out IO_COUNTERS lpIoCounters);

    [StructLayout(LayoutKind.Sequential)]
    public struct WNODE_HEADER
    {
        public uint BufferSize;
        public uint ProviderId;
        public ulong HistoricalContext;
        public ulong TimeStamp;
        public Guid Guid;
        public uint ClientContext;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct EVENT_TRACE_PROPERTIES
    {
        public WNODE_HEADER Wnode;
        public uint BufferSize;
        public uint MinimumBuffers;
        public uint MaximumBuffers;
        public uint MaximumFileSize;
        public uint LogFileMode;
        public uint FlushTimer;
        public uint EnableFlags;
        public int AgeLimit;
        public uint NumberOfBuffers;
        public uint FreeBuffers;
        public uint EventsLost;
        public uint BuffersWritten;
        public uint LogBuffersLost;
        public uint RealTimeBuffersLost;
        public IntPtr LoggerThreadId;
        public uint LogFileNameOffset;
        public uint LoggerNameOffset;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EVENT_DESCRIPTOR
    {
        public ushort Id;
        public byte Version;
        public byte Channel;
        public byte Level;
        public byte Opcode;
        public ushort Task;
        public ulong Keyword;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EVENT_HEADER
    {
        public ushort Size;
        public ushort HeaderType;
        public ushort Flags;
        public ushort EventProperty;
        public uint ThreadId;
        public uint ProcessId;
        public long TimeStamp;
        public Guid ProviderId;
        public EVENT_DESCRIPTOR EventDescriptor;
        public ulong ProcessorTime;
        public Guid ActivityId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ETW_BUFFER_CONTEXT
    {
        public byte ProcessorNumber;
        public byte Alignment;
        public ushort LoggerId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EVENT_RECORD
    {
        public EVENT_HEADER EventHeader;
        public ETW_BUFFER_CONTEXT BufferContext;
        public ushort ExtendedDataCount;
        public ushort UserDataLength;
        public IntPtr ExtendedData;
        public IntPtr UserData;
        public IntPtr UserContext;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void EventRecordCallback([In] ref EVENT_RECORD record);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct EVENT_TRACE_LOGFILEW
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? LogFileName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? LoggerName;
        public long CurrentTime;
        public uint BuffersRead;
        public uint ProcessTraceMode;
        public EVENT_TRACE CurrentEvent;
        public TRACE_LOGFILE_HEADER LogfileHeader;
        public IntPtr BufferCallback;
        public int BufferSize;
        public int Filled;
        public int EventsLost;
        public EventRecordCallback EventRecordCallback;
        public int IsKernelTrace;
        public IntPtr Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct EVENT_TRACE
    {
        public EVENT_HEADER Header;
        public uint InstanceId;
        public uint ParentInstanceId;
        public Guid ParentGuid;
        public IntPtr MofData;
        public uint MofLength;
        public uint ClientContext;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TRACE_LOGFILE_HEADER
    {
        public uint BufferSize;
        public uint MajorVersion;
        public uint MinorVersion;
        public uint SubVersion;
        public uint SubMinorVersion;
        public uint ProviderVersion;
        public uint NumberOfProcessors;
        public long EndTime;
        public uint TimerResolution;
        public uint MaximumFileSize;
        public uint LogFileMode;
        public uint BuffersWritten;
        public Guid LogInstanceGuid;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string LoggerName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string LogFileName;
        public uint TimeZoneInformation;
        public long BootTime;
        public long PerfFreq;
        public long StartTime;
        public uint ReservedFlags;
        public uint BuffersLost;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint StartTraceW(out ulong SessionHandle, string SessionName, IntPtr Properties);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint ControlTraceW(ulong SessionHandle, string SessionName, IntPtr Properties, uint ControlCode);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern uint EnableTraceEx2(ulong SessionHandle, ref Guid ProviderId, uint ControlCode, byte Level, ulong MatchAnyKeyword, ulong MatchAllKeyword, uint Timeout, IntPtr EnableParameters);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ulong OpenTraceW(ref EVENT_TRACE_LOGFILEW Logfile);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern uint ProcessTrace([In] ulong[] HandleArray, uint HandleCount, IntPtr StartTime, IntPtr EndTime);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern uint CloseTrace(ulong TraceHandle);

    #endregion

    public void Dispose()
    {
        Stop();
        _aggregationTimer.Dispose();
    }
}
