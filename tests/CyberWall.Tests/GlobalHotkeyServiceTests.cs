using System.Windows.Input;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.Tests;

public class GlobalHotkeyServiceTests
{
    [Fact]
    public void TheDefaultGlobalHotkeyIsAltShiftW()
    {
        Assert.Equal("Alt+Shift+W", new AppSettings().GlobalHotkey);
    }

    [Fact]
    public void TheDefaultWidgetHotkeyIsAltShiftF2()
    {
        Assert.Equal("Alt+Shift+F2", new AppSettings().WidgetHotkey);
    }

    [Fact]
    public void HotkeyIdsAreDistinct()
    {
        Assert.NotEqual(GlobalHotkeyService.MainHotkeyId, GlobalHotkeyService.WidgetHotkeyId);
    }

    [Fact]
    public void TheDefaultWidgetHotkeyParsesIntoF2()
    {
        Assert.True(GlobalHotkeyService.TryParseHotkey(new AppSettings().WidgetHotkey, out var modifiers, out var vk));
        Assert.NotEqual(0u, modifiers);
        Assert.Equal(0x71u, vk);
    }

    [Fact]
    public void TheDefaultHotkeyParsesIntoModifiersAndAKey()
    {
        Assert.True(GlobalHotkeyService.TryParseHotkey(new AppSettings().GlobalHotkey, out var modifiers, out var vk));
        Assert.NotEqual(0u, modifiers);
        Assert.NotEqual(0u, vk);
    }

    [Fact]
    public void FunctionKeysAreAccepted()
    {
        Assert.True(GlobalHotkeyService.TryParseHotkey("Alt+Shift+F1", out _, out var vk));
        Assert.Equal(0x70u, vk);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Ctrl+Alt")]
    [InlineData("Alt+Shift+NotAKey")]
    public void IncompleteOrInvalidStringsAreRejected(string? value)
    {
        Assert.False(GlobalHotkeyService.TryParseHotkey(value, out _, out _));
    }

    [Fact]
    public void WhitespaceAroundTokensIsIgnored()
    {
        Assert.True(GlobalHotkeyService.TryParseHotkey("Ctrl + Alt + M", out _, out _));
    }

    [Fact]
    public void FormattingUsesTheStoredNotation()
    {
        Assert.Equal(
            "Alt + Shift + F1",
            GlobalHotkeyService.FormatHotkey(ModifierKeys.Alt | ModifierKeys.Shift, Key.F1));
    }

    [Theory]
    [InlineData("Alt+Shift+W", "Alt + Shift + W")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void StoredShortcutsAreShownWithSpaces(string? stored, string expected)
    {
        Assert.Equal(expected, GlobalHotkeyService.FormatDisplay(stored));
    }
}
