using System.ComponentModel;
using System.Runtime.CompilerServices;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;

namespace CyberWall.UI.Dialogs;

public sealed class NotificationItemVm : INotifyPropertyChanged
{
    private string _title = "";
    private string _description = "";
    private string _actionLabel = "";
    private bool _showAction;
    private bool _isBusy;
    private bool _isResolved;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Guid Id { get; init; }
    public AppNotificationKind Kind { get; init; }
    public string? AppPath { get; init; }
    public string? AppName { get; init; }
    public bool Highlight { get; init; }
    public string TimeLabel { get; init; } = "";
    public bool HasAppIcon => !string.IsNullOrWhiteSpace(AppPath);

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => Set(ref _description, value);
    }

    public string ActionLabel
    {
        get => _actionLabel;
        set => Set(ref _actionLabel, value);
    }

    public bool ShowAction
    {
        get => _showAction;
        set
        {
            if (Set(ref _showAction, value))
                OnPropertyChanged(nameof(CanAct));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (Set(ref _isBusy, value))
                OnPropertyChanged(nameof(CanAct));
        }
    }

    public bool IsResolved
    {
        get => _isResolved;
        set => Set(ref _isResolved, value);
    }

    public bool CanAct => ShowAction && !IsBusy && !IsResolved;

    public void MarkBusy(string label)
    {
        IsBusy = true;
        ActionLabel = label;
    }

    public void MarkResolved(string title, string description)
    {
        IsBusy = false;
        IsResolved = true;
        ShowAction = false;
        Title = title;
        Description = description;
        ActionLabel = title;
    }

    public static NotificationItemVm From(AppNotification n, bool highlight, bool isProtectionOn = false)
    {
        var name = string.IsNullOrWhiteSpace(n.AppName)
            ? (string.IsNullOrWhiteSpace(n.AppPath) ? "" : System.IO.Path.GetFileName(n.AppPath))
            : n.AppName;

        bool isUpdateActive = true;
        if (n.Kind == AppNotificationKind.UpdateAvailable)
        {
            var verStr = (n.Detail ?? n.AppName ?? "").Trim().TrimStart('v', 'V');
            if (Version.TryParse(verStr, out var v))
            {
                isUpdateActive = v > Services.UpdateService.GetCurrentVersion();
            }
        }

        bool isProtectionOffActionActive = (n.Kind == AppNotificationKind.ProtectionOff) && !isProtectionOn;

        var (title, desc) = n.Kind switch
        {
            AppNotificationKind.SilentBlock => (
                Strings.T("SilentBlockTitle"),
                Strings.T("SilentBlockDesc", string.IsNullOrEmpty(name) ? "?" : name)),
            AppNotificationKind.ProtectionOff => (
                Strings.T("NotifProtectionOffTitle"),
                isProtectionOn ? Strings.T("NotifProtectionOnDesc") : Strings.T("NotifProtectionOffDesc")),
            AppNotificationKind.UpdateAvailable => (
                Strings.T("NotifUpdateTitle"),
                isUpdateActive ? Strings.T("NotifUpdateDesc", n.Detail ?? "") : Strings.T("AlreadyUpdated")),
            _ => (
                Strings.T("AutoBlockedTitle"),
                Strings.T("AutoBlockedDesc", string.IsNullOrEmpty(name) ? "?" : name))
        };

        var showAllow = (n.Kind is AppNotificationKind.AutoBlocked or AppNotificationKind.SilentBlock)
                        && !string.IsNullOrWhiteSpace(n.AppPath);

        return new NotificationItemVm
        {
            Id = n.Id,
            Kind = n.Kind,
            AppPath = n.AppPath,
            AppName = name,
            Highlight = highlight,
            TimeLabel = FormatRelative(n.Timestamp),
            Title = title,
            Description = desc,
            ShowAction = showAllow || isProtectionOffActionActive || (n.Kind is AppNotificationKind.UpdateAvailable && isUpdateActive),
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

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
