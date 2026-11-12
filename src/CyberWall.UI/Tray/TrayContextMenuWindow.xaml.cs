using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Service.Engine;
using CyberWall.UI.Services;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using MediaColor = System.Windows.Media.Color;

namespace CyberWall.UI.Tray;

public partial class TrayContextMenuWindow : Window
{
    private enum TrayMenuView { Main, PauseAlerts, Help }

    private readonly MainWindow _mainWindow;
    private readonly FirewallService _svc;
    private readonly DrawingPoint _clickPoint;
    private bool _isClosing;
    private bool _updatingUi;
    private TrayMenuView _currentView = TrayMenuView.Main;

    public TrayContextMenuWindow(MainWindow mainWindow, FirewallService svc, DrawingPoint clickPoint)
    {
        _mainWindow = mainWindow;
        _svc = svc;
        _clickPoint = clickPoint;

        InitializeComponent();
        Icon = AppIconHelper.CreateShieldImageSource(32);
        Title = Strings.T("WindowTrayMenu");
        LoadLocalizedStrings();
        UpdateProtectionState();
        UpdateWidgetState();
        UpdatePauseAlertsState();

        WindowStartupLocation = WindowStartupLocation.Manual;
        SizeChanged += (_, _) => PositionWindow();
        PositionWindow();
    }

    private void PositionWindow()
    {
        try
        {
            var screen = System.Windows.Forms.Screen.FromPoint(_clickPoint);
            var physWork = screen.WorkingArea;
            var physBounds = screen.Bounds;
            var (scaleX, scaleY) = PopupWindowHelper.GetMonitorScale(screen);
            var hwnd = new WindowInteropHelper(this).Handle;

            if (hwnd != IntPtr.Zero)
            {
                PopupWindowHelper.EnsureWindowOnMonitorContext(this, hwnd, screen);
                var (physW, physH) = PopupWindowHelper.GetPhysicalWindowSize(this, hwnd, scaleX, scaleY, defaultHeight: 360);
                var (leftPx, topPx) = ComputeTargetPhysicalOrigin(physWork, physBounds, _clickPoint, physW, physH, scaleX, scaleY);
                PopupWindowHelper.MoveWindowPhysical(hwnd, leftPx, topPx);
                return;
            }

            var (dipW, dipH) = PopupWindowHelper.GetDipWindowSize(this, defaultHeight: 360);
            int prePhysW = (int)Math.Round(dipW * scaleX);
            int prePhysH = (int)Math.Round(dipH * scaleY);
            var (preLeftPx, preTopPx) = ComputeTargetPhysicalOrigin(physWork, physBounds, _clickPoint, prePhysW, prePhysH, scaleX, scaleY);
            PopupWindowHelper.ApplyPreShowLogicalPosition(this, preLeftPx, preTopPx);
        }
        catch { }
    }

    internal static (int LeftPx, int TopPx) ComputeTargetPhysicalOrigin(
        DrawingRectangle physWorkArea,
        DrawingRectangle physBounds,
        DrawingPoint clickPointPx,
        int windowWidthPx,
        int windowHeightPx,
        double scaleX,
        double scaleY)
    {
        if (scaleX <= 0) scaleX = 1.0;
        if (scaleY <= 0) scaleY = 1.0;

        // The card includes a 20 DIP transparent shadow buffer, so keeping the outer HWND flush
        // with the taskbar work-area preserves a 20 DIP visual margin without clipping the shadow.
        const int outerGapPx = 0;
        int epsX = (int)Math.Round(4 * scaleX);
        int epsY = (int)Math.Round(4 * scaleY);

        bool taskbarLeft = physWorkArea.Left > physBounds.Left + epsX;
        bool taskbarRight = physWorkArea.Right < physBounds.Right - epsX;
        bool taskbarTop = physWorkArea.Top > physBounds.Top + epsY;

        int leftPx;
        int topPx;

        if (taskbarLeft)
        {
            leftPx = physWorkArea.Left + outerGapPx;
            topPx = clickPointPx.Y - windowHeightPx / 2;
        }
        else if (taskbarRight)
        {
            leftPx = physWorkArea.Right - windowWidthPx - outerGapPx;
            topPx = clickPointPx.Y - windowHeightPx / 2;
        }
        else if (taskbarTop)
        {
            leftPx = clickPointPx.X - windowWidthPx / 2;
            topPx = physWorkArea.Top + outerGapPx;
        }
        else
        {
            leftPx = clickPointPx.X - windowWidthPx / 2;
            topPx = physWorkArea.Bottom - windowHeightPx - outerGapPx;
        }

        int minLeft = physWorkArea.Left + outerGapPx;
        int maxLeft = physWorkArea.Right - windowWidthPx - outerGapPx;
        int minTop = physWorkArea.Top + outerGapPx;
        int maxTop = physWorkArea.Bottom - windowHeightPx - outerGapPx;

        leftPx = maxLeft < minLeft
            ? physWorkArea.Left + (physWorkArea.Width - windowWidthPx) / 2
            : Math.Clamp(leftPx, minLeft, maxLeft);

        topPx = maxTop < minTop
            ? physWorkArea.Top + (physWorkArea.Height - windowHeightPx) / 2
            : Math.Clamp(topPx, minTop, maxTop);

        return (leftPx, topPx);
    }

