using CyberWall.Service.Wfp;

namespace CyberWall.Tests;

/// <summary>
/// The firewall policy has to come back to a usable state when CyberWall stops.
/// These tests pin the decision that is easy to get wrong: a machine that was left
/// in the default-deny state by an unclean shutdown must not be restored to it.
/// </summary>
public class FirewallPolicyRestoreTests
{
    [Fact]
    public void TheOutboundActionIsAlwaysRestoredToAllow()
    {
        var (outbound, _) = RealFirewall.ResolveRestoredActions(capturedOutbound: 2, capturedInbound: 2);
        Assert.Equal(1, outbound);
    }

    [Fact]
    public void ABlockedOutboundStateIsNeverRestored()
    {
        // 2 = Block. This is the state CyberWall applies itself, so restoring it would
        // leave the host without connectivity.
        var (outbound, _) = RealFirewall.ResolveRestoredActions(capturedOutbound: 2, capturedInbound: 2);
        Assert.NotEqual(2, outbound);
    }

    [Fact]
    public void TheCapturedInboundActionIsRestored()
    {
        var (_, inbound) = RealFirewall.ResolveRestoredActions(capturedOutbound: 1, capturedInbound: 1);
        Assert.Equal(1, inbound);
    }

    [Fact]
    public void WithoutACapturedInboundActionTheWindowsDefaultIsUsed()
    {
        var (_, inbound) = RealFirewall.ResolveRestoredActions(capturedOutbound: null, capturedInbound: null);
        Assert.Equal(2, inbound); // Block, the Windows default
    }
}
