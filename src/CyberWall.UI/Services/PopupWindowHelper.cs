using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using CyberWall.Common.Settings;
using CyberWall.UI.Popup;

namespace CyberWall.UI.Services;

public static class PopupWindowHelper
{
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int GWL_EXSTYLE = -20;

    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int MDT_EFFECTIVE_DPI = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongA")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongA")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(System.Drawing.Point pt, uint dwFlags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, int dpiType, out uint dpiX, out uint dpiY);

    public static Screen[] GetSortedScreens()
    {
        return Screen.AllScreens
            .OrderByDescending(s => s.Primary)
            .ThenBy(s => s.Bounds.X)
            .ThenBy(s => s.Bounds.Y)
            .ToArray();
    }

    public static void ApplyNoActivateChrome(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TOOLWINDOW;
            exStyle |= WS_EX_NOACTIVATE;
            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
        }
        catch { }
    }

    public static void PositionPopup(Window popup, PopupPosition position, int monitorIndex = -1, int? explicitStackIndex = null)
    {
        PositionWindow(popup, position, monitorIndex, explicitStackIndex);
    }

    public static void PositionWindow(Window window, PopupPosition position, int monitorIndex = -1, int? explicitStackIndex = null)
    {
        var screens = GetSortedScreens();
        var screen = (monitorIndex >= 0 && monitorIndex < screens.Length)
            ? screens[monitorIndex]
            : (Screen.FromPoint(Cursor.Position) ?? screens.FirstOrDefault());
        if (screen == null) return;

        var phys = screen.WorkingArea;
        var (scaleX, scaleY) = GetMonitorScale(screen);
        // Each notification embeds a 20 DIP transparent shadow buffer, so -14 DIPs between outer HWNDs keeps a 26 DIP visible card gap.
        int gapPx = (int)Math.Round(-14 * scaleY);
        var hwnd = new WindowInteropHelper(window).Handle;

        if (hwnd != IntPtr.Zero)
        {
            EnsureWindowOnMonitorContext(window, hwnd, screen);

            var (physW, physH) = GetPhysicalWindowSize(window, hwnd, scaleX, scaleY);
            int stackOffsetPx = ComputeStackOffsetPx(window, screen, physH, gapPx, scaleY, explicitStackIndex);
            var (leftPx, topPx) = ComputeTargetPhysicalOrigin(phys, physW, physH, scaleX, scaleY, position, stackOffsetPx);

            MoveWindowPhysical(hwnd, leftPx, topPx);
            return;
        }

        // Before Show() creates the HWND, map the target physical coordinates using primary/system DPI
        // so the window is created directly on the target monitor without flashing at primary defaults.
        var (dipW, dipH) = GetDipWindowSize(window);
        int prePhysW = (int)Math.Round(dipW * scaleX);
        int prePhysH = (int)Math.Round(dipH * scaleY);
        int preStackOffsetPx = ComputeStackOffsetPx(window, screen, prePhysH, gapPx, scaleY, explicitStackIndex);
        var (preLeftPx, preTopPx) = ComputeTargetPhysicalOrigin(phys, prePhysW, prePhysH, scaleX, scaleY, position, preStackOffsetPx);

        ApplyPreShowLogicalPosition(window, preLeftPx, preTopPx);
    }

    internal static (int LeftPx, int TopPx) ComputeTargetPhysicalOrigin(
        System.Drawing.Rectangle physWorkArea,
        int windowWidthPx,
        int windowHeightPx,
        double scaleX,
        double scaleY,
        PopupPosition position,
        int stackOffsetPx)
    {
        if (scaleX <= 0) scaleX = 1.0;
        if (scaleY <= 0) scaleY = 1.0;

        // Visible card sits inset by a 20 DIP shadow buffer; 8 DIP outer margin places the visible card 28 DIPs from work-area edges.
        int marginX = (int)Math.Round(8 * scaleX);
        int marginY = (int)Math.Round(8 * scaleY);

        int leftPx;
        int topPx;

        switch (position)
        {
            case PopupPosition.TopLeft:
                leftPx = physWorkArea.Left + marginX;
                topPx = physWorkArea.Top + marginY + stackOffsetPx;
                break;

            case PopupPosition.TopCenter:
                leftPx = physWorkArea.Left + (physWorkArea.Width - windowWidthPx) / 2;
                topPx = physWorkArea.Top + marginY + stackOffsetPx;
                break;

            case PopupPosition.TopRight:
                leftPx = physWorkArea.Right - windowWidthPx - marginX;
                topPx = physWorkArea.Top + marginY + stackOffsetPx;
                break;

            case PopupPosition.Left:
                leftPx = physWorkArea.Left + marginX;
                topPx = physWorkArea.Top + (physWorkArea.Height - windowHeightPx) / 2 + stackOffsetPx;
                break;

            case PopupPosition.Right:
                leftPx = physWorkArea.Right - windowWidthPx - marginX;
                topPx = physWorkArea.Top + (physWorkArea.Height - windowHeightPx) / 2 + stackOffsetPx;
                break;

            case PopupPosition.BottomLeft:
                leftPx = physWorkArea.Left + marginX;
                topPx = physWorkArea.Bottom - windowHeightPx - marginY - stackOffsetPx;
                break;

            case PopupPosition.BottomCenter:
                leftPx = physWorkArea.Left + (physWorkArea.Width - windowWidthPx) / 2;
                topPx = physWorkArea.Bottom - windowHeightPx - marginY - stackOffsetPx;
                break;

            case PopupPosition.BottomRight:
            default:
                leftPx = physWorkArea.Right - windowWidthPx - marginX;
                topPx = physWorkArea.Bottom - windowHeightPx - marginY - stackOffsetPx;
                break;
        }

        leftPx = Math.Clamp(leftPx, physWorkArea.Left, Math.Max(physWorkArea.Left, physWorkArea.Right - windowWidthPx));
        topPx = Math.Clamp(topPx, physWorkArea.Top, Math.Max(physWorkArea.Top, physWorkArea.Bottom - windowHeightPx));
        return (leftPx, topPx);
    }

    internal static void EnsureWindowOnMonitorContext(Window window, IntPtr hwnd, Screen screen)
    {
        var currentScreen = Screen.FromHandle(hwnd);
        if (!string.Equals(currentScreen.DeviceName, screen.DeviceName, StringComparison.OrdinalIgnoreCase))
        {
            // Move onto the target monitor first so Windows switches DPI context before measuring size.
            var phys = screen.WorkingArea;
            SetWindowPos(hwnd, IntPtr.Zero, phys.X, phys.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
            window.UpdateLayout();
        }
    }

    internal static void MoveWindowPhysical(IntPtr hwnd, int leftPx, int topPx)
    {
        SetWindowPos(hwnd, IntPtr.Zero, leftPx, topPx, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
    }

    internal static void ApplyPreShowLogicalPosition(Window window, int physLeftPx, int physTopPx)
    {
        var (preScaleX, preScaleY) = GetPreShowDpiScale(window);
        window.Left = physLeftPx / preScaleX;
        window.Top = physTopPx / preScaleY;
    }

    internal static (double Width, double Height) GetDipWindowSize(Window window, double defaultHeight = 280)
    {
        double width = window.ActualWidth > 1 ? window.ActualWidth
            : (window.Width > 0 ? window.Width : 516);
        double height = window.ActualHeight > 1 ? window.ActualHeight
            : (window.Height > 0 ? window.Height
            : (window.DesiredSize.Height > 1 ? window.DesiredSize.Height : defaultHeight));
        return (width, height);
    }

    internal static (int WidthPx, int HeightPx) GetPhysicalWindowSize(Window window, IntPtr hwnd, double scaleX, double scaleY, double defaultHeight = 280)
    {
        if (hwnd != IntPtr.Zero && GetWindowRect(hwnd, out var rc) && rc.Right > rc.Left && rc.Bottom > rc.Top)
        {
            return (rc.Right - rc.Left, rc.Bottom - rc.Top);
        }

        var (dipW, dipH) = GetDipWindowSize(window, defaultHeight);
        return ((int)Math.Round(dipW * scaleX), (int)Math.Round(dipH * scaleY));
    }

    private static int ComputeStackOffsetPx(
        Window currentWindow,
        Screen targetScreen,
        int currentPhysHeight,
        int gapPx,
        double targetScaleY,
        int? explicitStackIndex)
    {
        if (explicitStackIndex.HasValue)
        {
            return explicitStackIndex.Value * (currentPhysHeight + gapPx);
        }

        var existing = System.Windows.Application.Current?.Windows
            .OfType<Window>()
            .Where(w => w != currentWindow && w.IsVisible && (
                (w is PromptStackWindow psw && !psw.IsPreview) ||
                w is AppInfoToast ||
                w is FirstActivityToast ||
                w is AutoBlockToast))
            .ToList();

        if (existing == null || existing.Count == 0) return 0;

        int totalPx = 0;
        foreach (var w in existing)
        {
            var wHwnd = new WindowInteropHelper(w).Handle;
            if (wHwnd != IntPtr.Zero)
            {
                var wScreen = Screen.FromHandle(wHwnd);
                if (!string.Equals(wScreen.DeviceName, targetScreen.DeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (GetWindowRect(wHwnd, out var rc) && rc.Bottom > rc.Top)
                {
                    totalPx += (rc.Bottom - rc.Top) + gapPx;
                    continue;
                }
            }

            double hDip = w.ActualHeight > 1 ? w.ActualHeight : (w.Height > 0 ? w.Height : 160);
            totalPx += (int)Math.Round(hDip * targetScaleY) + gapPx;
        }

        return totalPx;
    }

    internal static (double ScaleX, double ScaleY) GetMonitorScale(Screen screen)
    {
        try
        {
            var area = screen.WorkingArea;
            var sample = new System.Drawing.Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            var hmon = MonitorFromPoint(sample, MONITOR_DEFAULTTONEAREST);
            if (hmon != IntPtr.Zero &&
                GetDpiForMonitor(hmon, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY) == 0 &&
                dpiX > 0 && dpiY > 0)
            {
                return (dpiX / 96.0, dpiY / 96.0);
            }
        }
        catch { }

        return (1.0, 1.0);
    }

    internal static (double ScaleX, double ScaleY) GetPreShowDpiScale(Window window)
    {
        try
        {
            var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
            if (dpi.DpiScaleX > 0 && dpi.DpiScaleY > 0)
            {
                return (dpi.DpiScaleX, dpi.DpiScaleY);
            }
        }
        catch { }

        var primary = Screen.PrimaryScreen;
        return primary != null ? GetMonitorScale(primary) : (1.0, 1.0);
    }

    public static bool HasOpenPermissionPopup()
    {
        try
        {
            if (PromptManager.Instance.HasOpenPrompts()) return true;
            return System.Windows.Application.Current?.Windows
                .OfType<PromptStackWindow>()
                .Any(p => p.IsVisible && !p.IsPreview) == true;
        }
        catch
        {
            return false;
        }
    }
}
