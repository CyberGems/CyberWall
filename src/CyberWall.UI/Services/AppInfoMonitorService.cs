using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Notifications;
using CyberWall.Service.Engine;
using CyberWall.UI.Popup;

namespace CyberWall.UI.Services;

public sealed class AppInfoMonitorService
{
    public static AppInfoMonitorService Instance { get; } = new();

    private readonly ConcurrentDictionary<string, (string? Version, long FileSize, DateTime WriteTime)> _sessionCache = new(StringComparer.OrdinalIgnoreCase);
    private System.Threading.Timer? _timer;
    private NotificationStore? _notifications;
    private FirewallService? _svc;
    private int _scanning;

    public void Start(NotificationStore notifications, FirewallService svc)
    {
        _notifications = notifications;
        _svc = svc;

        // Run immediately, then every 2.5 seconds
        _timer?.Dispose();
        _timer = new System.Threading.Timer(_ => ScanAllRules(), null, 500, 2500);
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    public void CheckApp(string? appPath)
    {
        if (_notifications == null || _svc == null) return;
        if (string.IsNullOrWhiteSpace(appPath) || !File.Exists(appPath)) return;
        if (appPath.Contains("CyberWall", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            InspectAppFile(appPath, _notifications, _svc);
        }
        catch { }
    }

    private void ScanAllRules()
    {
        if (_notifications == null || _svc == null) return;
        if (Interlocked.Exchange(ref _scanning, 1) == 1) return;

        try
        {
            var rules = _svc.Store.All;
            foreach (var rule in rules)
            {
                if (string.IsNullOrWhiteSpace(rule.AppPath) || !File.Exists(rule.AppPath)) continue;
                if (rule.AppPath.Contains("CyberWall", StringComparison.OrdinalIgnoreCase)) continue;

                InspectAppFile(rule.AppPath, _notifications, _svc);
            }
        }
        catch { }
        finally
        {
            Interlocked.Exchange(ref _scanning, 0);
        }
    }

    private void InspectAppFile(string appPath, NotificationStore notifications, FirewallService svc)
    {
        var key = AppRule.Normalize(appPath);

        FileInfo fi;
        try
        {
            fi = new FileInfo(appPath);
            if (!fi.Exists) return;
        }
        catch
        {
            return;
        }

        var currentSize = fi.Length;
        var currentWriteTime = fi.LastWriteTimeUtc;

        string? currentVersion = null;
        try
        {
            var vi = FileVersionInfo.GetVersionInfo(appPath);
            currentVersion = Clean(vi.ProductVersion) ?? Clean(vi.FileVersion);
        }
        catch { }

        if (!svc.Store.TryGet(appPath, out var rule))
        {
            _sessionCache[key] = (currentVersion, currentSize, currentWriteTime);
            return;
        }

        if (!_sessionCache.TryGetValue(key, out var cached))
        {
            // First time seen in this runtime session
            var hasStoredMeta = !string.IsNullOrEmpty(rule.LastKnownVersion) || rule.LastKnownFileSize.HasValue;

            if (hasStoredMeta)
            {
                // Check if file was modified/updated while CyberWall was closed
                var versionChanged = !string.IsNullOrEmpty(rule.LastKnownVersion) &&
                                     !string.IsNullOrEmpty(currentVersion) &&
                                     !rule.LastKnownVersion.Equals(currentVersion, StringComparison.OrdinalIgnoreCase);

                var binaryChanged = rule.LastKnownFileSize.HasValue &&
                                    (rule.LastKnownFileSize.Value != currentSize ||
                                     (rule.LastKnownWriteTimeUtc.HasValue &&
                                      Math.Abs((currentWriteTime - rule.LastKnownWriteTimeUtc.Value).TotalSeconds) > 3));

                if (versionChanged)
                {
                    var oldVer = rule.LastKnownVersion!;
                    var msg = Strings.T("NotifAppVersionChangedDesc", rule.DisplayName, oldVer, currentVersion!);
                    notifications.Add(AppNotificationKind.AppVersionChanged, rule.AppPath, rule.DisplayName, msg);
                    AppInfoToast.ShowToast(Strings.T("NotifAppVersionChangedTitle"), msg, rule.AppPath);

                    var updatedRule = rule with
                    {
                        LastKnownVersion = currentVersion,
                        LastKnownFileSize = currentSize,
                        LastKnownWriteTimeUtc = currentWriteTime
                    };
                    svc.Store.Upsert(updatedRule);
                }
                else if (binaryChanged && string.IsNullOrEmpty(currentVersion))
                {
                    var msg = Strings.T("NotifAppExecutableChangedDesc", rule.DisplayName);
                    notifications.Add(AppNotificationKind.AppExecutableChanged, rule.AppPath, rule.DisplayName, msg);
                    AppInfoToast.ShowToast(Strings.T("NotifAppExecutableChangedTitle"), msg, rule.AppPath);

                    var updatedRule = rule with
                    {
                        LastKnownVersion = currentVersion,
                        LastKnownFileSize = currentSize,
                        LastKnownWriteTimeUtc = currentWriteTime
                    };
                    svc.Store.Upsert(updatedRule);
                }
            }
            else
            {
                // First initialization of baseline metadata for this rule
                var initialRule = rule with
                {
                    LastKnownVersion = currentVersion,
                    LastKnownFileSize = currentSize,
                    LastKnownWriteTimeUtc = currentWriteTime
                };
                svc.Store.Upsert(initialRule);
            }

            _sessionCache[key] = (currentVersion, currentSize, currentWriteTime);
            return;
        }

        // Live check against active session cache: has the file changed since last tick?
        var fileModified = cached.FileSize != currentSize ||
                           Math.Abs((currentWriteTime - cached.WriteTime).TotalSeconds) > 2;

        if (fileModified)
        {
            var versionChanged = !string.IsNullOrEmpty(cached.Version) &&
                                 !string.IsNullOrEmpty(currentVersion) &&
                                 !cached.Version.Equals(currentVersion, StringComparison.OrdinalIgnoreCase);

            if (versionChanged)
            {
                var oldVer = cached.Version!;
                var msg = Strings.T("NotifAppVersionChangedDesc", rule.DisplayName, oldVer, currentVersion!);
                notifications.Add(AppNotificationKind.AppVersionChanged, rule.AppPath, rule.DisplayName, msg);
                AppInfoToast.ShowToast(Strings.T("NotifAppVersionChangedTitle"), msg, rule.AppPath);
            }
            else
            {
                var msg = Strings.T("NotifAppExecutableChangedDesc", rule.DisplayName);
                notifications.Add(AppNotificationKind.AppExecutableChanged, rule.AppPath, rule.DisplayName, msg);
                AppInfoToast.ShowToast(Strings.T("NotifAppExecutableChangedTitle"), msg, rule.AppPath);
            }

            var updatedRule = rule with
            {
                LastKnownVersion = currentVersion,
                LastKnownFileSize = currentSize,
                LastKnownWriteTimeUtc = currentWriteTime
            };
            svc.Store.Upsert(updatedRule);
            _sessionCache[key] = (currentVersion, currentSize, currentWriteTime);
        }
    }

    private static string? Clean(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        var trimmed = val.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
