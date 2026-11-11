using System.Drawing;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.Tests;

public class PopupPositioningTests
{
    [Theory]
    [InlineData(PopupPosition.TopRight, 12)]
    [InlineData(PopupPosition.Right, 435)]
    [InlineData(PopupPosition.BottomRight, 858)]
    public void AlignsRightSidePopupsAccuratelyOnNegativeXMonitorWith150PercentDpi(PopupPosition position, int expectedTopPx)
    {
        // Left monitor at 150% DPI while primary is 125%: physical work area ends at X = 0.
        var workArea = new Rectangle(-1811, 0, 1811, 1080);
        const int physW = 682;
        const int physH = 210;

        var (leftPx, topPx) = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scaleX: 1.5, scaleY: 1.5, position, stackOffsetPx: 0);

        // 8 DIP outer margin * 1.5 = 12 physical pixels from workArea.Right (0), keeping visible card 28 DIPs inside.
        Assert.Equal(-12 - physW, leftPx);
        Assert.Equal(workArea.Right - 12, leftPx + physW);
        Assert.Equal(expectedTopPx, topPx);
    }

    [Fact]
    public void ComputesAllPositionsAndStackOffsetsConsistentlyInPhysicalPixels()
    {
        var workArea = new Rectangle(1920, -1080, 2560, 1440);
        const int physW = 908;
        const int physH = 300;
        const double scale = 2.0; // outer margin = 8 DIP * 2 = 16px
        const int stackPx = 240;

        var topLeft = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.TopLeft, stackPx);
        Assert.Equal((1920 + 16, -1080 + 16 + stackPx), topLeft);

        var topCenter = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.TopCenter, stackPx);
        Assert.Equal((1920 + (2560 - physW) / 2, -1080 + 16 + stackPx), topCenter);

        var bottomLeft = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.BottomLeft, stackPx);
        Assert.Equal((1920 + 16, (-1080 + 1440) - physH - 16 - stackPx), bottomLeft);

        var bottomCenter = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.BottomCenter, stackPx);
        Assert.Equal((1920 + (2560 - physW) / 2, (-1080 + 1440) - physH - 16 - stackPx), bottomCenter);

        var bottomRight = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.BottomRight, stackPx);
        Assert.Equal((1920 + 2560 - physW - 16, (-1080 + 1440) - physH - 16 - stackPx), bottomRight);

        var left = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.Left, 0);
        Assert.Equal((1920 + 16, -1080 + (1440 - physH) / 2), left);
    }

    [Fact]
    public void ClampsPopupInsideWorkAreaWhenStackExceedsMonitorBounds()
    {
        var workArea = new Rectangle(-1811, 0, 1811, 1080);
        const int physW = 682;
        const int physH = 300;

        var (leftPx, topPx) = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scaleX: 1.5, scaleY: 1.5, PopupPosition.BottomRight, stackOffsetPx: 2000);

        Assert.Equal(-12 - physW, leftPx);
        Assert.Equal(workArea.Top, topPx);
    }
}

