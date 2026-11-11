using System.Windows.Input;

namespace CyberWall.UI.Controls;

public enum TitleBarShortcutAction
{
    None = 0,
    ToggleAbout,
    SelectFirewall,
    SelectTraffic,
    SelectConnections,
    SelectStatistics,
    OpenSettings,
    CheckOrApplyUpdate,
    OpenNotifications,
    ToggleWidget,
    ToggleMoreMenu,
    MinimizeWindow,
    MaximizeOrRestoreWindow,
    CloseWindow
}

/// <summary>
/// Centralized keyboard shortcut constants and key routing for main-window title bar controls,
/// following the CyberManager suite convention.
/// </summary>
internal static class KeyboardShortcuts
{
    internal const string About = "F1";
    internal const string Firewall = "Ctrl+1";
    internal const string TrafficMonitor = "Ctrl+2";
    internal const string ConnectionsLog = "Ctrl+3";
    internal const string Statistics = "Ctrl+4";
    internal const string Settings = "Ctrl+5 / Ctrl+,";
    internal const string Update = "Ctrl+Shift+U";
    internal const string Notifications = "Alt+N";
    internal const string Widget = "Ctrl+Shift+W";
    internal const string More = "Alt+M";
    internal const string Minimize = "Ctrl+Alt+Down";
    internal const string Maximize = "Ctrl+Alt+Up";
    internal const string Close = "Alt+F4";

    internal static TitleBarShortcutAction ResolveTitleBarAction(Key key, ModifierKeys modifiers) =>
        (modifiers, key) switch
        {
            (ModifierKeys.None, Key.F1) => TitleBarShortcutAction.ToggleAbout,
            (ModifierKeys.Alt, Key.M) => TitleBarShortcutAction.ToggleMoreMenu,
            (ModifierKeys.Alt, Key.N) => TitleBarShortcutAction.OpenNotifications,
            (ModifierKeys.Alt, Key.F4) => TitleBarShortcutAction.CloseWindow,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.U) => TitleBarShortcutAction.CheckOrApplyUpdate,
            (ModifierKeys.Control | ModifierKeys.Shift, Key.W) => TitleBarShortcutAction.ToggleWidget,
            (ModifierKeys.Control | ModifierKeys.Alt, Key.Down) => TitleBarShortcutAction.MinimizeWindow,
            (ModifierKeys.Control | ModifierKeys.Alt, Key.Up) => TitleBarShortcutAction.MaximizeOrRestoreWindow,
            (ModifierKeys.Control, Key.D1 or Key.NumPad1) => TitleBarShortcutAction.SelectFirewall,
            (ModifierKeys.Control, Key.D2 or Key.NumPad2 or Key.H) => TitleBarShortcutAction.SelectTraffic,
            (ModifierKeys.Control, Key.D3 or Key.NumPad3 or Key.L) => TitleBarShortcutAction.SelectConnections,
            (ModifierKeys.Control, Key.D4 or Key.NumPad4 or Key.I) => TitleBarShortcutAction.SelectStatistics,
            (ModifierKeys.Control, Key.D5 or Key.NumPad5 or Key.OemComma) => TitleBarShortcutAction.OpenSettings,
            _ => TitleBarShortcutAction.None
        };
}

