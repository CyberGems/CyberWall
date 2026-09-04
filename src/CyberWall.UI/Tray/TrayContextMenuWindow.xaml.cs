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

        WindowStartupLocation = WindowStartupLocation.Manual;
        SizeChanged += (_, _) => PositionWindow();
        PositionWindow();
    }

    private void PositionWindow()
    {
        try
        {
            var cursor = _clickPoint;
            var screen = System.Windows.Forms.Screen.FromPoint(cursor);
            var physWork = screen.WorkingArea;
            var physBounds = screen.Bounds;

            // Compute monitor DPI scale factor
            double scaleX = 1.0;
            double scaleY = 1.0;

            try
            {
                var dpi = VisualTreeHelper.GetDpi(this);
                if (dpi.DpiScaleX > 0) scaleX = dpi.DpiScaleX;
                if (dpi.DpiScaleY > 0) scaleY = dpi.DpiScaleY;
            }
            catch { }

            if (scaleX <= 0) scaleX = 1.0;
            if (scaleY <= 0) scaleY = 1.0;

            double workLeft = physWork.Left / scaleX;
            double workTop = physWork.Top / scaleY;
            double workRight = physWork.Right / scaleX;
            double workBottom = physWork.Bottom / scaleY;
            double workWidth = physWork.Width / scaleX;
            double workHeight = physWork.Height / scaleY;

            double screenLeft = physBounds.Left / scaleX;
            double screenRight = physBounds.Right / scaleX;
            double screenTop = physBounds.Top / scaleY;

            double cursorX = cursor.X / scaleX;
            double cursorY = cursor.Y / scaleY;

            double windowWidth = ActualWidth > 0 ? ActualWidth : (Width > 0 ? Width : 280);
            double windowHeight = ActualHeight > 0 ? ActualHeight : 340;

            const double gap = 10;
            const double eps = 4;

            bool taskbarLeft = workLeft > screenLeft + eps;
            bool taskbarRight = workRight < screenRight - eps;
            bool taskbarTop = workTop > screenTop + eps;

            double left;
            double top;

            if (taskbarLeft)
            {
                // Left taskbar: place menu directly to the right of taskbar
                left = workLeft + gap;
                top = cursorY - (windowHeight / 2);
            }
            else if (taskbarRight)
            {
                // Right taskbar: place menu directly to the left of taskbar
                left = workRight - windowWidth - gap;
                top = cursorY - (windowHeight / 2);
            }
            else if (taskbarTop)
            {
                // Top taskbar: place menu below taskbar
                left = cursorX - (windowWidth / 2);
                top = workTop + gap;
            }
            else
            {
                // Bottom taskbar: place menu above taskbar
                left = cursorX - (windowWidth / 2);
                top = workBottom - windowHeight - gap;
            }

            // Strictly clamp to monitor work area
            double minLeft = workLeft + gap;
            double maxLeft = workRight - windowWidth - gap;
            double minTop = workTop + gap;
            double maxTop = workBottom - windowHeight - gap;

            if (maxLeft < minLeft) left = workLeft + (workWidth - windowWidth) / 2;
            else left = Math.Clamp(left, minLeft, maxLeft);

            if (maxTop < minTop) top = workTop + (workHeight - windowHeight) / 2;
            else top = Math.Clamp(top, minTop, maxTop);

            Left = left;
            Top = top;
        }
        catch { }
    }

    private void LoadLocalizedStrings()
    {
        OpenAppText.Text = Strings.T("OpenCyberWall");
        NotifMenuText.Text = Strings.T("Notifications");
        SettingsText.Text = Strings.T("Settings");
        LogText.Text = Strings.T("OpenLog");
        StatsText.Text = Strings.T("StatsMenu");
        HelpText.Text = Strings.T("Help");
        SubHelpText.Text = Strings.T("Help");
        SubFaqText.Text = Strings.T("Faq");
        SubChangelogText.Text = Strings.T("Changelog");
        SubWebsiteText.Text = Strings.T("Website");
        SubDonateText.Text = Strings.T("Donate");
        SubAboutText.Text = $"{Strings.T("About")}...";
        SubCheckUpdateText.Text = Strings.T("CheckForUpdates");
        ExitText.Text = Strings.T("ExitApp");
    }

    private void UpdateProtectionState()
    {
        _updatingUi = true;
        var on = _svc.IsMasterOn;
        MasterSwitch.IsChecked = on;
        ProtectionStatusText.Text = on ? Strings.T("ProtectionActive") : Strings.T("ProtectionDisabled");
        StatusDot.Fill = on ? new SolidColorBrush(MediaColor.FromRgb(0x4A, 0xDE, 0x80)) : new SolidColorBrush(MediaColor.FromRgb(0xEF, 0x44, 0x44));
        ProtectionModeText.Text = _svc.Mode switch
        {
            FirewallMode.BlockAll => Strings.T("ModeBlockAll"),
            FirewallMode.Killswitch => Strings.T("ModeKillswitch"),
            _ => Strings.T("ModeAsk")
        };
        _updatingUi = false;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            PositionWindow();
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

    private void Notifications_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() => _mainWindow.ShowNotifications());
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() =>
        {
            var isMainVisible = _mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized;
            var w = new SettingsWindow(App.Settings)
            {
                Owner = isMainVisible ? _mainWindow : null,
                WindowStartupLocation = isMainVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
            };
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
            if (_mainWindow.WindowState == WindowState.Minimized)
                _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
            _mainWindow.SelectTab(MainTab.ConnectionsLog);
        });
    }

    private void Stats_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() =>
        {
            _mainWindow.Show();
            if (_mainWindow.WindowState == WindowState.Minimized)
                _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
            _mainWindow.SelectTab(MainTab.Statistics);
        });
    }

    private void Help_Click(object sender, RoutedEventArgs e)
    {
        bool isExpanded = HelpSubPanel.Visibility == Visibility.Visible;
        HelpSubPanel.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;
        HelpChevronRotate.Angle = isExpanded ? 0 : 180;
    }

    private void SubHelp_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        OpenUrl("https://github.com/CyberGems/CyberWall/wiki");
    }

    private void SubFaq_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        OpenUrl("https://github.com/CyberGems/CyberWall/wiki/FAQ");
    }

    private void SubChangelog_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        OpenUrl("https://github.com/CyberGems/CyberWall/releases");
    }

    private void SubWebsite_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        OpenUrl("https://cybergems.org");
    }

    private void SubDonate_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        OpenUrl("https://github.com/CyberGems/CyberWall#%EF%B8%8F-donate");
    }

    private void SubAbout_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() =>
        {
            var isMainVisible = _mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized;
            var dlg = new Dialogs.AboutWindow(App.Settings, checkUpdatesNow: false)
            {
                Owner = isMainVisible ? _mainWindow : null,
                WindowStartupLocation = isMainVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
            };
            dlg.ShowDialog();
        });
    }

    private void SubCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() =>
        {
            var isMainVisible = _mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized;
            var dlg = new Dialogs.AboutWindow(App.Settings, checkUpdatesNow: true)
            {
                Owner = isMainVisible ? _mainWindow : null,
                WindowStartupLocation = isMainVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
            };
            dlg.ShowDialog();
        });
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.ExitApplication();
    }
}
