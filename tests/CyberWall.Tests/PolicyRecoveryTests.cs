using System.Xml.Linq;
using CyberWall.Service.Wfp;

namespace CyberWall.Tests;

/// <summary>
/// The recovery watchdog restores the firewall policy when CyberWall dies. Its task
/// definition and its script are validated here, because a broken definition would
/// silently leave the machine in default-deny without any way to recover.
/// </summary>
public class PolicyRecoveryTests
{
    [Fact]
    public void TheTaskDefinitionIsValidXml()
    {
        var xml = PolicyRecovery.BuildTaskXml();
        var document = XDocument.Parse(xml);

        Assert.Equal("Task", document.Root!.Name.LocalName);
        Assert.Equal("http://schemas.microsoft.com/windows/2004/02/mit/task", document.Root.Name.NamespaceName);
    }

    [Fact]
    public void TheTaskRunsAsSystemWithTheHighestPrivileges()
    {
        var document = XDocument.Parse(PolicyRecovery.BuildTaskXml());
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        var principal = document.Descendants(ns + "UserId").Single().Value;
        Assert.Equal("S-1-5-18", principal);

        var runLevel = document.Descendants(ns + "RunLevel").Single().Value;
        Assert.Equal("HighestAvailable", runLevel);
    }

    [Fact]
    public void TheTaskRepeatsEveryTwoMinutes()
    {
        var document = XDocument.Parse(PolicyRecovery.BuildTaskXml());
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        var interval = document.Descendants(ns + "Interval").Single().Value;
        Assert.Equal("PT2M", interval);
    }

    [Fact]
    public void TheTaskPointsToTheRecoveryScript()
    {
        var document = XDocument.Parse(PolicyRecovery.BuildTaskXml());
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";

        var command = document.Descendants(ns + "Command").Single().Value;
        Assert.Equal("powershell.exe", command);

        var arguments = document.Descendants(ns + "Arguments").Single().Value;
        Assert.Contains("recovery.ps1", arguments);
        Assert.Contains("-NonInteractive", arguments);
    }

    [Fact]
    public void TheRecoveryScriptRestoresTheFirewallPolicy()
    {
        var script = PolicyRecovery.BuildRecoveryScript();
        Assert.Contains("advfirewall set allprofiles firewallpolicy blockinbound,allowoutbound", script);
    }

    [Fact]
    public void TheRecoveryScriptRemovesTheRulesLeftBehind()
    {
        var script = PolicyRecovery.BuildRecoveryScript();
        Assert.Contains("Get-NetFirewallRule -DisplayName 'CyberWall-Block-*' | Remove-NetFirewallRule", script);
        Assert.Contains("Get-NetFirewallRule -DisplayName 'CyberWall-Killswitch-*' | Remove-NetFirewallRule", script);
    }

    [Fact]
    public void TheRecoveryScriptDoesNothingWhileTheEngineIsAlive()
    {
        var script = PolicyRecovery.BuildRecoveryScript();
        Assert.Contains("if (Test-Path $heartbeat) {", script);
        Assert.Contains("exit 0", script);
    }

    [Fact]
    public void TheRecoveryScriptRestoresTheAuditPolicyOnlyWhenCyberWallChangedIt()
    {
        var script = PolicyRecovery.BuildRecoveryScript();
        Assert.Contains("if (Test-Path $auditMarker) {", script);
        Assert.Contains("auditpol /set", script);
    }
}
