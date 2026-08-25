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

    public static Rect GetWorkAreaDip(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var screen = hwnd != IntPtr.Zero
            ? Screen.FromHandle(hwnd)
            : Screen.FromPoint(Control.MousePosition);

        var wa = screen.WorkingArea;
        var dpi = GetDpi(window);
        return new Rect(
            wa.Left / dpi.x,
            wa.Top / dpi.y,
            wa.Width / dpi.x,
            wa.Height / dpi.y);
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
