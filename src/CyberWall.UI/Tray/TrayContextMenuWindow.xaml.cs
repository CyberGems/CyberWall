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

            // HWND Monitor migration
            try
            {
                var hwnd = new WindowInteropHelper(this).EnsureHandle();
                if (hwnd != IntPtr.Zero)
                {
                    SetWindowPos(hwnd, IntPtr.Zero, physWork.X, physWork.Y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOZORDER);
                }
            }
            catch { }

            Rect PhysicalToWindowDips(System.Drawing.Rectangle r)
            {
                var tl = PointFromScreen(new WpfPoint(r.Left, r.Top));
                var br = PointFromScreen(new WpfPoint(r.Right, r.Bottom));
                return new Rect(
                    Left + tl.X,
                    Top + tl.Y,
                    Math.Max(0, br.X - tl.X),
                    Math.Max(0, br.Y - tl.Y));
            }

            var workArea = PhysicalToWindowDips(physWork);
            var screenArea = PhysicalToWindowDips(physBounds);

            var cursorLocal = PointFromScreen(new WpfPoint(physicalCursor.X, physicalCursor.Y));
            double cursorX = Left + cursorLocal.X;
            double cursorY = Top + cursorLocal.Y;

            UpdateLayout();
            double windowWidth = ActualWidth > 0 ? ActualWidth : Width;
            double windowHeight = ActualHeight > 0 ? ActualHeight : 260;

            const double gap = 8;
            const double eps = 2;
            double left;
            double top;

            bool taskbarLeft = workArea.Left > screenArea.Left + eps;
            bool taskbarRight = workArea.Right < screenArea.Right - eps;
            bool taskbarTop = workArea.Top > screenArea.Top + eps;

            if (taskbarLeft)
            {
                left = workArea.Left + gap;
                top = cursorY - (windowHeight / 2);
            }
            else if (taskbarRight)
            {
                left = workArea.Right - windowWidth - gap;
                top = cursorY - (windowHeight / 2);
            }
            else if (taskbarTop)
            {
                left = cursorX - (windowWidth / 2);
                top = workArea.Top + gap;
            }
            else
            {
                left = cursorX - (windowWidth / 2);
                top = workArea.Bottom - windowHeight - gap;
            }

            double minLeft = workArea.Left + gap;
            double maxLeft = workArea.Right - windowWidth - gap;
            double minTop = workArea.Top + gap;
            double maxTop = workArea.Bottom - windowHeight - gap;

            if (maxLeft < minLeft) left = workArea.Left + (workArea.Width - windowWidth) / 2;
            else left = Math.Clamp(left, minLeft, maxLeft);

            if (maxTop < minTop) top = workArea.Top + (workArea.Height - windowHeight) / 2;
            else top = Math.Clamp(top, minTop, maxTop);

            Left = left;
            Top = top;

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

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.ExitApplication();
    }
}
