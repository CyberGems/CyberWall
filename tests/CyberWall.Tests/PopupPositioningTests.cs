using System.Drawing;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.Tests;

public class PopupPositioningTests
{
    [Theory]
    [InlineData(PopupPosition.TopRight, 30)]
    [InlineData(PopupPosition.Right, 435)]
    [InlineData(PopupPosition.BottomRight, 840)]
    public void AlignsRightSidePopupsAccuratelyOnNegativeXMonitorWith150PercentDpi(PopupPosition position, int expectedTopPx)
    {
        // Left monitor at 150% DPI while primary is 125%: physical work area ends at X = 0.
        var workArea = new Rectangle(-1811, 0, 1811, 1080);
        const int physW = 646;
        const int physH = 210;

        var (leftPx, topPx) = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scaleX: 1.5, scaleY: 1.5, position, stackOffsetPx: 0);

        // 20 DIP margin * 1.5 = 30 physical pixels from workArea.Right (0).
        Assert.Equal(-30 - physW, leftPx);
        Assert.Equal(workArea.Right - 30, leftPx + physW);
        Assert.Equal(expectedTopPx, topPx);
    }

    [Fact]
    public void ComputesAllPositionsAndStackOffsetsConsistentlyInPhysicalPixels()
    {
        var workArea = new Rectangle(1920, -1080, 2560, 1440);
        const int physW = 860;
        const int physH = 300;
        const double scale = 2.0; // margin = 40px
        const int stackPx = 240;

        var topLeft = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.TopLeft, stackPx);
        Assert.Equal((1920 + 40, -1080 + 40 + stackPx), topLeft);

        var topCenter = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.TopCenter, stackPx);
        Assert.Equal((1920 + (2560 - physW) / 2, -1080 + 40 + stackPx), topCenter);

        var bottomLeft = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.BottomLeft, stackPx);
        Assert.Equal((1920 + 40, (-1080 + 1440) - physH - 40 - stackPx), bottomLeft);

        var bottomCenter = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.BottomCenter, stackPx);
        Assert.Equal((1920 + (2560 - physW) / 2, (-1080 + 1440) - physH - 40 - stackPx), bottomCenter);

        var bottomRight = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.BottomRight, stackPx);
        Assert.Equal((1920 + 2560 - physW - 40, (-1080 + 1440) - physH - 40 - stackPx), bottomRight);

        var left = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scale, scale, PopupPosition.Left, 0);
        Assert.Equal((1920 + 40, -1080 + (1440 - physH) / 2), left);
    }

    [Fact]
    public void ClampsPopupInsideWorkAreaWhenStackExceedsMonitorBounds()
    {
        var workArea = new Rectangle(-1811, 0, 1811, 1080);
        const int physW = 646;
        const int physH = 300;

        var (leftPx, topPx) = PopupWindowHelper.ComputeTargetPhysicalOrigin(
            workArea, physW, physH, scaleX: 1.5, scaleY: 1.5, PopupPosition.BottomRight, stackOffsetPx: 2000);

        Assert.Equal(-30 - physW, leftPx);
        Assert.Equal(workArea.Top, topPx);
    }
}

