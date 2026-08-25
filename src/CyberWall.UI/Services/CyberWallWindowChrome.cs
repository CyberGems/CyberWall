using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Shell;

namespace CyberWall.UI.Services;

public static class CyberWallWindowChrome
{
    private const double DefaultCornerRadius = 12;

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    public static void Apply(Window window, double radius = DefaultCornerRadius)
    {
        bool canResize = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 0,
            CornerRadius = new CornerRadius(radius),
            GlassFrameThickness = new Thickness(0),
            ResizeBorderThickness = canResize ? new Thickness(8) : new Thickness(0),
            UseAeroCaptionButtons = false
        });

        if (!canResize)
        {
            void MakeContentClickable()
            {
                if (window.Content is IInputElement content)
                    WindowChrome.SetIsHitTestVisibleInChrome(content, true);
            }
            if (window.IsLoaded) MakeContentClickable();
            else window.Loaded += (_, _) => MakeContentClickable();
        }

        ApplyRoundedCorners(window, radius);
        if (canResize)
            WorkAreaMaximize.Attach(window);
    }

    public static void ApplyRoundedCorners(Window window, double radius)
    {
        void ApplyCurrentRegion()
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            {
                int pref = WorkAreaMaximize.IsFilled(window) ? DWMWCP_DONOTROUND : DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
                return;
            }

            if (WorkAreaMaximize.IsFilled(window))
            {
                SetWindowRgn(hwnd, IntPtr.Zero, true);
                return;
            }

            SetRoundedWindowRegion(window, hwnd, radius);
        }

        window.SourceInitialized += (_, _) => ApplyCurrentRegion();
        window.SizeChanged += (_, _) => ApplyCurrentRegion();
        window.Closed += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd != IntPtr.Zero)
                SetWindowRgn(hwnd, IntPtr.Zero, true);
        };
    }

    private static void SetRoundedWindowRegion(Window window, IntPtr hwnd, double radius)
    {
        if (window.ActualWidth <= 0 || window.ActualHeight <= 0) return;

        var source = PresentationSource.FromVisual(window);
        var transform = source?.CompositionTarget?.TransformToDevice ?? System.Windows.Media.Matrix.Identity;
        int width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth * transform.M11));
        int height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight * transform.M22));
        int diameterX = Math.Max(1, (int)Math.Round(radius * 2 * transform.M11));
        int diameterY = Math.Max(1, (int)Math.Round(radius * 2 * transform.M22));

        var region = CreateRoundRectRgn(0, 0, width + 1, height + 1, diameterX, diameterY);
        if (region == IntPtr.Zero) return;

        if (SetWindowRgn(hwnd, region, true) == 0)
            DeleteObject(region);
    }
}
