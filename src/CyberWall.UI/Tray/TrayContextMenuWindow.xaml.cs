using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Service.Engine;
using CyberWall.UI.Services;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;
using MediaColor = System.Windows.Media.Color;

namespace CyberWall.UI.Tray;

internal enum TrayFlyoutSide { Left, Right }

public partial class TrayContextMenuWindow : Window
{
    private enum TrayFlyoutKind { None, PauseAlerts, Help, Suite }

    private const double MainCardDipWidth = 260.0;
    private const double OuterShadowMarginDip = 20.0;
    private const double CardWithMarginDipWidth = MainCardDipWidth + OuterShadowMarginDip * 2.0;
    private const double FlyoutSpanDipWidth = MainCardDipWidth + 8.0;

    private readonly MainWindow _mainWindow;
    private readonly FirewallService _svc;
    private readonly DrawingPoint _clickPoint;
    private readonly DispatcherTimer _hoverOpenTimer;
    private readonly DispatcherTimer _hoverCloseTimer;
    private bool _isClosing;
    private bool _updatingUi;
    private TrayFlyoutKind _activeFlyout = TrayFlyoutKind.None;
    private TrayFlyoutKind _hoveredTrigger = TrayFlyoutKind.None;
    private TrayFlyoutKind _pendingOpenFlyout = TrayFlyoutKind.None;
    private TrayFlyoutSide _flyoutSide = TrayFlyoutSide.Left;
    private bool _isPointerInFlyout;

    private bool _isPositionAnchored;
    private int _anchorLeftPx;
    private int _anchorTopPx;
    private int _anchorBottomPx;
    private bool _isTaskbarBottom;

    public TrayContextMenuWindow(MainWindow mainWindow, FirewallService svc, DrawingPoint clickPoint)
    {
        _mainWindow = mainWindow;
        _svc = svc;
        _clickPoint = clickPoint;

        _hoverOpenTimer = new DispatcherTimer();
        _hoverOpenTimer.Tick += HoverOpenTimer_Tick;

        _hoverCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _hoverCloseTimer.Tick += HoverCloseTimer_Tick;

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

    protected override void OnClosed(EventArgs e)
    {
        _hoverOpenTimer.Stop();
        _hoverCloseTimer.Stop();
        base.OnClosed(e);
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

            var (dipW, dipH) = PopupWindowHelper.GetDipWindowSize(this, defaultHeight: 360);
            int physW = (int)Math.Round(dipW * scaleX);
            int physH = (int)Math.Round(dipH * scaleY);
            int cardWithMarginPhysW = (int)Math.Round(CardWithMarginDipWidth * scaleX);
            int flyoutSpanPhysW = (int)Math.Round(FlyoutSpanDipWidth * scaleX);

            var (targetLeftPx, targetTopPx, flyoutSide, alignBottom) = ComputeFlyoutLayout(
                physWork,
                physBounds,
                _clickPoint,
                cardWithMarginPhysW,
                flyoutSpanPhysW,
                physW,
                physH,
                scaleX,
                scaleY);

            ApplyStageOrientation(flyoutSide, alignBottom);

            if (hwnd != IntPtr.Zero)
            {
                PopupWindowHelper.EnsureWindowOnMonitorContext(this, hwnd, screen);

                int leftPx;
                int topPx;

                if (!_isPositionAnchored)
                {
                    _anchorLeftPx = targetLeftPx;
                    _anchorTopPx = targetTopPx;
                    _anchorBottomPx = targetTopPx + physH;
                    _isTaskbarBottom = alignBottom;

                    if (ActualHeight > 1)
                    {
                        _isPositionAnchored = true;
                    }

                    leftPx = targetLeftPx;
                    topPx = targetTopPx;
                }
                else
                {
                    (leftPx, topPx) = ComputeAnchoredOrigin(physWork, _anchorLeftPx, _anchorTopPx, _anchorBottomPx, physW, physH, _isTaskbarBottom);
                }

                PopupWindowHelper.SetWindowPosPhysical(hwnd, leftPx, topPx, physW, physH);
                return;
            }

            PopupWindowHelper.ApplyPreShowLogicalPosition(this, targetLeftPx, targetTopPx);
        }
        catch { }
    }

