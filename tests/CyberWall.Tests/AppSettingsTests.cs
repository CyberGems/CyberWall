using System.Text.Json;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;

namespace CyberWall.Tests;

public class AppSettingsTests
{
    [Fact]
    public void TheFirewallModeIsStoredByName()
    {
        var settings = new AppSettings { FirewallMode = FirewallMode.Killswitch };
        var json = JsonSerializer.Serialize(settings);
        Assert.Contains("\"Killswitch\"", json);
    }

    [Fact]
    public void TheFirewallModeIsReadFromItsName()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"FirewallMode":"BlockAll"}""");
        Assert.NotNull(settings);
        Assert.Equal(FirewallMode.BlockAll, settings!.FirewallMode);
    }

    [Fact]
    public void TheFirewallModeIsReadFromItsNameIgnoringTheCase()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"FirewallMode":"killswitch"}""");
        Assert.NotNull(settings);
        Assert.Equal(FirewallMode.Killswitch, settings!.FirewallMode);
    }

    [Theory]
    [InlineData(0, FirewallMode.Disabled)]
    [InlineData(1, FirewallMode.BlockAll)]
    [InlineData(2, FirewallMode.Ask)]
    [InlineData(3, FirewallMode.Killswitch)]
    public void LegacyFilesWithTheNumericModeAreStillRead(int legacy, FirewallMode expected)
    {
        var json = "{\"FirewallMode\":" + legacy + "}";
        var settings = JsonSerializer.Deserialize<AppSettings>(json);
        Assert.NotNull(settings);
        Assert.Equal(expected, settings!.FirewallMode);
    }

    [Fact]
    public void AnUnknownModeFallsBackToAsk()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"FirewallMode":"NotAMode"}""");
        Assert.NotNull(settings);
        Assert.Equal(FirewallMode.Ask, settings!.FirewallMode);
    }

    [Fact]
    public void TheDefaultModeIsAsk()
    {
        Assert.Equal(FirewallMode.Ask, new AppSettings().FirewallMode);
    }

    [Fact]
    public void UnansweredPromptsAreNotBlockedByDefault()
    {
        Assert.False(new AppSettings().PopupAutoBlockEnabled);
    }

    [Fact]
    public void LegacySettingsDisableAutoBlockOnce()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"PopupAutoBlockEnabled":true}""");
        Assert.NotNull(settings);
        Assert.True(settings!.PopupAutoBlockEnabled);

        Assert.True(AppSettings.ApplyManualBlockDefault(settings));
        Assert.False(settings.PopupAutoBlockEnabled);
        Assert.True(settings.ManualBlockDefaultApplied);

        Assert.False(AppSettings.ApplyManualBlockDefault(settings));
        Assert.False(settings.PopupAutoBlockEnabled);
    }

    [Fact]
    public void ASavedAutoBlockChoiceIsRespected()
    {
        var settings = JsonSerializer.Deserialize<AppSettings>("""{"PopupAutoBlockEnabled":true,"ManualBlockDefaultApplied":true}""");
        Assert.NotNull(settings);
        Assert.False(AppSettings.ApplyManualBlockDefault(settings!));
        Assert.True(settings!.PopupAutoBlockEnabled);
    }
}
