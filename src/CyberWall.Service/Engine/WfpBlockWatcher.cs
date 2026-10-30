using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using CyberWall.Common.Models;
using CyberWall.Service.Wfp;

namespace CyberWall.Service.Engine;

/// <summary>
/// Real-time watcher for Windows Filtering Platform (WFP) dropped connection events (Event ID 5157).
/// This provides instantaneous event-driven detection for CLI tools (git, curl, dotnet) and transient processes.
/// </summary>
public sealed class WfpBlockWatcher : IDisposable
{
    public event Action<ConnectionEvent>? OnBlockedConnection;

    private EventLogWatcher? _watcher;
    private readonly ConcurrentDictionary<string, DateTime> _recentEvents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _deviceMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _mapLock = new();
    private DateTime _lastMapRefresh = DateTime.MinValue;
    private bool _isDisposed;

    private const string WfpConnectionSubcategoryGuid = "{0CCE9226-69AE-11D9-BED3-505054503030}";

    /// <summary>Null when the audit state was never read, otherwise the previous "failure" state.</summary>
    private static bool? _auditWasEnabled;

    public static bool IsAdmin =>
        new System.Security.Principal.WindowsPrincipal(System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);

    public void Start()
    {
        if (!IsAdmin)
        {
            Debug.WriteLine("WfpBlockWatcher: Not running as Administrator. Event 5157 auditing will not be enabled.");
            return;
        }

        try
        {
            EnableAuditPolicy();
            RefreshDeviceMap();

            // Query Security log for Event ID 5157 (WFP Connection Blocked)
            var query = new EventLogQuery("Security", PathType.LogName, "*[System[(EventID=5157)]]");
            _watcher = new EventLogWatcher(query);
            _watcher.EventRecordWritten += OnEventRecordWritten;
            _watcher.Enabled = true;
            Debug.WriteLine("WfpBlockWatcher: Real-time WFP drop watcher started successfully.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WfpBlockWatcher: Error starting watcher: {ex.Message}");
        }
    }

