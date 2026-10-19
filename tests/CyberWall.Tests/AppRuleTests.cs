using CyberWall.Common.Models;

namespace CyberWall.Tests;

public class AppRuleTests
{
    [Fact]
    public void NormalizeReturnsTheFullLowercasePath()
    {
        var normalized = AppRule.Normalize(@"C:\Program Files\App\App.exe");
        Assert.Equal(@"c:\program files\app\app.exe", normalized);
    }

    [Fact]
    public void NormalizeIsStableForTheSamePath()
    {
        var a = AppRule.Normalize(@"C:\Apps\Demo.exe");
        var b = AppRule.Normalize(@"c:\apps\demo.exe");
        Assert.Equal(a, b);
    }

    [Fact]
    public void EffectiveVerdictsFallBackToTheOverallVerdictForBothDirections()
    {
        var rule = new AppRule { AppPath = @"C:\Apps\Demo.exe", Verdict = Verdict.Allow, Direction = Direction.Both };
        Assert.Equal(Verdict.Allow, rule.EffectiveInboundVerdict);
        Assert.Equal(Verdict.Allow, rule.EffectiveOutboundVerdict);
    }

    [Fact]
    public void EffectiveVerdictsFallBackToBlockForTheDirectionNotCovered()
    {
        var rule = new AppRule { AppPath = @"C:\Apps\Demo.exe", Verdict = Verdict.Allow, Direction = Direction.Inbound };
        Assert.Equal(Verdict.Allow, rule.EffectiveInboundVerdict);
        Assert.Equal(Verdict.Block, rule.EffectiveOutboundVerdict);
    }

    [Fact]
    public void PerDirectionVerdictsTakePrecedence()
    {
        var rule = new AppRule
        {
            AppPath = @"C:\Apps\Demo.exe",
            Verdict = Verdict.Block,
            Direction = Direction.Both,
            InboundVerdict = Verdict.Allow,
            OutboundVerdict = Verdict.Block,
        };
        Assert.Equal(Verdict.Allow, rule.EffectiveInboundVerdict);
        Assert.Equal(Verdict.Block, rule.EffectiveOutboundVerdict);
    }

    [Theory]
    [InlineData(Direction.Inbound)]
    [InlineData(Direction.Outbound)]
    public void GetVerdictForReturnsTheVerdictOfTheDirection(Direction direction)
    {
        var rule = new AppRule
        {
            AppPath = @"C:\Apps\Demo.exe",
            Verdict = Verdict.Block,
            Direction = Direction.Both,
            InboundVerdict = Verdict.Allow,
            OutboundVerdict = Verdict.Block,
        };

        var expected = direction == Direction.Inbound ? Verdict.Allow : Verdict.Block;
        Assert.Equal(expected, rule.GetVerdictFor(direction));
    }

    [Fact]
    public void GetVerdictForBothReturnsAllowWhenAnyDirectionIsAllowed()
    {
        var rule = new AppRule
        {
            AppPath = @"C:\Apps\Demo.exe",
            Verdict = Verdict.Block,
            Direction = Direction.Both,
            InboundVerdict = Verdict.Allow,
            OutboundVerdict = Verdict.Block,
        };
        Assert.Equal(Verdict.Allow, rule.GetVerdictFor(Direction.Both));
    }
}
