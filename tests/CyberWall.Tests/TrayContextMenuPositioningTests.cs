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

    [Fact]
    public void AnchoredOriginPreventsJumpingAndClippingOnVerticalTaskbar()
    {
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var leftWork = new Rectangle(80, 0, 1840, 1080); // left taskbar
        var click = new Point(40, 950); // tray icon near bottom of taskbar

        const int physW = 375;
        const int mainPhysH = 550;
        const int subPhysH = 300;

        // 1. Initial open with Main View
        var (initLeft, initTop) = TrayContextMenuWindow.ComputeTargetPhysicalOrigin(
            leftWork, bounds, click, physW, mainPhysH, scaleX: 1.25, scaleY: 1.25);

        int anchorLeft = initLeft;
        int anchorTop = initTop;
        int anchorBottom = initTop + mainPhysH;

        // On left taskbar, bottom must not exceed 1080
        Assert.True(initTop + mainPhysH <= 1080);
        Assert.Equal(80, initLeft);

        // 2. Switch to shorter submenu (Pause alerts or Help)
        var (subLeft, subTop) = TrayContextMenuWindow.ComputeAnchoredOrigin(
            leftWork, anchorLeft, anchorTop, anchorBottom, physW, subPhysH, isTaskbarBottom: false);

        // Top position stays stable so header/< Back does not jump away from cursor
        Assert.Equal(anchorTop, subTop);
        Assert.Equal(anchorLeft, subLeft);
        Assert.True(subTop + subPhysH <= 1080);

        // 3. Switch back to Main view
        var (returnLeft, returnTop) = TrayContextMenuWindow.ComputeAnchoredOrigin(
            leftWork, anchorLeft, anchorTop, anchorBottom, physW, mainPhysH, isTaskbarBottom: false);

        // Top position must be identical to the initial open (zero jump, zero off-screen clip)
        Assert.Equal(initTop, returnTop);
        Assert.Equal(initLeft, returnLeft);
        Assert.True(returnTop + mainPhysH <= 1080);
    }

    [Fact]
    public void AnchoredOriginKeepsBottomFlushOnBottomTaskbar()
    {
        var bounds = new Rectangle(0, 0, 1920, 1080);
        var bottomWork = new Rectangle(0, 0, 1920, 1040); // bottom taskbar
        var click = new Point(1750, 1060);

        const int physW = 375;
        const int mainPhysH = 550;
        const int subPhysH = 300;

        // 1. Initial open with Main View
        var (initLeft, initTop) = TrayContextMenuWindow.ComputeTargetPhysicalOrigin(
            bottomWork, bounds, click, physW, mainPhysH, scaleX: 1.25, scaleY: 1.25);

        int anchorLeft = initLeft;
        int anchorTop = initTop;
        int anchorBottom = initTop + mainPhysH;

        Assert.Equal(1040, anchorBottom);
        Assert.Equal(1040 - mainPhysH, initTop);

        // 2. Switch to shorter submenu
        var (subLeft, subTop) = TrayContextMenuWindow.ComputeAnchoredOrigin(
            bottomWork, anchorLeft, anchorTop, anchorBottom, physW, subPhysH, isTaskbarBottom: true);

        // Bottom remains flush with taskbar
        Assert.Equal(1040 - subPhysH, subTop);
        Assert.Equal(1040, subTop + subPhysH);
        Assert.Equal(initLeft, subLeft);

        // 3. Switch back to Main view
        var (returnLeft, returnTop) = TrayContextMenuWindow.ComputeAnchoredOrigin(
            bottomWork, anchorLeft, anchorTop, anchorBottom, physW, mainPhysH, isTaskbarBottom: true);

        Assert.Equal(initTop, returnTop);
        Assert.Equal(1040, returnTop + mainPhysH);
    }
}