    private void ApplyStageOrientation(TrayFlyoutSide side, bool alignBottom)
    {
        _flyoutSide = side;
        Grid.SetColumn(MainCardHost, side == TrayFlyoutSide.Left ? 2 : 0);
        Grid.SetColumn(FlyoutSlot, side == TrayFlyoutSide.Left ? 0 : 2);

        var verticalAlignment = alignBottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        MainCardHost.VerticalAlignment = verticalAlignment;
        FlyoutSlot.VerticalAlignment = verticalAlignment;
        PauseAlertsFlyout.VerticalAlignment = verticalAlignment;
        HelpFlyout.VerticalAlignment = verticalAlignment;
        SuiteFlyout.VerticalAlignment = verticalAlignment;

        string chevronTag = side == TrayFlyoutSide.Left ? "Left" : "Right";
        PauseChevron.Tag = chevronTag;
        HelpChevron.Tag = chevronTag;
        SuiteChevron.Tag = chevronTag;
    }

    internal static (int LeftPx, int TopPx, TrayFlyoutSide FlyoutSide, bool AlignBottom) ComputeFlyoutLayout(
        DrawingRectangle physWorkArea,
        DrawingRectangle physBounds,
        DrawingPoint clickPointPx,
        int cardWithMarginWidthPx,
        int flyoutSpanWidthPx,
        int totalWindowWidthPx,
        int windowHeightPx,
        double scaleX,
        double scaleY)
    {
        if (scaleX <= 0) scaleX = 1.0;
        if (scaleY <= 0) scaleY = 1.0;

        int epsX = (int)Math.Round(4 * scaleX);
        int epsY = (int)Math.Round(4 * scaleY);
        bool taskbarLeft = physWorkArea.Left > physBounds.Left + epsX;
        bool taskbarRight = physWorkArea.Right < physBounds.Right - epsX;
        bool taskbarTop = physWorkArea.Top > physBounds.Top + epsY;
        bool isTaskbarBottom = !taskbarLeft && !taskbarRight && !taskbarTop;

        var (cardLeftPx, cardTopPx) = ComputeTargetPhysicalOrigin(
            physWorkArea, physBounds, clickPointPx, cardWithMarginWidthPx, windowHeightPx, scaleX, scaleY);

        int spaceLeft = cardLeftPx - physWorkArea.Left;
        int spaceRight = physWorkArea.Right - (cardLeftPx + cardWithMarginWidthPx);

        TrayFlyoutSide side;
        if (taskbarLeft)
        {
            side = TrayFlyoutSide.Right;
        }
        else if (taskbarRight)
        {
            side = TrayFlyoutSide.Left;
        }
        else if (spaceLeft >= flyoutSpanWidthPx)
        {
            side = TrayFlyoutSide.Left;
        }
        else if (spaceRight >= flyoutSpanWidthPx)
        {
            side = TrayFlyoutSide.Right;
        }
        else
        {
            side = spaceLeft >= spaceRight ? TrayFlyoutSide.Left : TrayFlyoutSide.Right;
        }

        int winLeftPx = side == TrayFlyoutSide.Left
            ? cardLeftPx - flyoutSpanWidthPx
            : cardLeftPx;

        int minLeft = physWorkArea.Left;
        int maxLeft = physWorkArea.Right - totalWindowWidthPx;
        winLeftPx = maxLeft < minLeft
            ? physWorkArea.Left + (physWorkArea.Width - totalWindowWidthPx) / 2
            : Math.Clamp(winLeftPx, minLeft, maxLeft);

        bool alignBottom = isTaskbarBottom
            || (!taskbarTop && clickPointPx.Y >= physWorkArea.Top + physWorkArea.Height / 2);

        return (winLeftPx, cardTopPx, side, alignBottom);
    }

