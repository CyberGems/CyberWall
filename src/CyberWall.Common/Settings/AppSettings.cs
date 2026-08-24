using System.IO;
using System.Text.Json;
using CyberWall.Common.I18n;

namespace CyberWall.Common.Settings;

public enum AppTheme { CyberWall, Dark, Light }

public sealed class AppSettings
{
    public Lang Language { get; set; } = Lang.Es;
    public AppTheme Theme { get; set; } = AppTheme.CyberWall;
    public bool FirewallEnabled { get; set; } = true;
    public int FirewallMode { get; set; } = 2;

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
