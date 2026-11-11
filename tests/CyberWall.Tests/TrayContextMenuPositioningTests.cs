using System.Drawing;
using CyberWall.UI.Tray;

namespace CyberWall.Tests;

public class TrayContextMenuPositioningTests
{
    [Fact]
    public void PlacesBottomTaskbarMenuOnNegativeXMonitorInPhysicalPixels()
    {
        var bounds = new Rectangle(-1920, 0, 1920, 1080);
        var workArea = new Rectangle(-1920, 0, 1920, 1020);
        var click = new Point(-180, 1050);
        const int physW = 450; // 300 DIP * 1.5
        const int physH = 540; // 360 DIP * 1.5

        var (leftPx, topPx) = TrayContextMenuWindow.ComputeTargetPhysicalOrigin(
            workArea, bounds, click, physW, physH, scaleX: 1.5, scaleY: 1.5);

        // Right edge clamps to workArea.Right (0) when click is near the right edge of the secondary monitor.
        Assert.Equal(-physW, leftPx);
        Assert.Equal(workArea.Bottom - physH, topPx);
    }

    [Fact]
    public void PlacesMenuRespectingLeftRightAndTopTaskbarEdges()
    {
        var bounds = new Rectangle(0, 0, 1920, 1080);
        const int physW = 375;
        const int physH = 450;

        // Left taskbar (x: 0..80)
        var leftWork = new Rectangle(80, 0, 1840, 1080);
        var leftPos = TrayContextMenuWindow.ComputeTargetPhysicalOrigin(
            leftWork, bounds, new Point(40, 600), physW, physH, scaleX: 1.25, scaleY: 1.25);
        Assert.Equal((80, 600 - physH / 2), leftPos);

        // Right taskbar (x: 1840..1920)
        var rightWork = new Rectangle(0, 0, 1840, 1080);
        var rightPos = TrayContextMenuWindow.ComputeTargetPhysicalOrigin(
            rightWork, bounds, new Point(1880, 600), physW, physH, scaleX: 1.25, scaleY: 1.25);
        Assert.Equal((1840 - physW, 600 - physH / 2), rightPos);

        // Top taskbar (y: 0..60)
        var topWork = new Rectangle(0, 60, 1920, 1020);
        var topPos = TrayContextMenuWindow.ComputeTargetPhysicalOrigin(
            topWork, bounds, new Point(1200, 30), physW, physH, scaleX: 1.25, scaleY: 1.25);
        Assert.Equal((1200 - physW / 2, 60), topPos);
    }

    [Fact]
    public void CentersWhenWindowExceedsWorkAreaBounds()
    {
        var bounds = new Rectangle(0, 0, 400, 400);
        var workArea = new Rectangle(0, 0, 400, 360);

        var (leftPx, topPx) = TrayContextMenuWindow.ComputeTargetPhysicalOrigin(
            workArea, bounds, new Point(200, 380), windowWidthPx: 450, windowHeightPx: 500, scaleX: 1.5, scaleY: 1.5);

        Assert.Equal(-25, leftPx);
        Assert.Equal(-70, topPx);
    }
}

