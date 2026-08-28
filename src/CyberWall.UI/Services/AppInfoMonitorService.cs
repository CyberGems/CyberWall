using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
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
    private readonly ConcurrentDictionary<string, DateTime> _debounce = new(StringComparer.OrdinalIgnoreCase);

    public void CheckApp(string? appPath, NotificationStore notifications, FirewallService svc)
    {
        if (string.IsNullOrWhiteSpace(appPath) || !File.Exists(appPath)) return;
        if (appPath.Contains("CyberWall", StringComparison.OrdinalIgnoreCase)) return;

        var key = AppRule.Normalize(appPath);

        // Debounce checks to at most once every 5 seconds per app
        var now = DateTime.UtcNow;
        if (_debounce.TryGetValue(key, out var lastCheck) && (now - lastCheck).TotalSeconds < 5)
            return;
        _debounce[key] = now;

        try
        {
            var fi = new FileInfo(appPath);
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

            var hasStoredMeta = !string.IsNullOrEmpty(rule.LastKnownVersion) || rule.LastKnownFileSize.HasValue;

            if (!hasStoredMeta)
            {
                // First initialization of baseline metadata for this rule
                _sessionCache[key] = (currentVersion, currentSize, currentWriteTime);
                var initialRule = rule with
                {
                    LastKnownVersion = currentVersion,
                    LastKnownFileSize = currentSize,
                    LastKnownWriteTimeUtc = currentWriteTime
                };
                svc.Store.Upsert(initialRule);
                return;
            }

            // Check for Version Change
            if (!string.IsNullOrEmpty(rule.LastKnownVersion) && !string.IsNullOrEmpty(currentVersion) &&
                !rule.LastKnownVersion.Equals(currentVersion, StringComparison.OrdinalIgnoreCase))
            {
                var oldVer = rule.LastKnownVersion;
                var msg = Strings.T("NotifAppVersionChangedDesc", rule.DisplayName, oldVer, currentVersion);
                notifications.Add(AppNotificationKind.AppVersionChanged, rule.AppPath, rule.DisplayName, msg);
                AppInfoToast.ShowToast(Strings.T("NotifAppVersionChangedTitle"), msg, rule.AppPath);

                var updatedRule = rule with
                {
                    LastKnownVersion = currentVersion,
                    LastKnownFileSize = currentSize,
                    LastKnownWriteTimeUtc = currentWriteTime
                };
                svc.Store.Upsert(updatedRule);
                _sessionCache[key] = (currentVersion, currentSize, currentWriteTime);
                return;
            }

            // Check for Executable Binary Modification (size or last modified timestamp changed while version is same)
            var sizeChanged = rule.LastKnownFileSize.HasValue && rule.LastKnownFileSize.Value != currentSize;
            var timeChanged = rule.LastKnownWriteTimeUtc.HasValue &&
                              Math.Abs((currentWriteTime - rule.LastKnownWriteTimeUtc.Value).TotalSeconds) > 3;

            if (sizeChanged || (timeChanged && string.IsNullOrEmpty(currentVersion)))
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
                _sessionCache[key] = (currentVersion, currentSize, currentWriteTime);
            }
        }
        catch { }
    }

    private static string? Clean(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        var trimmed = val.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
