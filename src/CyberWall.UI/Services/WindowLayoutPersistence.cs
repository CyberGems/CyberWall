using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CyberWall.Common.Settings;
using FormsScreen = System.Windows.Forms.Screen;

namespace CyberWall.UI.Services;

internal static class WindowLayoutPersistence
{
    public static void Restore(Window window, AppSettings settings)
    {
        if (!settings.MainWindowBoundsSaved)
        {
            CenterOnCurrentMonitor(window, Rect.Empty);
            return;
        }

        try
        {
            window.WindowState = WindowState.Normal;
            var savedBounds = new Rect(
                settings.MainWindowLeft,
                settings.MainWindowTop,
                settings.MainWindowWidth,
                settings.MainWindowHeight);

            if (IsValid(savedBounds) && HasMonitor(settings.MainWindowMonitor))
            {
                window.Left = savedBounds.Left;
                window.Top = savedBounds.Top;
                window.Width = savedBounds.Width;
                window.Height = savedBounds.Height;
            }
            else
            {
                CenterOnCurrentMonitor(window, savedBounds);
            }

            window.UpdateLayout();
            KeepOnCurrentWorkArea(window);

            if (settings.MainWindowMaximized)
            {
                window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
                {
                    if (window.IsVisible)
                        WorkAreaMaximize.Fill(window);
                }));
            }
        }
        catch
        {
            // Invalid or stale settings must never prevent the main window from opening.
        }
    }

    public static void Save(Window window, AppSettings settings)
    {
        try
        {
            var bounds = WorkAreaMaximize.GetRestoreBounds(window);
            if (!IsValid(bounds))
                return;

            var handle = new WindowInteropHelper(window).Handle;
            var screen = handle != IntPtr.Zero
                ? FormsScreen.FromHandle(handle)
                : FormsScreen.PrimaryScreen;
            if (screen == null)
                return;

            settings.MainWindowBoundsSaved = true;
            settings.MainWindowMonitor = screen.DeviceName;
            settings.MainWindowLeft = bounds.Left;
            settings.MainWindowTop = bounds.Top;
            settings.MainWindowWidth = bounds.Width;
            settings.MainWindowHeight = bounds.Height;
            settings.MainWindowMaximized =
                WorkAreaMaximize.IsFilled(window) || window.WindowState == WindowState.Maximized;
        }
        catch
        {
            // Window shutdown should not fail because layout persistence is unavailable.
        }
    }

    public static void ResetToDefault(Window window, AppSettings settings)
    {
        try
        {
            WorkAreaMaximize.Restore(window);
            window.WindowState = WindowState.Normal;

            var area = WorkAreaMaximize.GetWorkAreaDip(window);
            const double defaultWidth = 1160;
            const double defaultHeight = 720;

            window.Width = Math.Min(defaultWidth, area.Width);
            window.Height = Math.Min(defaultHeight, area.Height);
            window.Left = area.Left + Math.Max(0, (area.Width - window.Width) / 2);
            window.Top = area.Top + Math.Max(0, (area.Height - window.Height) / 2);

            var handle = new WindowInteropHelper(window).Handle;
            var screen = handle != IntPtr.Zero
                ? FormsScreen.FromHandle(handle)
                : FormsScreen.PrimaryScreen;

            settings.MainWindowBoundsSaved = true;
            settings.MainWindowMonitor = screen?.DeviceName ?? string.Empty;
            settings.MainWindowWidth = window.Width;
            settings.MainWindowHeight = window.Height;
            settings.MainWindowLeft = window.Left;
            settings.MainWindowTop = window.Top;
            settings.MainWindowMaximized = false;
            settings.Save();
        }
        catch
        {
        }
    }

    private static bool HasMonitor(string deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return true;

        try
        {
            return FormsScreen.AllScreens.Any(s =>
                s.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    public static void CenterOnCurrentMonitor(Window window, Rect savedBounds)
    {
        var area = WorkAreaMaximize.GetWorkAreaDip(window);
        var width = IsFinitePositive(savedBounds.Width)
            ? Math.Min(savedBounds.Width, area.Width)
            : Math.Min(window.Width, area.Width);
        var height = IsFinitePositive(savedBounds.Height)
            ? Math.Min(savedBounds.Height, area.Height)
            : Math.Min(window.Height, area.Height);

        window.Width = Math.Max(window.MinWidth, width);
        window.Height = Math.Max(window.MinHeight, height);
        window.Left = area.Left + (area.Width - window.Width) / 2;
        window.Top = area.Top + (area.Height - window.Height) / 2;
    }

    public static void KeepOnCurrentWorkArea(Window window)
    {
        var area = WorkAreaMaximize.GetWorkAreaDip(window);
        window.Width = Math.Max(window.MinWidth, Math.Min(window.Width, area.Width));
        window.Height = Math.Max(window.MinHeight, Math.Min(window.Height, area.Height));
        window.Left = Math.Max(area.Left, Math.Min(window.Left, area.Right - window.Width));
        window.Top = Math.Max(area.Top, Math.Min(window.Top, area.Bottom - window.Height));
    }

    private static bool IsValid(Rect bounds) =>
        IsFinitePositive(bounds.Width) &&
        IsFinitePositive(bounds.Height) &&
        double.IsFinite(bounds.Left) &&
        double.IsFinite(bounds.Top);

    private static bool IsFinitePositive(double value) =>
        double.IsFinite(value) && value > 0;
}
