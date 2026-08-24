using WApp = System.Windows.Application;
using CyberWall.Common.Settings;

namespace CyberWall.UI.Services;

public static class ThemeManager
{
    public record Palette(string Bg, string Card, string Border, string Text, string SubText, string Accent, string AccentFg, string GridAlt, string HeaderBg);

    private static readonly Dictionary<AppTheme, Palette> Palettes = new()
    {
        [AppTheme.CyberWall] = new("#0A0F14", "#141B22", "#243040", "#E6EDF3", "#8B949E", "#1F6FEB", "#FFFFFF", "#101820", "#1A2532"),
        [AppTheme.Dark] = new("#0D1117", "#161B22", "#30363D", "#E6EDF3", "#8B949E", "#1F6FEB", "#FFFFFF", "#1A1F29", "#21262D"),
        [AppTheme.Light] = new("#F1F5F9", "#FFFFFF", "#E2E8F0", "#0F172A", "#64748B", "#2563EB", "#FFFFFF", "#F8FAFC", "#F1F5F9"),
    };

    public static void Apply(AppTheme theme)
    {
        var p = Palettes[theme];
        var res = WApp.Current.Resources;
        void Set(string k, string hex) => res[k] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
        Set("BgBrush", p.Bg);
        Set("CardBrush", p.Card);
        Set("BorderBrush", p.Border);
        Set("TextBrush", p.Text);
        Set("SubTextBrush", p.SubText);
        Set("AccentBrush", p.Accent);
        Set("AccentFgBrush", p.AccentFg);
        Set("GridAltBrush", p.GridAlt);
        Set("HeaderBgBrush", p.HeaderBg);
        if (WApp.Current.MainWindow != null) WApp.Current.MainWindow.Background = (System.Windows.Media.Brush)res["BgBrush"];
    }

    public static Palette Get(AppTheme t) => Palettes[t];
}
