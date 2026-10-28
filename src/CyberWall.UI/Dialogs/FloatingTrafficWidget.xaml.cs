using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.UI.Services;
using MediaColor = System.Windows.Media.Color;
using WPoint = System.Windows.Point;

namespace CyberWall.UI.Dialogs;

/// <summary>
/// Small always-on-top companion window that mirrors the live network telemetry of the
/// Traffic Monitor tab (download/upload speeds, sparklines and the 60s graph) so the
/// traffic stays visible while the main window sits in the tray.
/// It is a passive view: <see cref="NetworkSpeedService"/> keeps sampling and the widget
/// pulls the shared history on a one second UI timer, so sampling stays single-source.
/// </summary>
public partial class FloatingTrafficWidget : Window
{
    private const int WM_DISPLAYCHANGE = 0x007E;

    /// <summary>Gap kept from the work area edge when the widget is placed for the first time.</summary>
    private const double DefaultEdgeMargin = 24;

    private const int HistorySlots = 60;

    private static readonly MediaColor DownColor = MediaColor.FromRgb(0x38, 0xBD, 0xF8);
    private static readonly MediaColor UpColor = MediaColor.FromRgb(0x22, 0xC5, 0x5E);
    private static readonly MediaColor OnlineColor = MediaColor.FromRgb(0x4A, 0xDE, 0x80);
    private static readonly MediaColor OfflineColor = MediaColor.FromRgb(0xEF, 0x44, 0x44);

    private readonly DispatcherTimer _timer;
    private bool _positionApplied;
    private bool _firewallActive = true;
    private FirewallMode _firewallMode = FirewallMode.Ask;
    private bool _online = true;
    private double _currentDown;
    private double _currentUp;
    private double _peakDown;
    private double _peakUp;
    private NetworkSpeedSnapshot _snapshot = new(0, 0, 0, 0, string.Empty, false);
    private int _historySampleCount;

    /// <summary>Raised when the user asks to close the widget.</summary>
    public event Action? CloseRequested;

    /// <summary>Raised when the user toggles always-on-top, from the pin or the context menu.</summary>
    public event Action<bool>? AlwaysOnTopChanged;

    /// <summary>Raised when the widget is moved, with its new screen position.</summary>
    public event Action<double, double>? PositionChanged;

    /// <summary>Raised when the user asks to open the Traffic Monitor view.</summary>
    public event Action? OpenTrafficMonitorRequested;

    /// <summary>Raised when the user asks to show the main window.</summary>
    public event Action? ShowMainWindowRequested;

    /// <summary>True once the widget has been placed at a valid on-screen position.</summary>
    public bool PositionApplied => _positionApplied;

