using CyberWall.Service.Wfp;

namespace CyberWall.Tests;

/// <summary>
/// The firewall policy has to come back to a usable state when CyberWall stops.
///
/// The order of the restore is what keeps the host connected: the reset that
/// covers every profile at once has to run before the per profile writes, and
/// the effective policy has to be read back before the rules are removed.
/// </summary>
public class FirewallPolicyRestoreOrderTests
{
    [Fact]
    public void TheResetThatCoversEveryProfileRunsBeforeTheApiWrites()
    {
        var steps = new List<string>();

        RealFirewall.RestoreProfilePolicy(
            () => { steps.Add("reset"); return true; },
            () => steps.Add("api"),
            () => true);

        Assert.Equal(new[] { "reset", "api" }, steps);
    }

    [Fact]
    public void AConfirmedPolicyNeedsASingleReset()
    {
        var resets = 0;

        var restored = RealFirewall.RestoreProfilePolicy(
            () => { resets++; return true; },
            () => { },
            () => true);

        Assert.True(restored);
        Assert.Equal(1, resets);
    }

    [Fact]
    public void AProfileThatStillBlocksOutboundIsResetAgain()
    {
        var steps = new List<string>();
        var readBacks = 0;

        var restored = RealFirewall.RestoreProfilePolicy(
            () => { steps.Add("reset"); return true; },
            () => steps.Add("api"),
            () => ++readBacks > 1);

        Assert.True(restored);
        Assert.Equal(new[] { "reset", "api", "reset", "api" }, steps);
        Assert.Equal(2, readBacks);
    }

    [Fact]
    public void AnOutboundPolicyThatCannotBeRestoredIsReported()
    {
        var restored = RealFirewall.RestoreProfilePolicy(
            () => true,
            () => { },
            () => false);

        // The caller keeps the application rules when the restore fails, so the
        // host is not left without any outbound connectivity.
        Assert.False(restored);
    }

    [Fact]
    public void TheApiWritesAreAppliedEvenWhenTheResetFails()
    {
        // The firewall API is the fallback for the outbound action when netsh is
        // not available, so it has to run regardless of the reset result.
        var apiApplied = false;

        RealFirewall.RestoreProfilePolicy(
            () => false,
            () => apiApplied = true,
            () => true);

        Assert.True(apiApplied);
    }
}