    internal static (int LeftPx, int TopPx) ComputeAnchoredOrigin(
        DrawingRectangle physWorkArea,
        int anchorLeftPx,
        int anchorTopPx,
        int anchorBottomPx,
        int windowWidthPx,
        int windowHeightPx,
        bool isTaskbarBottom)
    {
        int leftPx = anchorLeftPx;
        int topPx = isTaskbarBottom
            ? anchorBottomPx - windowHeightPx
            : anchorTopPx;

        int minLeft = physWorkArea.Left;
        int maxLeft = Math.Max(minLeft, physWorkArea.Right - windowWidthPx);
        int minTop = physWorkArea.Top;
        int maxTop = Math.Max(minTop, physWorkArea.Bottom - windowHeightPx);

        leftPx = maxLeft < minLeft
            ? physWorkArea.Left + (physWorkArea.Width - windowWidthPx) / 2
            : Math.Clamp(leftPx, minLeft, maxLeft);

        topPx = maxTop < minTop
            ? physWorkArea.Top + (physWorkArea.Height - windowHeightPx) / 2
            : Math.Clamp(topPx, minTop, maxTop);

        return (leftPx, topPx);
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
        UpdateOpenAppState();
        NotifMenuText.Text = Strings.T("Notifications");
        PauseAlertsText.Text = Strings.T("PauseFloatingAlerts");
        ResumeAlertsText.Text = Strings.T("ResumeFloatingAlerts");
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
        SuiteMenuText.Text = Strings.T("MoreFromCyberGems");
        SuiteBtn.Visibility = App.Settings.ShowAppRecommendations ? Visibility.Visible : Visibility.Collapsed;
        HelpSubTitleText.Text = Strings.T("Help");
        SubHelpText.Text = Strings.T("Help");
        SubFaqText.Text = Strings.T("Faq");
        SubChangelogText.Text = Strings.T("Changelog");
        SubWebsiteText.Text = Strings.T("Website");
        SubDonateText.Text = Strings.T("Donate");
        SubAboutText.Text = $"{Strings.T("About")}...";
        SubCheckUpdateText.Text = Strings.T("CheckForUpdates");
        SuiteMoreText.Text = Strings.T("TraySuiteMoreDetails");
        PopulateSuiteApps();
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

    private bool IsMainWindowCurrentlyVisible()
        => _mainWindow.IsVisible && _mainWindow.WindowState != WindowState.Minimized;

    private void UpdateOpenAppState()
    {
        OpenAppText.Text = Strings.T(IsMainWindowCurrentlyVisible() ? "HideCyberWall" : "ShowCyberWall");
    }

    private Grid? GetFlyoutPanel(TrayFlyoutKind kind) => kind switch
    {
        TrayFlyoutKind.PauseAlerts => PauseAlertsFlyout,
        TrayFlyoutKind.Help => HelpFlyout,
        TrayFlyoutKind.Suite => SuiteFlyout,
        _ => null
    };

    private System.Windows.Controls.Button? GetFlyoutTrigger(TrayFlyoutKind kind) => kind switch
    {
        TrayFlyoutKind.PauseAlerts => PauseAlertsBtn,
        TrayFlyoutKind.Help => HelpBtn,
        TrayFlyoutKind.Suite => SuiteBtn,
        _ => null
    };

    private void ScheduleFlyoutOpen(TrayFlyoutKind kind)
    {
        _hoverCloseTimer.Stop();
        _hoverOpenTimer.Stop();

        if (kind == TrayFlyoutKind.Suite && !App.Settings.ShowAppRecommendations)
            return;

        if (_activeFlyout == kind)
            return;

        _pendingOpenFlyout = kind;
        _hoverOpenTimer.Interval = TimeSpan.FromMilliseconds(_activeFlyout != TrayFlyoutKind.None ? 60 : 120);
        _hoverOpenTimer.Start();
    }

    private void ScheduleFlyoutClose()
    {
        _hoverOpenTimer.Stop();
        _pendingOpenFlyout = TrayFlyoutKind.None;

        if (_activeFlyout == TrayFlyoutKind.None)
            return;

        _hoverCloseTimer.Stop();
        _hoverCloseTimer.Start();
    }

    private void HoverOpenTimer_Tick(object? sender, EventArgs e)
    {
        _hoverOpenTimer.Stop();
        var target = _pendingOpenFlyout;
        _pendingOpenFlyout = TrayFlyoutKind.None;
        if (target != TrayFlyoutKind.None && _hoveredTrigger == target)
        {
            SetActiveFlyout(target);
        }
    }

    private void HoverCloseTimer_Tick(object? sender, EventArgs e)
    {
        _hoverCloseTimer.Stop();
        if (_hoveredTrigger == TrayFlyoutKind.None && !_isPointerInFlyout)
        {
            SetActiveFlyout(TrayFlyoutKind.None);
        }
    }

    private void SetActiveFlyout(TrayFlyoutKind kind)
    {
        if (kind == TrayFlyoutKind.Suite && !App.Settings.ShowAppRecommendations)
            kind = TrayFlyoutKind.None;

        if (kind == _activeFlyout)
            return;

        _activeFlyout = kind;

        PauseAlertsBtn.Tag = kind == TrayFlyoutKind.PauseAlerts ? "FlyoutOpen" : null;
        HelpBtn.Tag = kind == TrayFlyoutKind.Help ? "FlyoutOpen" : null;
        SuiteBtn.Tag = kind == TrayFlyoutKind.Suite ? "FlyoutOpen" : null;

        HideFlyoutPanel(PauseAlertsFlyout, kind != TrayFlyoutKind.PauseAlerts);
        HideFlyoutPanel(HelpFlyout, kind != TrayFlyoutKind.Help);
        HideFlyoutPanel(SuiteFlyout, kind != TrayFlyoutKind.Suite);

        var incoming = GetFlyoutPanel(kind);
        if (incoming == null)
            return;

        incoming.BeginAnimation(OpacityProperty, null);
        incoming.Visibility = Visibility.Visible;
        incoming.IsHitTestVisible = true;

        if (incoming.RenderTransform is not TranslateTransform translate)
        {
            translate = new TranslateTransform();
            incoming.RenderTransform = translate;
        }

        translate.BeginAnimation(TranslateTransform.XProperty, null);
        double offsetX = _flyoutSide == TrayFlyoutSide.Left ? 8 : -8;
        translate.X = offsetX;
        incoming.Opacity = 0;

        var duration = TimeSpan.FromMilliseconds(140);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        incoming.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
        translate.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(offsetX, 0, duration) { EasingFunction = ease });
    }

