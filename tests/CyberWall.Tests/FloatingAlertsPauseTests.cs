using System.Text.Json;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.Tests;

public class FloatingAlertsPauseTests
{
    [Fact]
    public void DefaultsDoNotPauseOrSuppressFloatingAlerts()
    {
        var settings = new AppSettings();

        Assert.False(settings.FloatingAlertsPausedPermanent);
        Assert.Null(settings.FloatingAlertsPausedUntilUtc);
        Assert.False(settings.IsFloatingAlertsPaused());
        Assert.False(AppSettings.ShouldSuppressRegularFloatingAlert(settings));
        Assert.Null(FloatingAlertsController.GetPausedStatusLabel(settings));
        Assert.Null(FloatingAlertsController.GetPausedBannerDescription(settings));
    }

    [Theory]
    [InlineData(60)]
    [InlineData(360)]
    [InlineData(720)]
    [InlineData(1440)]
    public void TimedPauseIsActiveBeforeExpiryAndExpiresAutomatically(int minutes)
    {
        var now = new DateTime(2026, 11, 12, 16, 0, 0, DateTimeKind.Utc);
        var settings = new AppSettings();

        settings.SetFloatingAlertsPause(TimeSpan.FromMinutes(minutes), now);

        Assert.False(settings.FloatingAlertsPausedPermanent);
        Assert.Equal(now + TimeSpan.FromMinutes(minutes), settings.FloatingAlertsPausedUntilUtc);
        Assert.True(settings.IsFloatingAlertsPaused(now + TimeSpan.FromMinutes(minutes - 1)));
        Assert.False(settings.IsFloatingAlertsPaused(now + TimeSpan.FromMinutes(minutes)));
        Assert.False(settings.IsFloatingAlertsPaused(now + TimeSpan.FromMinutes(minutes + 5)));
    }

    [Fact]
    public void PermanentPauseOverridesTimestampAndPersistsAcrossJsonRoundTrip()
    {
        var settings = new AppSettings
        {
            ToastAppInfoEnabled = false,
            ToastInternetEventsEnabled = true,
            ToastProtectionEventsEnabled = false,
            ToastAutoBlockEnabled = true
        };

        var now = new DateTime(2026, 11, 12, 16, 0, 0, DateTimeKind.Utc);
        settings.SetFloatingAlertsPause(TimeSpan.FromHours(6), now);
        settings.SetFloatingAlertsPause(null, now);

        var json = JsonSerializer.Serialize(settings);
        var reloaded = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.True(reloaded.FloatingAlertsPausedPermanent);
        Assert.Null(reloaded.FloatingAlertsPausedUntilUtc);
        Assert.True(reloaded.IsFloatingAlertsPaused(now.AddDays(30)));

        // Granular per-category settings must remain untouched through pause & resume.
        Assert.True(reloaded.ClearFloatingAlertsPause());
        Assert.False(reloaded.ClearFloatingAlertsPause());
        Assert.False(reloaded.IsFloatingAlertsPaused(now));
        Assert.False(reloaded.ToastAppInfoEnabled);
        Assert.True(reloaded.ToastInternetEventsEnabled);
        Assert.False(reloaded.ToastProtectionEventsEnabled);
        Assert.True(reloaded.ToastAutoBlockEnabled);
    }

    [Fact]
    public void UnspecifiedExpiryKindFromManualJsonIsTreatedAsUtc()
    {
        var json = """{"FloatingAlertsPausedUntilUtc":"2026-11-12T18:00:00"}""";
        var settings = JsonSerializer.Deserialize<AppSettings>(json)!;

        Assert.True(settings.IsFloatingAlertsPaused(new DateTime(2026, 11, 12, 17, 59, 0, DateTimeKind.Utc)));
        Assert.False(settings.IsFloatingAlertsPaused(new DateTime(2026, 11, 12, 18, 1, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void CriticalSafetyAlertsBypassPauseWhileRegularAlertsAreSuppressed()
    {
        var settings = new AppSettings();
        settings.SetFloatingAlertsPause(null);

        Assert.True(AppSettings.ShouldSuppressRegularFloatingAlert(settings));
        Assert.True(AppSettings.ShouldSuppressRegularFloatingAlert(settings, bypassPause: false));
        Assert.False(AppSettings.ShouldSuppressRegularFloatingAlert(settings, bypassPause: true));
    }

    [Theory]
    [InlineData(Lang.En)]
    [InlineData(Lang.Es)]
    public void StatusAndBannerCopyRenderForTimedAndPermanentPauses(Lang lang)
    {
        var prev = Strings.Current;
        Strings.Current = lang;
        try
        {
            var now = new DateTime(2026, 11, 12, 16, 0, 0, DateTimeKind.Utc);
            var settings = new AppSettings();
            settings.SetFloatingAlertsPause(TimeSpan.FromHours(1), now);

            var timedLabel = FloatingAlertsController.GetPausedStatusLabel(settings, now);
            var timedBanner = FloatingAlertsController.GetPausedBannerDescription(settings, now);
            Assert.False(string.IsNullOrWhiteSpace(timedLabel));
            Assert.Contains(timedLabel!, timedBanner!);

            settings.SetFloatingAlertsPause(null, now);
            var permLabel = FloatingAlertsController.GetPausedStatusLabel(settings, now);
            Assert.Equal(Strings.T("FloatingAlertsPausedPermanently"), permLabel);
        }
        finally
        {
            Strings.Current = prev;
        }
    }
}