    public FloatingTrafficWidget()
    {
        InitializeComponent();
        CyberWallWindowChrome.Apply(this, 12);
        Icon = AppIconHelper.CreateShieldImageSource(64);
        ApplyAlwaysOnTop(App.Settings.TrafficWidgetAlwaysOnTop);
        ApplySectionVisibility();
        RefreshLanguage();

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => RefreshData();

        ConnectivityService.Instance.ConnectivityChanged += OnConnectivityChanged;

        Loaded += (_, _) => ApplyInitialPosition();
        IsVisibleChanged += Widget_IsVisibleChanged;
        Closing += Widget_Closing;

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CloseRequested?.Invoke();
            }
        };
    }

    /// <summary>Applies the current language to every visible string and tooltip.</summary>
    public void RefreshLanguage()
    {
        HeaderText.Text = "CyberWall";
        DownLabel.Text = Strings.T("LegendDownload");
        UpLabel.Text = Strings.T("LegendUpload");
        GraphTitleText.Text = Strings.T("WidgetGraphTitle");
        GraphLegendDownText.Text = Strings.T("LegendDownload");
        GraphLegendUpText.Text = Strings.T("LegendUpload");
        CloseButton.ToolTip = Strings.T("WidgetClose");
        AutomationProperties.SetName(CloseButton, Strings.T("WidgetClose"));
        AutomationProperties.SetName(PinButton, Strings.T("WidgetAlwaysOnTop"));
        UpdatePinToolTip();

        WidgetPinItem.Header = Strings.T("WidgetAlwaysOnTop");
        WidgetShowItem.Header = Strings.T("WidgetShowMainWindow");
        WidgetTrafficItem.Header = Strings.T("TrafficMonitorMenu");
        WidgetGraphItem.Header = Strings.T("WidgetShowGraph");
        WidgetSessionItem.Header = Strings.T("WidgetShowSession");
    }

    public void ApplyAlwaysOnTop(bool onTop)
    {
        Topmost = onTop;
        PinButton.IsChecked = onTop;
        WidgetPinItem.IsChecked = onTop;
        PinIcon.SetResourceReference(Shape.StrokeProperty, onTop ? "AccentBrush" : "SubTextBrush");
        UpdatePinToolTip();
    }

    /// <summary>
    /// Pushes the current firewall state so the widget can mute readings during a killswitch
    /// lockdown, mirroring the main window indicator.
    /// </summary>
    public void SetFirewallState(bool active, FirewallMode mode)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetFirewallState(active, mode));
            return;
        }

        _firewallActive = active;
        _firewallMode = mode;
        RefreshData();
    }

    private void ApplySectionVisibility()
    {
        GraphSection.Visibility = App.Settings.TrafficWidgetShowGraph ? Visibility.Visible : Visibility.Collapsed;
        SessionDivider.Visibility = App.Settings.TrafficWidgetShowSession ? Visibility.Visible : Visibility.Collapsed;
        SessionSection.Visibility = App.Settings.TrafficWidgetShowSession ? Visibility.Visible : Visibility.Collapsed;
        WidgetGraphItem.IsChecked = App.Settings.TrafficWidgetShowGraph;
        WidgetSessionItem.IsChecked = App.Settings.TrafficWidgetShowSession;
    }

    private void UpdatePinToolTip() => PinButton.ToolTip = Strings.T("WidgetAlwaysOnTop");

    #region Data refresh

    private void RefreshData()
    {
        var service = NetworkSpeedService.Instance;
        // Keep the shared sampler alive even when the main window is hidden in the tray and
        // its indicator has stopped it. Start() is a no-op while it is already running.
        service.Start();
        _snapshot = service.CurrentSnapshot;

        var history = service.GetHistory(HistorySlots);
        _historySampleCount = history.Count;
        _peakDown = 0;
        _peakUp = 0;
        foreach (var pt in history)
        {
            if (pt.DownloadBps > _peakDown) _peakDown = pt.DownloadBps;
            if (pt.UploadBps > _peakUp) _peakUp = pt.UploadBps;
        }

        UpdateValues();
        DrawCharts(history);
    }

    /// <summary>True while readings must display zero: offline adapter, no connectivity or killswitch lockdown.</summary>
    private bool IsMuted =>
        !_firewallActive ||
        !_snapshot.IsConnected ||
        !_online ||
        _firewallMode == FirewallMode.Killswitch;

    private void UpdateValues()
    {
        _currentDown = IsMuted ? 0 : _snapshot.DownloadBps;
        _currentUp = IsMuted ? 0 : _snapshot.UploadBps;

        DownValueText.Text = NetworkSpeedService.FormatSpeed(_currentDown);
        UpValueText.Text = NetworkSpeedService.FormatSpeed(_currentUp);
        SessionDownVal.Text = NetworkSpeedService.FormatBytes(_snapshot.TotalBytesReceived);
        SessionUpVal.Text = NetworkSpeedService.FormatBytes(_snapshot.TotalBytesSent);
        AdapterVal.Text = string.IsNullOrWhiteSpace(_snapshot.AdapterName) ? "-" : _snapshot.AdapterName;
        var offline = !_online || !_snapshot.IsConnected || (_firewallMode == FirewallMode.Killswitch && _firewallActive);
        StatusDot.Fill = new SolidColorBrush(offline ? OfflineColor : OnlineColor);
    }

    private void DrawCharts(IReadOnlyList<TrafficHistoryPoint> history)
    {
        // Pad history into a fixed 60-slot array (index 0 = oldest, index 59 = now).
        var downSeries = new double[HistorySlots];
        var upSeries = new double[HistorySlots];

        int offset = HistorySlots - history.Count;
        for (int i = 0; i < history.Count; i++)
        {
            int slot = offset + i;
            if (slot >= 0 && slot < HistorySlots)
            {
                downSeries[slot] = history[i].DownloadBps;
                upSeries[slot] = history[i].UploadBps;
            }
        }

        if (IsMuted)
        {
            downSeries[HistorySlots - 1] = 0;
            upSeries[HistorySlots - 1] = 0;
        }
        else
        {
            downSeries[HistorySlots - 1] = _snapshot.DownloadBps;
            upSeries[HistorySlots - 1] = _snapshot.UploadBps;
        }

        // Shared scale with a 32 KB/s floor so idle traffic stays visible, 15% headroom.
        double maxRate = 32.0 * 1024.0;
        for (int i = 0; i < HistorySlots; i++)
        {
            if (downSeries[i] > maxRate) maxRate = downSeries[i];
            if (upSeries[i] > maxRate) maxRate = upSeries[i];
        }
        double maxScale = maxRate * 1.15;

        if (RowDownCanvas.ActualWidth > 10)
            DrawSeries(RowDownLinePath, RowDownAreaPath, downSeries, maxScale, RowDownCanvas.ActualWidth, RowDownCanvas.ActualHeight);
        if (RowUpCanvas.ActualWidth > 10)
            DrawSeries(RowUpLinePath, RowUpAreaPath, upSeries, maxScale, RowUpCanvas.ActualWidth, RowUpCanvas.ActualHeight);
        if (GraphSection.Visibility == Visibility.Visible && GraphCanvasHost.ActualWidth > 10)
        {
            DrawSeries(CombinedDownLinePath, CombinedDownAreaPath, downSeries, maxScale, GraphCanvasHost.ActualWidth, GraphCanvasHost.ActualHeight);
            DrawSeries(CombinedUpLinePath, CombinedUpAreaPath, upSeries, maxScale, GraphCanvasHost.ActualWidth, GraphCanvasHost.ActualHeight);
        }
    }

    private static void DrawSeries(Path linePath, Path areaPath, double[] series, double maxScale, double width, double height)
    {
        if (height <= 4 || width <= 10) return;

        var pts = new WPoint[series.Length];
        double stepX = width / (series.Length - 1);
        for (int i = 0; i < series.Length; i++)
        {
            double ratio = Math.Clamp(series[i] / maxScale, 0.0, 1.0);
            pts[i] = new WPoint(i * stepX, height - (ratio * (height - 4)) - 2);
        }

        var lineGeom = new StreamGeometry();
        using (var ctx = lineGeom.Open())
        {
            ctx.BeginFigure(pts[0], false, false);
            for (int i = 1; i < series.Length; i++)
            {
                ctx.LineTo(pts[i], true, true);
            }
        }
        lineGeom.Freeze();
        linePath.Data = lineGeom;

        var areaGeom = new StreamGeometry();
        using (var ctx = areaGeom.Open())
        {
            ctx.BeginFigure(new WPoint(0, height), true, true);
            ctx.LineTo(pts[0], true, false);
            for (int i = 1; i < series.Length; i++)
            {
                ctx.LineTo(pts[i], true, true);
            }
            ctx.LineTo(new WPoint(width, height), true, false);
        }
        areaGeom.Freeze();
        areaPath.Data = areaGeom;
    }

    #endregion

    #region Tooltips

    private void DownRow_ToolTipOpening(object? sender, ToolTipEventArgs e) =>
        DownRow.ToolTip = BuildMetricTip(Strings.T("LegendDownload"), _currentDown, _peakDown, Strings.T("TrafficSessionIn"), _snapshot.TotalBytesReceived);

    private void UpRow_ToolTipOpening(object? sender, ToolTipEventArgs e) =>
        UpRow.ToolTip = BuildMetricTip(Strings.T("LegendUpload"), _currentUp, _peakUp, Strings.T("TrafficSessionOut"), _snapshot.TotalBytesSent);

    private void GraphSection_ToolTipOpening(object? sender, ToolTipEventArgs e)
    {
        double peak = Math.Max(_peakDown, _peakUp);
        GraphSection.ToolTip = string.Join("\n",
            Strings.T("WidgetGraphTitle"),
            $"{Strings.T("WidgetTipPeak")}: {NetworkSpeedService.FormatSpeed(peak)}",
            $"{Strings.T("TrafficHistorySubtitle")} · {_historySampleCount}/60");
    }

    private static string BuildMetricTip(string title, double current, double peak, string sessionLabel, long sessionBytes) =>
        string.Join("\n",
            title,
            $"{Strings.T("WidgetTipCurrent")}: {NetworkSpeedService.FormatSpeed(current)}",
            $"{Strings.T("WidgetTipPeak")}: {NetworkSpeedService.FormatSpeed(peak)}",
            $"{sessionLabel} {NetworkSpeedService.FormatBytes(sessionBytes)}",
            Strings.T("WidgetHistoryHint"));

    #endregion

    #region Placement

    /// <summary>
    /// Places the widget for the first time, once its measured size is known. A window using
    /// <c>SizeToContent</c> reports NaN as its height before the first layout pass.
    /// </summary>
    private void ApplyInitialPosition()
    {
        if (_positionApplied) return;
        _positionApplied = true;

        if (App.Settings.TrafficWidgetBoundsSaved)
        {
            MoveOnScreen(App.Settings.TrafficWidgetLeft, App.Settings.TrafficWidgetTop);
            return;
        }

        var area = WorkAreaMaximize.GetWorkAreaDip(this);
        MoveOnScreen(area.Right - Width - DefaultEdgeMargin, area.Bottom - Height - DefaultEdgeMargin);
    }

    /// <summary>
    /// Moves the widget to the requested position, clamped so the whole window stays inside the
    /// work area of the nearest monitor. A position saved on a monitor that is now gone therefore
    /// lands on the closest existing display instead of off screen.
    /// </summary>
    private void MoveOnScreen(double left, double top)
    {
        var area = WorkAreaMaximize.GetWorkAreaDip(this);
        Left = Math.Clamp(left, area.Left, Math.Max(area.Left, area.Right - Width));
        Top = Math.Clamp(top, area.Top, Math.Max(area.Top, area.Bottom - Height));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowMessageHook);
        }
    }

    private void Widget_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            // Coming back from a hidden period, the display layout may have changed meanwhile.
            if (_positionApplied) MoveOnScreen(Left, Top);
            RefreshData();
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }
    }

    private void OnConnectivityChanged(bool online)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnConnectivityChanged(online));
            return;
        }

        _online = online;
        RefreshData();
    }

    private IntPtr WindowMessageHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_DISPLAYCHANGE)
        {
            // A monitor was added, removed or rearranged while the widget is on screen.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_positionApplied) MoveOnScreen(Left, Top);
            }));
        }

        return IntPtr.Zero;
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // SizeToContent means taller content (a longer translation, a different DPI) could push
        // the widget past the bottom edge, so re-clamp once the new size is known.
        if (_positionApplied && e.HeightChanged) MoveOnScreen(Left, Top);
    }

    private void Widget_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
        ConnectivityService.Instance.ConnectivityChanged -= OnConnectivityChanged;
        if (!_positionApplied) return;

        // The widget only closes at application shutdown; persist its last position.
        App.Settings.TrafficWidgetLeft = Left;
        App.Settings.TrafficWidgetTop = Top;
        App.Settings.TrafficWidgetBoundsSaved = true;
        App.Settings.Save();
    }

    private void Window_LocationChanged(object sender, EventArgs e) => PositionChanged?.Invoke(Left, Top);

    #endregion

    #region Interaction

    private void Root_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // DragMove throws when the button was released between the event and the call.
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => CloseRequested?.Invoke();

    private void PinButton_Click(object sender, RoutedEventArgs e) =>
        AlwaysOnTopChanged?.Invoke(PinButton.IsChecked == true);

    private void WidgetPinItem_Click(object sender, RoutedEventArgs e)
    {
        var onTop = WidgetPinItem.IsChecked;
        ApplyAlwaysOnTop(onTop);
        AlwaysOnTopChanged?.Invoke(onTop);
    }

    private void WidgetShowItem_Click(object sender, RoutedEventArgs e) => ShowMainWindowRequested?.Invoke();

    private void WidgetTrafficItem_Click(object sender, RoutedEventArgs e) => OpenTrafficMonitorRequested?.Invoke();

    private void WidgetGraphItem_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.TrafficWidgetShowGraph = WidgetGraphItem.IsChecked == true;
        App.Settings.Save();
        ApplySectionVisibility();
    }

    private void WidgetSessionItem_Click(object sender, RoutedEventArgs e)
    {
        App.Settings.TrafficWidgetShowSession = WidgetSessionItem.IsChecked == true;
        App.Settings.Save();
        ApplySectionVisibility();
    }

    #endregion
}