    private void LoadLocalizedStrings()
    {
        OpenAppText.Text = Strings.T("OpenCyberWall");
        NotifMenuText.Text = Strings.T("Notifications");
        PauseAlertsText.Text = Strings.T("PauseFloatingAlerts");
        ResumeAlertsText.Text = Strings.T("ResumeFloatingAlerts");
        PauseBackText.Text = Strings.T("TrayBack");
        PauseSubTitleText.Text = Strings.T("PauseFloatingAlerts");
        Pause1hText.Text = Strings.T("PauseAlerts1h");
        Pause6hText.Text = Strings.T("PauseAlerts6h");
        Pause12hText.Text = Strings.T("PauseAlerts12h");
        Pause24hText.Text = Strings.T("PauseAlerts24h");
        Pause24hSubText.Text = Strings.T("PauseAlertsSuggested");
        PausePermanentText.Text = Strings.T("PauseAlertsPermanent");
        SettingsText.Text = Strings.T("Settings");
        LogText.Text = Strings.T("OpenLog");
        StatsText.Text = Strings.T("StatsMenu");
        var widgetLabel = Strings.T("TrayFloatingWidget");
        WidgetText.Text = widgetLabel;
        AutomationProperties.SetName(WidgetSwitch, widgetLabel);
        HelpText.Text = Strings.T("Help");
        HelpBackText.Text = Strings.T("TrayBack");
        HelpSubTitleText.Text = Strings.T("Help");
        SubHelpText.Text = Strings.T("Help");
        SubFaqText.Text = Strings.T("Faq");
        SubChangelogText.Text = Strings.T("Changelog");
        SubWebsiteText.Text = Strings.T("Website");
        SubDonateText.Text = Strings.T("Donate");
        SubAboutText.Text = $"{Strings.T("About")}...";
        SubCheckUpdateText.Text = Strings.T("CheckForUpdates");
        ExitText.Text = Strings.T("ExitApp");
    }

    private void UpdatePauseAlertsState()
    {
        var pausedStatus = FloatingAlertsController.GetPausedStatusLabel(App.Settings);
        bool paused = !string.IsNullOrEmpty(pausedStatus);
        PauseAlertsBtn.Visibility = paused ? Visibility.Collapsed : Visibility.Visible;
        ResumeAlertsBtn.Visibility = paused ? Visibility.Visible : Visibility.Collapsed;
        if (paused)
        {
            ResumeAlertsSubText.Text = pausedStatus;
        }
    }

    private void ShowView(TrayMenuView view)
    {
        _currentView = view;
        MainViewPanel.Visibility = view == TrayMenuView.Main ? Visibility.Visible : Visibility.Collapsed;
        PauseAlertsViewPanel.Visibility = view == TrayMenuView.PauseAlerts ? Visibility.Visible : Visibility.Collapsed;
        HelpViewPanel.Visibility = view == TrayMenuView.Help ? Visibility.Visible : Visibility.Collapsed;
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

    private void UpdateWidgetState()
    {
        _updatingUi = true;
        bool active = _mainWindow.IsTrafficWidgetVisible;
        WidgetSwitch.IsChecked = active;
        WidgetIcon.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, active ? "AccentBrush" : "SubTextBrush");
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
            // The enable re-creates the saved allow rules before the default-deny
            // starts, so it runs in the background and the states are refreshed when
            // it is done.
            var mode = App.Settings.FirewallMode;
            App.Settings.FirewallEnabled = true;
            App.Settings.Save();
            _ = Task.Run(() =>
            {
                _svc.Enable(mode);
                _mainWindow.Dispatcher.Invoke(() => _mainWindow.RefreshStatusFromExternal());
                Dispatcher.Invoke(UpdateProtectionState);
            });
            return;
        }

        _svc.Disable();
        App.Settings.FirewallEnabled = _svc.IsMasterOn;
        App.Settings.Save();
        _mainWindow.Dispatcher.Invoke(() => _mainWindow.RefreshStatusFromExternal());
        UpdateProtectionState();
    }

    private void WidgetSwitch_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi) return;
        bool visible = WidgetSwitch.IsChecked == true;
        _mainWindow.Dispatcher.Invoke(() => _mainWindow.SetTrafficWidgetVisible(visible));
        UpdateWidgetState();
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
            _mainWindow.OpenSettings();
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
        ShowView(TrayMenuView.Help);
    }

    private void PauseAlertsMenu_Click(object sender, RoutedEventArgs e)
    {
        ShowView(TrayMenuView.PauseAlerts);
    }

    private void ResumeAlerts_Click(object sender, RoutedEventArgs e)
    {
        FloatingAlertsController.Resume();
        CloseMenu();
    }

    private void PauseDuration_Click(object sender, RoutedEventArgs e)
    {
        TimeSpan? duration = sender is FrameworkElement { Tag: string raw }
            && int.TryParse(raw, out int minutes)
            && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : null;
        FloatingAlertsController.Pause(duration);
        CloseMenu();
    }

    private void SubMenuBack_Click(object sender, RoutedEventArgs e)
    {
        ShowView(TrayMenuView.Main);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        if (_currentView != TrayMenuView.Main)
            ShowView(TrayMenuView.Main);
        else
            CloseMenu();
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
        _mainWindow.Dispatcher.Invoke(() => _mainWindow.OpenAbout(checkUpdatesNow: false));
    }

    private void SubCheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        _mainWindow.Dispatcher.Invoke(() => _mainWindow.OpenAbout(checkUpdatesNow: true));
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
