using CyberWall.Service.Wfp;

namespace CyberWall.Tests;

/// <summary>
/// Windows Firewall rule names must be unique per executable. Before the rule names
/// carried a path hash, two programs with the same file name (chrome.exe, setup.exe...)
/// replaced each other's rule.
/// </summary>
public class FirewallRuleNameTests
{
    [Fact]
    public void TwoProgramsWithTheSameFileNameGetDifferentTags()
    {
        var first = RealFirewall.RuleTag(@"C:\Program Files\Browser\chrome.exe");
        var second = RealFirewall.RuleTag(@"C:\Users\test\AppData\Local\Browser\chrome.exe");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TheSameProgramAlwaysGetsTheSameTag()
    {
        var first = RealFirewall.RuleTag(@"C:\Program Files\Browser\chrome.exe");
        var second = RealFirewall.RuleTag(@"c:\program files\browser\CHROME.EXE");

        Assert.Equal(first, second);
    }

    [Fact]
    public void TheTagIsStableAcrossCalls()
    {
        var path = @"C:\Program Files\Browser\chrome.exe";
        Assert.Equal(RealFirewall.RuleTag(path), RealFirewall.RuleTag(path));
    }

    [Fact]
    public void TheTagIsEightHexCharacters()
    {
        var tag = RealFirewall.RuleTag(@"C:\Program Files\Browser\chrome.exe");
        Assert.Equal(8, tag.Length);
        Assert.All(tag, c => Assert.True(char.IsAsciiDigit(c) || (c >= 'a' && c <= 'f')));
    }

    [Fact]
    public void DifferentProgramsProduceDifferentRuleNames()
    {
        var a = RealFirewall.RuleTag(@"C:\Apps\One\update.exe");
        var b = RealFirewall.RuleTag(@"C:\Apps\Two\update.exe");

        Assert.NotEqual($"CyberWall-Block-update~{a}", $"CyberWall-Block-update~{b}");
    }
}
