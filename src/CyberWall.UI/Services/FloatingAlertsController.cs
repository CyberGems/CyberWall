using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Popup;

namespace CyberWall.UI.Services;

/// <summary>
/// Coordinates pausing and resuming regular floating toast alerts across the tray menu,
/// main window bell indicator, notifications dialog, and settings view without
/// touching interactive firewall permission prompts or NotificationStore recording.
/// </summary>
internal static class FloatingAlertsController
{
    public static event Action? PauseStateChanged;

    public static bool IsPaused(DateTime? nowUtc = null)
        => App.Settings.IsFloatingAlertsPaused(nowUtc);

    public static string? GetPausedStatusLabel(AppSettings? settings, DateTime? nowUtc = null)
    {
        if (settings == null || !settings.IsFloatingAlertsPaused(nowUtc))
            return null;

        if (settings.FloatingAlertsPausedPermanent)
            return Strings.T("FloatingAlertsPausedPermanently");

        if (settings.GetFloatingAlertsPauseExpiryUtc() is DateTime expiryUtc)
            return Strings.T("FloatingAlertsPausedUntil", expiryUtc.ToLocalTime().ToString("t"));

        return null;
    }

    public static string? GetPausedBannerDescription(AppSettings? settings, DateTime? nowUtc = null)
    {
        var status = GetPausedStatusLabel(settings, nowUtc);
        if (string.IsNullOrEmpty(status)) return null;
        return $"{status}. {Strings.T("FloatingAlertsPausedBannerDesc")}";
    }

    public static void Pause(TimeSpan? duration)
    {
        App.Settings.SetFloatingAlertsPause(duration);
        App.Settings.Save();
        AppInfoToast.CloseAll();
        AutoBlockToast.CloseAll();
        PauseStateChanged?.Invoke();
    }

    public static void Resume()
    {
        if (App.Settings.ClearFloatingAlertsPause())
        {
            App.Settings.Save();
        }
        PauseStateChanged?.Invoke();
    }

    public static void NotifyStateChanged()
        => PauseStateChanged?.Invoke();
}

