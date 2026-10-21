using CyberWall.Service.Wfp;

namespace CyberWall.Tests;

/// <summary>
/// Turning the protection off must clean up the rules CyberWall created, but it must
/// not remove the core service rules: they are the ones that keep the local traffic
/// (DNS, DHCP, NTP and 127.0.0.1) working for the applications that rely on it.
/// The recovery script keeps them for the same reason.
/// </summary>
public class FirewallRuleCleanupTests
{
    [Theory]
    [InlineData("CyberWall-Allow-Core-Loopback4-In")]
    [InlineData("CyberWall-Allow-Core-Loopback4-Out")]
    [InlineData("CyberWall-Allow-Core-Loopback6-In")]
    [InlineData("CyberWall-Allow-Core-DNS-UDP")]
    [InlineData("CyberWall-Allow-Core-NTP-Out")]
    public void TheCoreServiceRulesArePreserved(string name)
    {
        Assert.False(RealFirewall.IsRemovableOwnedRule(name));
    }

    [Theory]
    [InlineData("CyberWall-Allow-chrome~a1b2c3d4")]
    [InlineData("CyberWall-Block-git~00112233")]
    [InlineData("CyberWall-Block-Pkg-Microsoft.WindowsStore_8wekyb3d8bbwe")]
    [InlineData("CyberWall-Killswitch-BlockAll-Out")]
    public void TheApplicationRulesAreRemoved(string name)
    {
        Assert.True(RealFirewall.IsRemovableOwnedRule(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Windows-Some-Other-Rule")]
    [InlineData("CyberWallish-Rule")]
    public void RulesThatDoNotBelongToCyberWallAreLeftAlone(string? name)
    {
        Assert.False(RealFirewall.IsRemovableOwnedRule(name));
    }

    [Fact]
    public void TheCoreRuleMarkerIsMatchedRegardlessOfTheCase()
    {
        Assert.False(RealFirewall.IsRemovableOwnedRule("CyberWall-Allow-CORE-DNS-TCP"));
    }
}
