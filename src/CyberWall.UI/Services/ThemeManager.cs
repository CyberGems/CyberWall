using WApp = System.Windows.Application;
using CyberWall.Common.Settings;

namespace CyberWall.UI.Services;

public static class ThemeManager
{
    public record Palette(
        string Bg,
        string Card,
        string CardSecondary,
        string Border,
        string BorderLight,
        string Text,
        string SubText,
        string Accent,
        string AccentGlow,
        string AccentFg,
        string GridAlt,
        string HeaderBg,
        string BadgeAllowBg,
        string BadgeAllowFg,
        string BadgeBlockBg,
        string BadgeBlockFg,
        string BadgeWarnBg,
        string BadgeWarnFg,
        string SearchBg,
        string InputBg,
        string SwitchActive,
        string SwitchTrack);

    private static readonly Dictionary<AppTheme, Palette> Palettes = new()
    {
        // CyberWall Theme: Cybernetic Navy & Electric Neon Cyan / Emerald
        [AppTheme.CyberWall] = new(
            Bg: "#070B12",
            Card: "#0E1726",
            CardSecondary: "#131F33",
            Border: "#1C2E4A",
            BorderLight: "#2A436A",
            Text: "#F0F6FC",
            SubText: "#8BA2C4",
            Accent: "#00E5FF",
            AccentGlow: "#3300E5FF",
            AccentFg: "#070B12",
            GridAlt: "#0A111D",
            HeaderBg: "#111C2E",
            BadgeAllowBg: "#0A2E24",
            BadgeAllowFg: "#00F5A0",
            BadgeBlockBg: "#3B121E",
            BadgeBlockFg: "#FF4D6D",
            BadgeWarnBg: "#3D2410",
            BadgeWarnFg: "#FB923C",
            SearchBg: "#0A1220",
            InputBg: "#0A1220",
            SwitchActive: "#00E5FF",
            SwitchTrack: "#1C2E4A"),

        // Dark Theme: Refined Neutral Charcoal, Slate & Electric Indigo
        [AppTheme.Dark] = new(
            Bg: "#121214",
            Card: "#1A1A1E",
            CardSecondary: "#222228",
            Border: "#2E2E38",
            BorderLight: "#424250",
            Text: "#EDEDF0",
            SubText: "#9E9EA8",
            Accent: "#6366F1",
            AccentGlow: "#336366F1",
            AccentFg: "#FFFFFF",
            GridAlt: "#161619",
            HeaderBg: "#222229",
            BadgeAllowBg: "#132D21",
            BadgeAllowFg: "#34D399",
            BadgeBlockBg: "#37171C",
            BadgeBlockFg: "#F87171",
            BadgeWarnBg: "#3D2A12",
            BadgeWarnFg: "#FB923C",
            SearchBg: "#16161A",
            InputBg: "#16161A",
            SwitchActive: "#6366F1",
            SwitchTrack: "#2E2E38"),

        // Light Theme: Crisp Slate, Pure White & Royal Blue
        [AppTheme.Light] = new(
            Bg: "#F8FAFC",
            Card: "#FFFFFF",
            CardSecondary: "#F1F5F9",
            Border: "#E2E8F0",
            BorderLight: "#CBD5E1",
            Text: "#0F172A",
            SubText: "#64748B",
            Accent: "#2563EB",
            AccentGlow: "#222563EB",
            AccentFg: "#FFFFFF",
            GridAlt: "#F8FAFC",
            HeaderBg: "#F1F5F9",
            BadgeAllowBg: "#DCFCE7",
            BadgeAllowFg: "#15803D",
            BadgeBlockBg: "#FEE2E2",
            BadgeBlockFg: "#B91C1C",
            BadgeWarnBg: "#FFEDD5",
            BadgeWarnFg: "#C2410C",
            SearchBg: "#FFFFFF",
            InputBg: "#FFFFFF",
            SwitchActive: "#2563EB",
            SwitchTrack: "#CBD5E1"),
    };

    public static void Apply(AppTheme theme)
    {
        var p = Palettes[theme];
        var res = WApp.Current.Resources;
        void Set(string k, string hex) => res[k] = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
        Set("BgBrush", p.Bg);
        Set("CardBrush", p.Card);
        Set("CardSecondaryBrush", p.CardSecondary);
        Set("BorderBrush", p.Border);
        Set("BorderLightBrush", p.BorderLight);
        Set("TextBrush", p.Text);
        Set("SubTextBrush", p.SubText);
        Set("AccentBrush", p.Accent);
        Set("AccentGlowBrush", p.AccentGlow);
        Set("AccentFgBrush", p.AccentFg);
        Set("GridAltBrush", p.GridAlt);
        Set("HeaderBgBrush", p.HeaderBg);
        Set("BadgeAllowBgBrush", p.BadgeAllowBg);
        Set("BadgeAllowFgBrush", p.BadgeAllowFg);
        Set("BadgeBlockBgBrush", p.BadgeBlockBg);
        Set("BadgeBlockFgBrush", p.BadgeBlockFg);
        Set("BadgeWarnBgBrush", p.BadgeWarnBg);
        Set("BadgeWarnFgBrush", p.BadgeWarnFg);
        Set("SearchBgBrush", p.SearchBg);
        Set("InputBgBrush", p.InputBg);
        Set("SwitchActiveBrush", p.SwitchActive);
        Set("SwitchTrackBrush", p.SwitchTrack);
        if (WApp.Current.MainWindow != null) WApp.Current.MainWindow.Background = (System.Windows.Media.Brush)res["BgBrush"];
    }

    public static Palette Get(AppTheme t) => Palettes[t];
}
