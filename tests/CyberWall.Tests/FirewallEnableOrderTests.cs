using CyberWall.Service.Wfp;

namespace CyberWall.Tests;

/// <summary>
/// The default-deny policy has to start only after every allow rule is in place.
/// Switching the policy first cuts the connectivity of all the programs whose allow
/// rule has not been re-created yet, which reads as a temporary loss of the internet
/// connection on every start of the protection.
/// </summary>
public class FirewallEnableOrderTests
{
    [Fact]
    public void ThePreparationAndTheRulesRunBeforeThePolicySwitch()
    {
        var steps = new List<string>();

        RealFirewall.EnableDefaultDeny(
            () => { steps.Add("prepare"); return true; },
            () => steps.Add("rules"),
            () => { steps.Add("policy"); return true; });

        Assert.Equal(new[] { "prepare", "rules", "policy" }, steps);
    }

    [Fact]
    public void AFailedPreparationNeverSwitchesThePolicy()
    {
        var steps = new List<string>();

        var enabled = RealFirewall.EnableDefaultDeny(
            () => false,
            () => steps.Add("rules"),
            () => { steps.Add("policy"); return true; });

        // The policy stays as it was, so the host cannot end up in default-deny
        // without its allow rules.
        Assert.False(enabled);
        Assert.Empty(steps);
    }

    [Fact]
    public void AFailedPolicySwitchIsReported()
    {
        var enabled = RealFirewall.EnableDefaultDeny(
            () => true,
            () => { },
            () => false);

        Assert.False(enabled);
    }

    [Fact]
    public void ASuccessfulSwitchIsReported()
    {
        var enabled = RealFirewall.EnableDefaultDeny(
            () => true,
            () => { },
            () => true);

        Assert.True(enabled);
    }
}
