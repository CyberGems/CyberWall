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

    [DllImport("user32.dll", EntryPoint = "GetWindowLongA")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongA")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

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
        // 1. Get the target monitor
        var screens = GetSortedScreens();
        var screen = (monitorIndex >= 0 && monitorIndex < screens.Length)
            ? screens[monitorIndex]
            : Screen.FromPoint(Cursor.Position);

        var phys = screen.WorkingArea;

        // 2. Move window to the target monitor physically first so Windows/WPF switches DPI context
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            SetWindowPos(hwnd, IntPtr.Zero, phys.X, phys.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
        }

        // 3. Compute accurate DIP work area on that monitor via PointFromScreen
        Rect wa;
        try
        {
            if (window.IsLoaded && hwnd != IntPtr.Zero)
            {
                var topLeft = window.PointFromScreen(new System.Windows.Point(phys.Left, phys.Top));
                var bottomRight = window.PointFromScreen(new System.Windows.Point(phys.Right, phys.Bottom));

                wa = new Rect(
                    window.Left + topLeft.X,
                    window.Top + topLeft.Y,
                    Math.Max(100, bottomRight.X - topLeft.X),
                    Math.Max(100, bottomRight.Y - topLeft.Y));
            }
            else
            {
                wa = screen.Primary ? SystemParameters.WorkArea : new Rect(screen.Bounds.X, screen.Bounds.Y, screen.WorkingArea.Width, screen.WorkingArea.Height);
            }
        }
        catch
        {
            wa = SystemParameters.WorkArea;
        }

        int stackIndex;
        if (explicitStackIndex.HasValue)
        {
            stackIndex = explicitStackIndex.Value;
        }
        else
        {
            var existing = System.Windows.Application.Current.Windows
                .OfType<Window>()
                .Where(w => w != window && w.IsVisible && (
                    w is PromptStackWindow psw && !psw.IsPreview ||
                    w is FirstActivityToast ||
                    (window is AutoBlockToast && w is AutoBlockToast)))
                .ToList();
            stackIndex = existing.Count;
        }

        const double marginX = 20;
        const double marginY = 20;
        const double gap = 10;

        double width = window.ActualWidth > 1 ? window.ActualWidth : window.Width > 0 ? window.Width : 516;
        double height = window.ActualHeight > 1 ? window.ActualHeight : window.Height > 0 ? window.Height : 280;

        double left;
        double top;

        switch (position)
        {
            case PopupPosition.TopLeft:
                left = wa.Left + marginX;
                top = wa.Top + marginY + stackIndex * (height + gap);
                break;

            case PopupPosition.TopCenter:
                left = wa.Left + (wa.Width - width) / 2;
                top = wa.Top + marginY + stackIndex * (height + gap);
                break;

            case PopupPosition.TopRight:
                left = wa.Right - width - marginX;
                top = wa.Top + marginY + stackIndex * (height + gap);
                break;

            case PopupPosition.Left:
                left = wa.Left + marginX;
                top = wa.Top + (wa.Height - height) / 2 + stackIndex * (height + gap);
                break;

            case PopupPosition.Right:
                left = wa.Right - width - marginX;
                top = wa.Top + (wa.Height - height) / 2 + stackIndex * (height + gap);
                break;

            case PopupPosition.BottomLeft:
                left = wa.Left + marginX;
                top = wa.Bottom - height - marginY - stackIndex * (height + gap);
                break;

            case PopupPosition.BottomCenter:
                left = wa.Left + (wa.Width - width) / 2;
                top = wa.Bottom - height - marginY - stackIndex * (height + gap);
                break;

            case PopupPosition.BottomRight:
            default:
                left = wa.Right - width - marginX;
                top = wa.Bottom - height - marginY - stackIndex * (height + gap);
                break;
        }

        // Clamp inside workArea
        if (left < wa.Left) left = wa.Left;
        if (left + width > wa.Right) left = wa.Right - width;
        if (top < wa.Top) top = wa.Top;
        if (top + height > wa.Bottom) top = wa.Bottom - height;

        window.Left = SnapToDevicePixels(window, left, horizontal: true);
        window.Top = SnapToDevicePixels(window, top, horizontal: false);
    }

    private static double SnapToDevicePixels(Window window, double dip, bool horizontal)
    {
        try
        {
            var source = PresentationSource.FromVisual(window);
            var m = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
            double scale = horizontal ? m.M11 : m.M22;
            if (scale <= 0) return dip;
            return Math.Round(dip * scale) / scale;
        }
        catch
        {
            return dip;
        }
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
