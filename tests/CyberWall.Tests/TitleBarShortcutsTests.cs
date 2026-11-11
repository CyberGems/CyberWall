using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CyberWall.UI.Controls;

namespace CyberWall.Tests;

public class TitleBarShortcutsTests
{
    [Theory]
    [InlineData(Key.F1, ModifierKeys.None, TitleBarShortcutAction.ToggleAbout)]
    [InlineData(Key.D1, ModifierKeys.Control, TitleBarShortcutAction.SelectFirewall)]
    [InlineData(Key.NumPad1, ModifierKeys.Control, TitleBarShortcutAction.SelectFirewall)]
    [InlineData(Key.D2, ModifierKeys.Control, TitleBarShortcutAction.SelectTraffic)]
    [InlineData(Key.NumPad2, ModifierKeys.Control, TitleBarShortcutAction.SelectTraffic)]
    [InlineData(Key.H, ModifierKeys.Control, TitleBarShortcutAction.SelectTraffic)]
    [InlineData(Key.D3, ModifierKeys.Control, TitleBarShortcutAction.SelectConnections)]
    [InlineData(Key.NumPad3, ModifierKeys.Control, TitleBarShortcutAction.SelectConnections)]
    [InlineData(Key.L, ModifierKeys.Control, TitleBarShortcutAction.SelectConnections)]
    [InlineData(Key.D4, ModifierKeys.Control, TitleBarShortcutAction.SelectStatistics)]
    [InlineData(Key.NumPad4, ModifierKeys.Control, TitleBarShortcutAction.SelectStatistics)]
    [InlineData(Key.I, ModifierKeys.Control, TitleBarShortcutAction.SelectStatistics)]
    [InlineData(Key.D5, ModifierKeys.Control, TitleBarShortcutAction.OpenSettings)]
    [InlineData(Key.NumPad5, ModifierKeys.Control, TitleBarShortcutAction.OpenSettings)]
    [InlineData(Key.OemComma, ModifierKeys.Control, TitleBarShortcutAction.OpenSettings)]
    [InlineData(Key.U, ModifierKeys.Control | ModifierKeys.Shift, TitleBarShortcutAction.CheckOrApplyUpdate)]
    [InlineData(Key.N, ModifierKeys.Alt, TitleBarShortcutAction.OpenNotifications)]
    [InlineData(Key.W, ModifierKeys.Control | ModifierKeys.Shift, TitleBarShortcutAction.ToggleWidget)]
    [InlineData(Key.M, ModifierKeys.Alt, TitleBarShortcutAction.ToggleMoreMenu)]
    [InlineData(Key.Down, ModifierKeys.Control | ModifierKeys.Alt, TitleBarShortcutAction.MinimizeWindow)]
    [InlineData(Key.Up, ModifierKeys.Control | ModifierKeys.Alt, TitleBarShortcutAction.MaximizeOrRestoreWindow)]
    [InlineData(Key.F4, ModifierKeys.Alt, TitleBarShortcutAction.CloseWindow)]
    public void MapsAllTitleBarShortcutsAndAliases(Key key, ModifierKeys modifiers, TitleBarShortcutAction expected)
    {
        Assert.Equal(expected, KeyboardShortcuts.ResolveTitleBarAction(key, modifiers));
    }

    [Theory]
    [InlineData(Key.M, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.N, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData(Key.M, ModifierKeys.None)]
    [InlineData(Key.W, ModifierKeys.Alt | ModifierKeys.Shift)]
    [InlineData(Key.U, ModifierKeys.Control)]
    [InlineData(Key.C, ModifierKeys.Control | ModifierKeys.Shift)]
    public void IgnoresUnmappedAndAltGrKeyCombinations(Key key, ModifierKeys modifiers)
    {
        Assert.Equal(TitleBarShortcutAction.None, KeyboardShortcuts.ResolveTitleBarAction(key, modifiers));
    }

    [Fact]
    public void BuildsKeycapToolTipContentMatchingSuitePattern()
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                var content = HotkeyToolTipContent.WithShortcut("Más opciones", KeyboardShortcuts.More);
                var panel = Assert.IsType<StackPanel>(content);
                Assert.Equal(Orientation.Horizontal, panel.Orientation);
                Assert.Equal(2, panel.Children.Count);

                var label = Assert.IsType<TextBlock>(panel.Children[0]);
                Assert.Equal("Más opciones", label.Text);

                var badge = Assert.IsType<Border>(panel.Children[1]);
                var shortcut = Assert.IsType<TextBlock>(badge.Child);
                Assert.Equal("Alt+M", shortcut.Text);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured != null)
        {
            ExceptionDispatchInfo.Capture(captured).Throw();
        }
    }
}

