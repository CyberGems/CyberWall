using CyberWall.Common.I18n;
using CyberWall.Common.Models;

namespace CyberWall.UI.Dialogs;

public sealed class NotificationItemVm
{
    public Guid Id { get; init; }
    public AppNotificationKind Kind { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string TimeLabel { get; init; } = "";
    public string? AppPath { get; init; }
    public bool Highlight { get; init; }
    public bool ShowAllow { get; init; }
    public bool ShowEnable { get; init; }
    public bool ShowDownload { get; init; }
    public bool ShowAction => ShowAllow || ShowEnable || ShowDownload;
    public string ActionLabel { get; init; } = "";
    public bool HasAppIcon => !string.IsNullOrWhiteSpace(AppPath);

    public static NotificationItemVm From(AppNotification n, bool highlight)
    {
        var name = string.IsNullOrWhiteSpace(n.AppName)
            ? (string.IsNullOrWhiteSpace(n.AppPath) ? "" : System.IO.Path.GetFileName(n.AppPath))
            : n.AppName;

        var (title, desc) = n.Kind switch
        {
            AppNotificationKind.SilentBlock => (
                Strings.T("SilentBlockTitle"),
                Strings.T("SilentBlockDesc", string.IsNullOrEmpty(name) ? "?" : name)),
            AppNotificationKind.ProtectionOff => (
                Strings.T("NotifProtectionOffTitle"),
                Strings.T("NotifProtectionOffDesc")),
            AppNotificationKind.UpdateAvailable => (
                Strings.T("NotifUpdateTitle"),
                Strings.T("NotifUpdateDesc", n.Detail ?? "")),
            _ => (
                Strings.T("AutoBlockedTitle"),
                Strings.T("AutoBlockedDesc", string.IsNullOrEmpty(name) ? "?" : name))
        };

        return new NotificationItemVm
        {
            Id = n.Id,
            Kind = n.Kind,
            Title = title,
            Description = desc,
            TimeLabel = FormatRelative(n.Timestamp),
            AppPath = n.AppPath,
            Highlight = highlight,
            ShowAllow = (n.Kind is AppNotificationKind.AutoBlocked or AppNotificationKind.SilentBlock)
                        && !string.IsNullOrWhiteSpace(n.AppPath),
            ShowEnable = n.Kind == AppNotificationKind.ProtectionOff,
            ShowDownload = n.Kind == AppNotificationKind.UpdateAvailable,
            ActionLabel = n.Kind switch
            {
                AppNotificationKind.ProtectionOff => Strings.T("TurnProtectionOn"),
                AppNotificationKind.UpdateAvailable => Strings.T("Download"),
                _ => Strings.T("AutoBlockedUndo")
            }
        };
    }

    private static string FormatRelative(DateTime timestamp)
    {
        var utc = timestamp.Kind switch
        {
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            DateTimeKind.Utc => timestamp,
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)
        };
        var local = utc.ToLocalTime();
        var delta = DateTime.Now - local;
        if (delta.TotalMinutes < 1) return Strings.T("NotifJustNow");
        if (delta.TotalHours < 1) return Strings.T("NotifMinutesAgo", Math.Max(1, (int)delta.TotalMinutes));
        if (delta.TotalDays < 1) return Strings.T("NotifHoursAgo", Math.Max(1, (int)delta.TotalHours));
        if (delta.TotalDays < 7) return Strings.T("NotifDaysAgo", Math.Max(1, (int)delta.TotalDays));
        return local.ToString("g");
    }
}