    private static void HideFlyoutPanel(Grid panel, bool hide)
    {
        if (!hide) return;
        panel.BeginAnimation(OpacityProperty, null);
        panel.Opacity = 0;
        panel.IsHitTestVisible = false;
        panel.Visibility = Visibility.Hidden;
    }

    private void PauseAlertsBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoveredTrigger = TrayFlyoutKind.PauseAlerts;
        ScheduleFlyoutOpen(TrayFlyoutKind.PauseAlerts);
    }

    private void HelpBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoveredTrigger = TrayFlyoutKind.Help;
        ScheduleFlyoutOpen(TrayFlyoutKind.Help);
    }

    private void SuiteBtn_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoveredTrigger = TrayFlyoutKind.Suite;
        ScheduleFlyoutOpen(TrayFlyoutKind.Suite);
    }

    private void FlyoutTrigger_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoveredTrigger = TrayFlyoutKind.None;
        ScheduleFlyoutClose();
    }

    private void MainAction_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hoveredTrigger = TrayFlyoutKind.None;
        ScheduleFlyoutClose();
    }

    private void FlyoutSlot_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isPointerInFlyout = true;
        _hoverCloseTimer.Stop();
    }

    private void FlyoutSlot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isPointerInFlyout = false;
        ScheduleFlyoutClose();
    }

    private void Card_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private void RootBackdrop_MouseDown(object sender, MouseButtonEventArgs e)
    {
        CloseMenu();
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
            UpdateLayout();
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
            if (IsMainWindowCurrentlyVisible())
            {
                _mainWindow.Hide();
            }
            else
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
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
        _hoverOpenTimer.Stop();
        _hoverCloseTimer.Stop();
        SetActiveFlyout(_activeFlyout == TrayFlyoutKind.Help ? TrayFlyoutKind.None : TrayFlyoutKind.Help);
    }

    private void Suite_Click(object sender, RoutedEventArgs e)
    {
        _hoverOpenTimer.Stop();
        _hoverCloseTimer.Stop();
        SetActiveFlyout(_activeFlyout == TrayFlyoutKind.Suite ? TrayFlyoutKind.None : TrayFlyoutKind.Suite);
    }

    private void SuiteMore_Click(object sender, RoutedEventArgs e)
    {
        CloseMenu();
        OpenUrl("https://cybergems.org/#apps");
    }

    private void PopulateSuiteApps()
    {
        SuiteAppsPanel.Children.Clear();
        var rowStyle = TryFindResource("MenuCompactRowButtonStyle") as Style;
        var iconStyle = TryFindResource("MenuRowIcon") as Style;
        var textBrush = TryFindResource("TextBrush") as System.Windows.Media.Brush;
        var subTextBrush = TryFindResource("SubTextBrush") as System.Windows.Media.Brush;

        foreach (var app in SuiteCatalog.Others)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/CyberWall;component/Assets/Suite/{app.Slug}.png", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 64;
            bitmap.EndInit();
            bitmap.Freeze();

            var image = new System.Windows.Controls.Image
            {
                Width = 15,
                Height = 15,
                Source = bitmap,
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

            var iconHost = new ContentControl { Content = image };
            if (iconStyle != null) iconHost.Style = iconStyle;

            var nameBlock = new TextBlock
            {
                Text = app.Name,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (textBrush != null) nameBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

            var descBlock = new TextBlock
            {
                Text = Strings.T(app.PitchKey),
                FontSize = 9.8,
                Margin = new Thickness(0, 1, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            if (subTextBrush != null) descBlock.SetResourceReference(TextBlock.ForegroundProperty, "SubTextBrush");

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(nameBlock);
            textStack.Children.Add(descBlock);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(iconHost, 0);
            Grid.SetColumn(textStack, 1);
            grid.Children.Add(iconHost);
            grid.Children.Add(textStack);

            var btn = new System.Windows.Controls.Button { Content = grid };
            if (rowStyle != null) btn.Style = rowStyle;
            btn.Click += (_, _) =>
            {
                CloseMenu();
                OpenUrl(app.Site);
            };
            SuiteAppsPanel.Children.Add(btn);
        }
    }

    private void PauseAlertsMenu_Click(object sender, RoutedEventArgs e)
    {
        _hoverOpenTimer.Stop();
        _hoverCloseTimer.Stop();
        SetActiveFlyout(_activeFlyout == TrayFlyoutKind.PauseAlerts ? TrayFlyoutKind.None : TrayFlyoutKind.PauseAlerts);
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

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _hoverOpenTimer.Stop();
            _hoverCloseTimer.Stop();
            if (_activeFlyout != TrayFlyoutKind.None)
            {
                var trigger = GetFlyoutTrigger(_activeFlyout);
                SetActiveFlyout(TrayFlyoutKind.None);
                trigger?.Focus();
            }
            else
            {
                CloseMenu();
            }
            return;
        }

        var openKey = _flyoutSide == TrayFlyoutSide.Left ? Key.Left : Key.Right;
        var closeKey = _flyoutSide == TrayFlyoutSide.Left ? Key.Right : Key.Left;

        if (e.Key == openKey)
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            TrayFlyoutKind target = focused == PauseAlertsBtn ? TrayFlyoutKind.PauseAlerts
                : focused == HelpBtn ? TrayFlyoutKind.Help
                : focused == SuiteBtn ? TrayFlyoutKind.Suite
                : TrayFlyoutKind.None;

            if (target != TrayFlyoutKind.None)
            {
                e.Handled = true;
                _hoverOpenTimer.Stop();
                _hoverCloseTimer.Stop();
                SetActiveFlyout(target);
                FocusFirstButtonInFlyout(target);
            }
        }
        else if (e.Key == closeKey && _activeFlyout != TrayFlyoutKind.None)
        {
            var panel = GetFlyoutPanel(_activeFlyout);
            if (panel != null && Keyboard.FocusedElement is DependencyObject focused && panel.IsAncestorOf(focused))
            {
                e.Handled = true;
                var trigger = GetFlyoutTrigger(_activeFlyout);
                SetActiveFlyout(TrayFlyoutKind.None);
                trigger?.Focus();
            }
        }
    }

    private void FocusFirstButtonInFlyout(TrayFlyoutKind kind)
    {
        var panel = GetFlyoutPanel(kind);
        if (panel == null) return;
        foreach (var btn in FindVisualChildren<System.Windows.Controls.Button>(panel))
        {
            if (btn.IsVisible && btn.IsEnabled && btn.Focusable)
            {
                btn.Focus();
                break;
            }
        }
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var desc in FindVisualChildren<T>(child))
                yield return desc;
        }
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