    public void Stop()
    {
        try
        {
            if (_watcher != null)
            {
                _watcher.Enabled = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WfpBlockWatcher: Error stopping watcher: {ex.Message}");
        }
        RestoreAuditPolicy();
    }

    /// <summary>
    /// Enables the "Filtering Platform Connection" failure audit needed to receive
    /// event 5157. The previous state is remembered so it can be restored on stop,
    /// otherwise the Security log keeps filling with drop events forever.
    /// </summary>
    private static void EnableAuditPolicy()
    {
        try
        {
            if (_auditWasEnabled == null)
            {
                var current = RunAuditPol($"/get /subcategory:\"{WfpConnectionSubcategoryGuid}\"");
                if (!string.IsNullOrWhiteSpace(current))
                {
                    _auditWasEnabled = current.Contains("Failure:  Enabled", StringComparison.OrdinalIgnoreCase)
                                    || current.Contains("Failure: Enabled", StringComparison.OrdinalIgnoreCase);
                }
            }

            RunAuditPol($"/set /subcategory:\"{WfpConnectionSubcategoryGuid}\" /failure:enable");

            // The subcategory was turned on by CyberWall, so it has to be turned off again
            // if the process dies before Stop() runs.
            if (_auditWasEnabled == false)
            {
                PolicyRecovery.MarkSystemChange("restore-audit");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WfpBlockWatcher: Could not set audit policy: {ex.Message}");
        }
    }

    /// <summary>Puts the audit subcategory back the way it was found.</summary>
    private static void RestoreAuditPolicy()
    {
        try
        {
            if (_auditWasEnabled != false) return; // never changed it, or it was already enabled
            RunAuditPol($"/set /subcategory:\"{WfpConnectionSubcategoryGuid}\" /failure:disable");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WfpBlockWatcher: Could not restore audit policy: {ex.Message}");
        }
        finally
        {
            _auditWasEnabled = null;
            PolicyRecovery.ClearSystemChange("restore-audit");
        }
    }

    private static string? RunAuditPol(string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo("auditpol", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            if (p == null) return null;
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(5000)) { try { p.Kill(); } catch { } return null; }
            return output;
        }
        catch { return null; }
    }

    private void OnEventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        if (e.EventRecord == null || _isDisposed) return;

        try
        {
            using var record = e.EventRecord;
            var xml = record.ToXml();
            var parsed = ParseEventXml(xml);
            if (parsed == null) return;

            var resolvedPath = ResolveNtDevicePath(parsed.AppPath);
            if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
            {
                // If it's a valid path but File.Exists failed, test if resolvedPath without quotes works
                if (string.IsNullOrWhiteSpace(resolvedPath) || resolvedPath == "-" || resolvedPath.Equals("System", StringComparison.OrdinalIgnoreCase))
                    return;
            }

            // Exclude CyberWall binaries
            if (resolvedPath.Contains("CyberWall", StringComparison.OrdinalIgnoreCase)) return;

            // Debounce rapid repeat attempts (within 5 seconds per executable)
            var now = DateTime.UtcNow;
            if (_recentEvents.TryGetValue(resolvedPath, out var lastTime) && (now - lastTime).TotalSeconds < 5)
            {
                return;
            }
            _recentEvents[resolvedPath] = now;

            // Cleanup old items from debounce cache
            if (_recentEvents.Count > 100)
            {
                foreach (var kvp in _recentEvents.ToArray())
                {
                    if ((now - kvp.Value).TotalSeconds > 30)
                        _recentEvents.TryRemove(kvp.Key, out _);
                }
            }

            var ev = new ConnectionEvent
            {
                AppPath = resolvedPath,
                RemoteAddress = parsed.RemoteAddress,
                RemotePort = parsed.RemotePort,
                Direction = parsed.Direction,
                ProcessId = parsed.ProcessId,
                Protocol = parsed.Protocol,
                Timestamp = now
            };

            OnBlockedConnection?.Invoke(ev);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"WfpBlockWatcher: Error processing event: {ex.Message}");
        }
    }

    private sealed class RawEventData
    {
        public string AppPath { get; set; } = string.Empty;
        public string RemoteAddress { get; set; } = string.Empty;
        public int RemotePort { get; set; }
        public Direction Direction { get; set; } = Direction.Outbound;
        public string Protocol { get; set; } = "TCP";
        public int ProcessId { get; set; }
    }

    private static RawEventData? ParseEventXml(string xml)
    {
        try
        {
            var appPath = ExtractDataValue(xml, "Application");
            if (string.IsNullOrEmpty(appPath)) return null;

            var data = new RawEventData
            {
                AppPath = appPath,
                RemoteAddress = ExtractDataValue(xml, "DestAddress") ?? string.Empty
            };

            var portStr = ExtractDataValue(xml, "DestPort");
            if (portStr != null && int.TryParse(portStr, out var port))
                data.RemotePort = port;

            var pidStr = ExtractDataValue(xml, "ProcessId");
            if (pidStr != null && int.TryParse(pidStr, out var pid))
                data.ProcessId = pid;

            var protoCode = ExtractDataValue(xml, "Protocol");
            if (protoCode != null)
            {
                data.Protocol = protoCode switch
                {
                    "6" => "TCP",
                    "17" => "UDP",
                    "1" => "ICMP",
                    _ => protoCode
                };
            }

            var dirVal = ExtractDataValue(xml, "Direction");
            if (dirVal != null)
            {
                data.Direction = dirVal.Contains("14592") || dirVal.Equals("Inbound", StringComparison.OrdinalIgnoreCase)
                    ? Direction.Inbound
                    : Direction.Outbound;
            }

            return data;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractDataValue(string xml, string name)
    {
        string prefix = $"Name=\"{name}\">";
        int idx = xml.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            prefix = $"Name='{name}'>";
            idx = xml.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
        }

        int start = idx + prefix.Length;
        int end = xml.IndexOf("</Data>", start, StringComparison.Ordinal);
        if (end < 0) return null;
        return xml.Substring(start, end - start).Trim();
    }

    private void RefreshDeviceMap()
    {
        lock (_mapLock)
        {
            if ((DateTime.UtcNow - _lastMapRefresh).TotalMinutes < 5 && _deviceMap.Count > 0)
                return;

            _deviceMap.Clear();
            var drives = DriveInfo.GetDrives();
            var sb = new StringBuilder(512);

            foreach (var drive in drives)
            {
                var driveLetter = drive.Name.TrimEnd('\\'); // e.g. "C:"
                if (QueryDosDevice(driveLetter, sb, sb.Capacity) > 0)
                {
                    var devicePath = sb.ToString();
                    if (!string.IsNullOrEmpty(devicePath))
                    {
                        _deviceMap[devicePath] = driveLetter;
                    }
                }
            }
            _lastMapRefresh = DateTime.UtcNow;
        }
    }

    private string ResolveNtDevicePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        if (path.Length >= 2 && path[1] == ':') return path; // Already a drive letter path

        RefreshDeviceMap();

        lock (_mapLock)
        {
            foreach (var (devicePrefix, driveLetter) in _deviceMap)
            {
                if (path.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var relative = path.Substring(devicePrefix.Length);
                    return driveLetter + (relative.StartsWith("\\") ? relative : "\\" + relative);
                }
            }
        }

        // Fallback: check if path contains "HarddiskVolume" pattern
        var match = Regex.Match(path, @"^\\Device\\HarddiskVolume\d+(.*)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var suffix = match.Groups[1].Value;
            var systemDrive = Path.GetPathRoot(Environment.SystemDirectory)?.TrimEnd('\\') ?? "C:";
            var candidate = systemDrive + suffix;
            if (File.Exists(candidate)) return candidate;
        }

        return path;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern uint QueryDosDevice(string lpDeviceName, StringBuilder lpTargetPath, int ucchMax);

    public void Dispose()
    {
        _isDisposed = true;
        Stop();
    }
}
