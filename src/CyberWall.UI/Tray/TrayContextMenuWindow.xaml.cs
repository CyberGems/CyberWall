using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Service.Engine;
using CyberWall.UI.Services;
using DrawingPoint = System.Drawing.Point;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace CyberWall.UI.Tray;

public partial class TrayContextMenuWindow : Window
{
    private readonly MainWindow _mainWindow;
    private readonly FirewallService _svc;
    private readonly DrawingPoint _clickPoint;
    private bool _isClosing;
    private bool _updatingUi;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_NOZORDER = 0x0004;

    public TrayContextMenuWindow(MainWindow mainWindow, FirewallService svc, DrawingPoint clickPoint)
    {
        _mainWindow = mainWindow;
        _svc = svc;
        _clickPoint = clickPoint;

        InitializeComponent();
        Icon = AppIconHelper.CreateShieldImageSource(32);
        LoadLocalizedStrings();
        UpdateProtectionState();
    }

    private void LoadLocalizedStrings()
    {
        OpenAppText.Text = Strings.T("OpenCyberWall");
        SettingsText.Text = Strings.T("Settings");
        LogText.Text = Strings.T("OpenLog");
        AboutText.Text = Strings.T("About");
        ExitText.Text = Strings.T("ExitApp");
    }

    private void UpdateProtectionState()
    {
        _updatingUi = true;
        var on = _svc.IsMasterOn;
        MasterSwitch.IsChecked = on;
        ProtectionStatusText.Text = on ? Strings.T("ProtectionActive") : Strings.T("ProtectionDisabled");
        StatusDot.Fill = on ? new SolidColorBrush(MediaColor.FromRgb(0x4A, 0xDE, 0x80)) : new SolidColorBrush(MediaColor.FromRgb(0xEF, 0x44, 0x44));
        ProtectionModeText.Text = _svc.Mode == FirewallMode.Ask ? Strings.T("ModeAsk") : Strings.T("ModeBlockAll");
        _updatingUi = false;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var physicalCursor = _clickPoint;
            var screen = System.Windows.Forms.Screen.FromPoint(physicalCursor);
            var physWork = screen.WorkingArea;
            var physBounds = screen.Bounds;

            var hwnd = new WindowInteropHelper(this).EnsureHandle();

            // Measure layout to get accurate DIP dimensions
            Measure(new System.Windows.Size(Width, double.PositiveInfinity));
            double dipWidth = DesiredSize.Width > 0 ? DesiredSize.Width : (ActualWidth > 0 ? ActualWidth : Width);
            if (dipWidth <= 0) dipWidth = 280;
            double dipHeight = DesiredSize.Height > 0 ? DesiredSize.Height : (ActualHeight > 0 ? ActualHeight : 330);
            if (dipHeight <= 0) dipHeight = 330;

            var dpi = VisualTreeHelper.GetDpi(this);
            double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
            double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

            int physicalWidth = (int)Math.Ceiling(dipWidth * scaleX);
            int physicalHeight = (int)Math.Ceiling(dipHeight * scaleY);

            int gap = (int)Math.Round(10 * scaleX);
            int eps = (int)Math.Round(4 * scaleX);

            bool taskbarLeft = physWork.Left > physBounds.Left + eps;
            bool taskbarRight = physWork.Right < physBounds.Right - eps;
            bool taskbarTop = physWork.Top > physBounds.Top + eps;

            int physX;
            int physY;

            if (taskbarLeft)
            {
                // Left taskbar: place menu to the right of taskbar, aligned vertically near cursor
                physX = physWork.Left + gap;
                physY = physicalCursor.Y - (physicalHeight / 2);
            }
            else if (taskbarRight)
            {
                // Right taskbar: place menu to the left of taskbar, aligned vertically near cursor
                physX = physWork.Right - physicalWidth - gap;
                physY = physicalCursor.Y - (physicalHeight / 2);
            }
            else if (taskbarTop)
            {
                // Top taskbar: place menu below taskbar, aligned horizontally near cursor
                physX = physicalCursor.X - (physicalWidth / 2);
                physY = physWork.Top + gap;
            }
            else
            {
                // Bottom taskbar (default): place menu above taskbar, aligned horizontally near cursor
                physX = physicalCursor.X - (physicalWidth / 2);
                physY = physWork.Bottom - physicalHeight - gap;
            }

            // Strictly clamp so the entire window is 100% inside the monitor's work area
            int minX = physWork.Left + gap;
            int maxX = physWork.Right - physicalWidth - gap;
            int minY = physWork.Top + gap;
            int maxY = physWork.Bottom - physicalHeight - gap;

            if (maxX < minX) physX = physWork.Left + (physWork.Width - physicalWidth) / 2;
            else physX = Math.Clamp(physX, minX, maxX);

            if (maxY < minY) physY = physWork.Top + (physWork.Height - physicalHeight) / 2;
            else physY = Math.Clamp(physY, minY, maxY);

            // Set both Win32 HWND position and WPF Left/Top
            if (hwnd != IntPtr.Zero)
            {
                SetWindowPos(hwnd, IntPtr.Zero, physX, physY, physicalWidth, physicalHeight, SWP_NOZORDER | SWP_NOACTIVATE);
            }

            Left = physX / scaleX;
            Top = physY / scaleY;

            Activate();
            Focus();
        }
        catch { }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        CloseMenu();
    }

    private void CloseMenu()
    {
        if (_isClosing) return;
        _isClosing = true;
        try { Close(); } catch { }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseMenu();

    private void MasterSwitch_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi) return;
        if (MasterSwitch.IsChecked == true)
        {
            _svc.Enable((FirewallMode)App.Settings.FirewallMode);
        }
        else
        {
            _svc.Disable();
        }
        App.Settings.FirewallEnabled = _svc.IsMasterOn;
        App.Settings.Save();
        _mainWindow.Dispatcher.Invoke(() => _mainWindow.RefreshStatusFromExternal());
        UpdateProtectionState();
    }

    private void OpenApp_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
        });
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            var w = new SettingsWindow(App.Settings) { Owner = _mainWindow };
            w.ShowDialog();
            _mainWindow.RefreshLanguage();
            _mainWindow.RefreshStatusFromExternal();
        });
    }

    private void Log_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            var dlg = new Dialogs.LogViewerDialog { Owner = _mainWindow };
            dlg.ShowDialog();
        });
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            var dlg = new Dialogs.AboutWindow(App.Settings) { Owner = _mainWindow };
            dlg.ShowDialog();
        });
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.ExitApplication();
    }
}
