using System.IO;
using System.Text.Json;
using CyberWall.Common.I18n;

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

public sealed class AppSettings
{
    public Lang Language { get; set; } = Lang.Es;
    public AppTheme Theme { get; set; } = AppTheme.CyberWall;
    public bool FirewallEnabled { get; set; } = true;
    public int FirewallMode { get; set; } = 2;
    public PopupPosition NotificationPosition { get; set; } = PopupPosition.BottomRight;
    public int NotificationMonitor { get; set; } = -1; // -1 = Active monitor under cursor
    public bool AutoCheckForUpdates { get; set; } = true;
    public bool RunAtStartup { get; set; } = true;
    public bool StartMinimized { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool PlaySoundOnPrompt { get; set; } = true;
    public string? CustomSoundPath { get; set; } = null;
    public bool PopupAutoBlockEnabled { get; set; } = true;
    public int PopupAutoBlockSeconds { get; set; } = 300;
    public bool MainWindowBoundsSaved { get; set; } = false;
    public string MainWindowMonitor { get; set; } = "";
    public double MainWindowLeft { get; set; }
    public double MainWindowTop { get; set; }
    public double MainWindowWidth { get; set; }
    public double MainWindowHeight { get; set; }
    public bool MainWindowMaximized { get; set; }

    private static string Path => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberWall", "settings.json");

    public static AppSettings Load()
    {
        try { if (File.Exists(Path)) return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path)) ?? new(); } catch { }
        var s = new AppSettings();
        var sys = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Lang.Es : Lang.En;
        s.Language = sys;
        return s;
    }

    public void Save()
    {
        try { Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!); File.WriteAllText(Path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); } catch { }
    }
}
