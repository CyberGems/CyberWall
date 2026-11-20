using CyberWall.Common;

namespace CyberWall.Tests;

public class TrayTooltipTextTests
{
    [Fact]
    public void FormatUsesTheSuiteSeparatorWithOneSegment()
    {
        Assert.Equal("CyberWall · Protection active", TrayTooltipText.Format("Protection active"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FormatWithoutSegmentFallsBackToTheAppName(string? segment)
    {
        Assert.Equal("CyberWall", TrayTooltipText.Format(segment));
    }

    [Fact]
    public void FormatTrimsTheSegmentBeforeFormatting()
    {
        Assert.Equal("CyberWall · Protection off", TrayTooltipText.Format("  Protection off  "));
    }

    [Fact]
    public void FormatCapsTheTooltipAt63CharactersWithAnEllipsis()
    {
        string tooltip = TrayTooltipText.Format(new string('X', 80));

        Assert.Equal(63, tooltip.Length);
        Assert.EndsWith("…", tooltip);
        Assert.StartsWith("CyberWall · ", tooltip);
    }

    [Fact]
    public void FormatKeepsShortTooltipsUntouched()
    {
        string tooltip = TrayTooltipText.Format("Protection active (Windows service)");

        Assert.Equal("CyberWall · Protection active (Windows service)", tooltip);
        Assert.DoesNotContain("…", tooltip);
    }
}
