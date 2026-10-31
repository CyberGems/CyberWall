using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberWall.Common.I18n;
using CyberWall.Common.IO;

namespace CyberWall.Common.Settings;

public enum AppTheme { CyberWall, Dark, Light }

public enum PopupPosition
{
    TopLeft,
    TopCenter,
    TopRight,
    Left,
    Right,
    BottomLeft,
    BottomCenter,
    BottomRight
}

public enum TrafficAnimationMode { FluidStream, PulseGlow, Disabled }

public sealed class AppSettings
{
    public Lang Language { get; set; } = Lang.Es;
    public AppTheme Theme { get; set; } = AppTheme.CyberWall;
    public TrafficAnimationMode TrafficAnimation { get; set; } = TrafficAnimationMode.FluidStream;
    public bool FirewallEnabled { get; set; } = true;

    [JsonConverter(typeof(FirewallModeConverter))]
    public Models.FirewallMode FirewallMode { get; set; } = Models.FirewallMode.Ask;
    public PopupPosition NotificationPosition { get; set; } = PopupPosition.BottomRight;
    public int NotificationMonitor { get; set; } = -1; // -1 = Active monitor under cursor
    public bool AutoCheckForUpdates { get; set; } = true;
    public bool RunAtStartup { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool PlaySoundOnPrompt { get; set; } = true;
    public string? CustomSoundPath { get; set; } = null;
    public bool PopupAutoBlockEnabled { get; set; } = false;
    public int PopupAutoBlockSeconds { get; set; } = 300;

    /// <summary>
    /// Existing installations used to auto-block unanswered prompts. This flag lets the
    /// upgrade turn that off exactly once, without undoing a later user choice.
    /// </summary>
    public bool ManualBlockDefaultApplied { get; set; } = false;
    public bool ToastAppInfoEnabled { get; set; } = true;
    public bool ToastInternetEventsEnabled { get; set; } = true;
    public bool ToastProtectionEventsEnabled { get; set; } = true;
    public bool ToastAutoBlockEnabled { get; set; } = true;
    public bool AllowedRulesExpanded { get; set; } = true;
    public bool BlockedRulesExpanded { get; set; } = true;
    public bool MainWindowBoundsSaved { get; set; } = false;
    public string MainWindowMonitor { get; set; } = "";

    // Floating traffic widget (desktop companion window).
    public bool TrafficWidgetVisible { get; set; } = false;
    public bool TrafficWidgetAlwaysOnTop { get; set; } = true;
    public bool TrafficWidgetCompactMode { get; set; } = false;
    public bool TrafficWidgetShowGraph { get; set; } = true;
    public bool TrafficWidgetShowSession { get; set; } = true;
    public bool TrafficWidgetBoundsSaved { get; set; } = false;
    public double TrafficWidgetLeft { get; set; }
    public double TrafficWidgetTop { get; set; }
    public double MainWindowLeft { get; set; }
    public double MainWindowTop { get; set; }
    public double MainWindowWidth { get; set; }
    public double MainWindowHeight { get; set; }
    public bool MainWindowMaximized { get; set; }

    /// <summary>
    /// Set when the firewall policy could not be restored while closing. The next
    /// start warns the user, because the host may be left without connectivity.
    /// </summary>
    public bool PolicyRestoreFailed { get; set; }

    private static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberWall", "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(Path))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new AppSettings();
                if (ApplyManualBlockDefault(loaded))
                    loaded.Save();
                return loaded;
            }
        }
        catch { }

        var s = new AppSettings();
        s.ManualBlockDefaultApplied = true;
        var sys = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Lang.Es : Lang.En;
        s.Language = sys;
        return s;
    }

    /// <summary>
    /// One-time upgrade for settings written before unanswered prompts stopped blocking
    /// by default. A file without the marker is treated as an old install.
    /// </summary>
    internal static bool ApplyManualBlockDefault(AppSettings settings)
    {
        if (settings.ManualBlockDefaultApplied) return false;
        settings.PopupAutoBlockEnabled = false;
        settings.ManualBlockDefaultApplied = true;
        return true;
    }

    public void Save()
    {
        try
        {
            AtomicFile.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppSettings: could not save settings: {ex.Message}");
        }
    }
}
