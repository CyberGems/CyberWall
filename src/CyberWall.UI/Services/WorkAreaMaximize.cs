using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;

namespace CyberWall.UI.Services;

/// <summary>
/// Borderless / transparent windows ignore the taskbar when maximized.
/// Fill the monitor working area instead of using WindowState.Maximized.
/// </summary>
public static class WorkAreaMaximize
{
    private static readonly DependencyProperty IsFilledProperty =
        DependencyProperty.RegisterAttached("IsFilled", typeof(bool), typeof(WorkAreaMaximize));

    private static readonly DependencyProperty RestoreProperty =
        DependencyProperty.RegisterAttached("Restore", typeof(Rect), typeof(WorkAreaMaximize));

    private static readonly DependencyProperty ApplyingProperty =
        DependencyProperty.RegisterAttached("Applying", typeof(bool), typeof(WorkAreaMaximize));

    public static bool IsFilled(Window window) =>
        window != null && (bool)window.GetValue(IsFilledProperty);

    public static Rect GetRestoreBounds(Window window)
    {
        var restore = (Rect)window.GetValue(RestoreProperty);
        if (restore.Width > 0 && restore.Height > 0)
            return restore;
        return new Rect(window.Left, window.Top, window.Width, window.Height);
    }

    public static void Attach(Window window)
    {
        window.LocationChanged += (_, _) => RememberNormalBounds(window);
        window.SizeChanged += (_, _) => RememberNormalBounds(window);
        window.StateChanged += (_, _) =>
        {
            if ((bool)window.GetValue(ApplyingProperty)) return;
            if (window.WindowState != WindowState.Maximized) return;

            window.SetValue(ApplyingProperty, true);
            try
            {
                window.WindowState = WindowState.Normal;
                Fill(window);
            }
            finally
            {
                window.SetValue(ApplyingProperty, false);
            }
        };
    }

    public static void Toggle(Window window)
    {
        if (IsFilled(window) || window.WindowState == WindowState.Maximized)
            Restore(window);
        else
            Fill(window);
    }

    public static void Fill(Window window)
    {
        RememberNormalBounds(window);
        var nested = (bool)window.GetValue(ApplyingProperty);
        window.SetValue(ApplyingProperty, true);
        try
        {
            var area = GetWorkAreaDip(window);
            window.WindowState = WindowState.Normal;
            window.Left = area.Left;
            window.Top = area.Top;
            window.Width = Math.Max(window.MinWidth, area.Width);
            window.Height = Math.Max(window.MinHeight, area.Height);
            window.SetValue(IsFilledProperty, true);
        }
        finally
        {
            if (!nested)
                window.SetValue(ApplyingProperty, false);
        }
    }

    public static void Restore(Window window)
    {
        var restore = (Rect)window.GetValue(RestoreProperty);
        window.SetValue(ApplyingProperty, true);
        try
        {
            window.SetValue(IsFilledProperty, false);
            window.WindowState = WindowState.Normal;
            if (restore.Width <= 0 || restore.Height <= 0) return;
            window.Left = restore.Left;
            window.Top = restore.Top;
            window.Width = restore.Width;
            window.Height = restore.Height;
        }
        finally
        {
            window.SetValue(ApplyingProperty, false);
        }
    }

    private static void RememberNormalBounds(Window window)
    {
        if (IsFilled(window) || window.WindowState != WindowState.Normal) return;
        if ((bool)window.GetValue(ApplyingProperty)) return;
        if (window.Width <= 0 || window.Height <= 0) return;
        window.SetValue(RestoreProperty, new Rect(window.Left, window.Top, window.Width, window.Height));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    public static Rect GetWorkAreaDip(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            var hMon = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            if (hMon != IntPtr.Zero)
            {
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(hMon, ref mi))
                {
                    var source = PresentationSource.FromVisual(window);
                    if (source?.CompositionTarget != null)
                    {
                        var transform = source.CompositionTarget.TransformFromDevice;
                        var p1 = transform.Transform(new System.Windows.Point(mi.rcWork.left, mi.rcWork.top));
                        var p2 = transform.Transform(new System.Windows.Point(mi.rcWork.right, mi.rcWork.bottom));
                        return new Rect(p1.X, p1.Y, Math.Max(100, p2.X - p1.X), Math.Max(100, p2.Y - p1.Y));
                    }

                    var dpi = GetDpi(window);
                    return new Rect(
                        mi.rcWork.left / dpi.x,
                        mi.rcWork.top / dpi.y,
                        Math.Max(100, (mi.rcWork.right - mi.rcWork.left) / dpi.x),
                        Math.Max(100, (mi.rcWork.bottom - mi.rcWork.top) / dpi.y));
                }
            }
        }

        return SystemParameters.WorkArea;
    }

    private static (double x, double y) GetDpi(Window window)
    {
        try
        {
            var dpi = VisualTreeHelper.GetDpi(window);
            var x = dpi.DpiScaleX <= 0 ? 1 : dpi.DpiScaleX;
            var y = dpi.DpiScaleY <= 0 ? 1 : dpi.DpiScaleY;
            return (x, y);
        }
        catch
        {
            return (1, 1);
        }
    }
}
