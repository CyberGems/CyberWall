using WApp = System.Windows.Application;
using CyberWall.Common.Settings;

namespace CyberWall.UI.Services;

public static class ThemeManager
{
    public record Palette(
        string Bg,
        string Card,
        string CardSecondary,
        string SelectionBg,
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
        // CyberWall Theme: Refined Slate Navy & Electric Neon Cyan
        [AppTheme.CyberWall] = new(
            Bg: "#0B0E14",
            Card: "#141822",
            CardSecondary: "#1A202D",
            SelectionBg: "#1E283B",
            Border: "#242F41",
            BorderLight: "#34425A",
            Text: "#EEF2F6",
            SubText: "#94A0B4",
            Accent: "#00E5FF",
            AccentGlow: "#3300E5FF",
            AccentFg: "#0B0E14",
            GridAlt: "#10141C",
            HeaderBg: "#181E2B",
            BadgeAllowBg: "#0D2A22",
            BadgeAllowFg: "#00F5A0",
            BadgeBlockBg: "#38141D",
            BadgeBlockFg: "#FF4D6D",
            BadgeWarnBg: "#3D2611",
            BadgeWarnFg: "#FB923C",
            SearchBg: "#101520",
            InputBg: "#101520",
            SwitchActive: "#00E5FF",
            SwitchTrack: "#242F41"),

        // Dark Theme: Refined Neutral Charcoal, Slate & Electric Indigo
        [AppTheme.Dark] = new(
            Bg: "#121214",
            Card: "#1A1A1E",
            CardSecondary: "#222228",
            SelectionBg: "#282834",
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
            SelectionBg: "#DBEAFE",
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
        Set("SelectionBgBrush", p.SelectionBg);
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
