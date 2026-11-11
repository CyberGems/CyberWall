using CyberWall.Common.Models;
using CyberWall.UI;

namespace CyberWall.Tests;

public class FloatingWidgetStateTests
{
    [Fact]
    public void TreatsWidgetAsActiveWhileStartupEnableIsRunning()
    {
        var (active, mode) = MainWindow.ResolveWidgetFirewallState(
            isEnabling: true,
            ownedByAnotherHost: false,
            isMasterOn: false,
            configuredMode: FirewallMode.Killswitch,
            serviceMode: FirewallMode.Ask);

        Assert.True(active);
        Assert.Equal(FirewallMode.Killswitch, mode);
    }

    [Fact]
    public void ReportsActiveWhenWindowsServiceOwnsFirewallEngine()
    {
        var (active, mode) = MainWindow.ResolveWidgetFirewallState(
            isEnabling: false,
            ownedByAnotherHost: true,
            isMasterOn: false,
            configuredMode: FirewallMode.Ask,
            serviceMode: FirewallMode.BlockAll);

        Assert.True(active);
        Assert.Equal(FirewallMode.BlockAll, mode);
    }

    [Theory]
    [InlineData(true, FirewallMode.Ask)]
    [InlineData(false, FirewallMode.BlockAll)]
    [InlineData(true, FirewallMode.Killswitch)]
    public void MirrorsLocalEngineStateOnceReady(bool masterOn, FirewallMode serviceMode)
    {
        var (active, mode) = MainWindow.ResolveWidgetFirewallState(
            isEnabling: false,
            ownedByAnotherHost: false,
            isMasterOn: masterOn,
            configuredMode: FirewallMode.Ask,
            serviceMode: serviceMode);

        Assert.Equal(masterOn, active);
        Assert.Equal(serviceMode, mode);
    }
}

