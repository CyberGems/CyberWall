using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.UI.Services;
using WPoint = System.Windows.Point;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;
using MenuItem = System.Windows.Controls.MenuItem;
using ContextMenu = System.Windows.Controls.ContextMenu;
using RadioButton = System.Windows.Controls.RadioButton;
using Brush = System.Windows.Media.Brush;
using WpfClipboard = System.Windows.Clipboard;

namespace CyberWall.UI.Views;

public partial class TrafficMonitorView : UserControl
{
    public event Action<string>? QuickBlockRequested;

    private static readonly Geometry EthernetGeometry = Geometry.Parse("M 15 20 L 18 17 H 20 C 21.1 17 22 16.1 22 15 V 6 C 22 4.9 21.1 4 20 4 H 4 C 2.9 4 2 4.9 2 6 V 15 C 2 16.1 2.9 17 4 17 H 6 L 9 20 Z M 6 8 V 10 M 10 8 V 10 M 14 8 V 10 M 18 8 V 10");
    private static readonly Geometry WifiGeometry = Geometry.Parse("M 12,19 L 12,19.01 M 8.5,15.5 C 10.5,13.8 13.5,13.8 15.5,15.5 M 5.5,12 C 9.2,8.5 14.8,8.5 18.5,12 M 2.5,8.5 C 8,3.5 16,3.5 21.5,8.5");
    private static readonly Geometry PauseGeometry = Geometry.Parse("M 6 4 H 8 V 16 H 6 Z M 12 4 H 14 V 16 H 12 Z");
    private static readonly Geometry PlayGeometry = Geometry.Parse("M 7 4 L 17 10 L 7 16 Z");

    static TrafficMonitorView()
    {
        EthernetGeometry.Freeze();
        WifiGeometry.Freeze();
        PauseGeometry.Freeze();
        PlayGeometry.Freeze();
    }

    private readonly DispatcherTimer _refreshTimer;
    private bool _isActive;
    private int _timeWindowSeconds = 60;
    private bool _isChartPaused;
    private Border? _activeRowBorder;
    private string? _activeMenuAppPath;
    private ContextMenu? _activeContextMenu;

    public TrafficMonitorView()
    {
        InitializeComponent();
        RefreshLanguage();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _refreshTimer.Tick += (_, _) =>
        {
            if (_isActive) UpdateData();
        };

        Loaded += (_, _) =>
        {
            if (_isActive) UpdateData();
        };
    }

    public void Activate()
    {
        if (_isActive) return;
        _isActive = true;
        NetworkSpeedService.Instance.SpeedUpdated += OnSpeedUpdated;
        _refreshTimer.Start();
        UpdateData();
    }

    public void Deactivate()
    {
        if (!_isActive) return;
        _isActive = false;
        NetworkSpeedService.Instance.SpeedUpdated -= OnSpeedUpdated;
        _refreshTimer.Stop();
    }

    private void OnSpeedUpdated(NetworkSpeedSnapshot snapshot)
    {
        if (!_isActive) return;

        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (_isActive) UpdateData();
            });
            return;
        }

        UpdateData();
    }

    public void RefreshLanguage()
    {
        DownTitleLbl.Text = Strings.T("LegendDownload");
        UpTitleLbl.Text = Strings.T("LegendUpload");
        ChartTitleText.Text = $"{Strings.T("TrafficTelemetryTitle")} ({(_timeWindowSeconds <= 60 ? "60s" : "5 min")})";
        LegendDownText.Text = Strings.T("LegendDownload");
        LegendUpText.Text = Strings.T("LegendUpload");

        Time1mBtn.Content = Strings.T("ChartTime1m");
        Time1mBtn.ToolTip = Strings.T("ChartTime1mTooltip");
        Time5mBtn.Content = Strings.T("ChartTime5m");
        Time5mBtn.ToolTip = Strings.T("ChartTime5mTooltip");
        ChartPeakPill.ToolTip = Strings.T("ChartScaleTooltip");
        PauseChartBtn.ToolTip = _isChartPaused ? Strings.T("ChartResumeTooltip") : Strings.T("ChartPauseTooltip");

        var peakText = Strings.T("PeakSpeed") + " (60s)";
        var avgText = Strings.T("AvgSpeed");
        var sessionText = Strings.T("StatsSessionData");

        DownPeakLbl.Text = $"{peakText}: ";
        UpPeakLbl.Text = $"{peakText}: ";
        DownAvgLbl.Text = $"{avgText}: ";
        SessionTitleLbl.Text = sessionText;

        AdapterHeaderLbl.Text = Strings.T("TrafficActiveAdapter").Replace(":", "");
        AppsActiveLbl.Text = Strings.T("ActiveTransmittingApps") + ": ";
        ConsumersTitle.Text = Strings.T("TopConsumersHeader");
        ConsumersSubTitle.Text = Strings.T("TopConsumersSubtitle");
        EmptyStateText.Text = Strings.T("BandwidthNoTraffic");

        LiveTrafficGraph?.RefreshLanguage();
        UpdateScaleText();

        try
        {
            if (FindResource("TrafficItemContextMenu") is ContextMenu cm && cm.Items.Count >= 3)
            {
                if (cm.Items[0] is MenuItem item0) item0.Header = Strings.T("SearchOnline");
                if (cm.Items[1] is MenuItem item1) item1.Header = Strings.T("CopyPathTooltip");
                if (cm.Items[2] is MenuItem item2) item2.Header = Strings.T("OpenExeFolder");
            }
        }
        catch { }
    }

    public void UpdateData()
    {
        var snapshot = NetworkSpeedService.Instance.CurrentSnapshot;
        var history = NetworkSpeedService.Instance.GetHistory(_timeWindowSeconds);

        // Speeds and totals
        DownCurrentSpeedVal.Text = NetworkSpeedService.FormatSpeed(snapshot.DownloadBps);
        UpCurrentSpeedVal.Text = NetworkSpeedService.FormatSpeed(snapshot.UploadBps);
        DownTotalVal.Text = NetworkSpeedService.FormatBytes(snapshot.TotalBytesReceived);
        UpTotalVal.Text = NetworkSpeedService.FormatBytes(snapshot.TotalBytesSent);
        AdapterValueLbl.Text = string.IsNullOrWhiteSpace(snapshot.AdapterName) ? "None" : snapshot.AdapterName;

        var adapterName = snapshot.AdapterName ?? string.Empty;
        bool isWifi = adapterName.Contains("wi-fi", StringComparison.OrdinalIgnoreCase) ||
                     adapterName.Contains("wireless", StringComparison.OrdinalIgnoreCase) ||
                     adapterName.Contains("802.11", StringComparison.OrdinalIgnoreCase) ||
                     adapterName.Contains("wlan", StringComparison.OrdinalIgnoreCase);

        AdapterIconPath.Data = isWifi ? WifiGeometry : EthernetGeometry;

        // Calculate peak & avg over history
        double peakDown = 0;
        double peakUp = 0;
        double sumDown = 0;
        double sumUp = 0;

        if (history.Count > 0)
        {
            foreach (var pt in history)
            {
                if (pt.DownloadBps > peakDown) peakDown = pt.DownloadBps;
                if (pt.UploadBps > peakUp) peakUp = pt.UploadBps;
                sumDown += pt.DownloadBps;
                sumUp += pt.UploadBps;
            }
            DownPeakVal.Text = NetworkSpeedService.FormatSpeed(peakDown);
            UpPeakVal.Text = NetworkSpeedService.FormatSpeed(peakUp);
            DownAvgVal.Text = NetworkSpeedService.FormatSpeed(sumDown / history.Count);
        }
        else
        {
            DownPeakVal.Text = NetworkSpeedService.FormatSpeed(snapshot.DownloadBps);
            UpPeakVal.Text = NetworkSpeedService.FormatSpeed(snapshot.UploadBps);
            DownAvgVal.Text = NetworkSpeedService.FormatSpeed(snapshot.DownloadBps);
        }

        // Active consumers list
        var top = ProcessBandwidthService.Instance.GetTopConsumers(12);

        // If context menu is open, handle target item lifecycle
        if (_activeContextMenu != null && _activeContextMenu.IsOpen && !string.IsNullOrEmpty(_activeMenuAppPath))
        {
            bool itemStillPresent = top.Any(x => string.Equals(x.AppPath, _activeMenuAppPath, StringComparison.OrdinalIgnoreCase));
            if (!itemStillPresent)
            {
                // Item disappeared from active traffic: close the context menu immediately
                _activeContextMenu.IsOpen = false;
                ClearActiveMenuHighlight();
                _activeContextMenu = null;
                _activeMenuAppPath = null;
            }
            else
            {
                // Process is still active: freeze list re-ordering so the row does not shift
                // or jump from under the user's cursor while they interact with the menu.
                TotalThroughputText.Text = $"↓ {NetworkSpeedService.FormatSpeed(snapshot.DownloadBps)}  ↑ {NetworkSpeedService.FormatSpeed(snapshot.UploadBps)}";
                if (!_isChartPaused)
                {
                    LiveTrafficGraph?.UpdateData(history, snapshot);
                }
                UpdateScaleText();
                return;
            }
        }

        ConsumersList.ItemsSource = top;
        bool hasConsumers = top.Count > 0;
        ConsumersList.Visibility = hasConsumers ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateBorder.Visibility = hasConsumers ? Visibility.Collapsed : Visibility.Visible;
        AppsActiveCountVal.Text = $"{top.Count} apps";
        TotalThroughputText.Text = $"↓ {NetworkSpeedService.FormatSpeed(snapshot.DownloadBps)}  ↑ {NetworkSpeedService.FormatSpeed(snapshot.UploadBps)}";

        // Update live graph
        if (!_isChartPaused)
        {
            LiveTrafficGraph?.UpdateData(history, snapshot);
        }
        UpdateScaleText();
    }

    private void UpdateScaleText()
    {
        double scale = LiveTrafficGraph?.CurrentMaxScale ?? 0;
        string formatted = NetworkSpeedService.FormatSpeed(scale);
        ChartPeakRateText.Text = $"▲ {Strings.T("ChartScaleLabel", formatted)}";
    }

    private void PauseChartBtn_Click(object sender, RoutedEventArgs e)
    {
        _isChartPaused = !_isChartPaused;
        if (LiveTrafficGraph != null)
        {
            LiveTrafficGraph.IsPaused = _isChartPaused;
        }

        PauseBtnIcon.Data = _isChartPaused ? PlayGeometry : PauseGeometry;
        PauseBtnIcon.Fill = _isChartPaused ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("SubTextBrush");
        PauseChartBtn.ToolTip = _isChartPaused ? Strings.T("ChartResumeTooltip") : Strings.T("ChartPauseTooltip");
    }

    private void TimeWindow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton btn && int.TryParse(btn.Tag?.ToString(), out int seconds))
        {
            _timeWindowSeconds = seconds;
            ChartTitleText.Text = $"{Strings.T("TrafficTelemetryTitle")} ({(_timeWindowSeconds <= 60 ? "60s" : "5 min")})";
            var snapshot = NetworkSpeedService.Instance.CurrentSnapshot;
            var history = NetworkSpeedService.Instance.GetHistory(_timeWindowSeconds);
            LiveTrafficGraph?.SwitchTimeframe(seconds, history, snapshot);
            UpdateScaleText();
        }
    }

    private void QuickBlock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string appPath && !string.IsNullOrWhiteSpace(appPath))
        {
            QuickBlockRequested?.Invoke(appPath);
            UpdateData();
        }
    }

    private static ProcessBandwidthUsage? GetItemFromMenuSender(object sender)
    {
        if (sender is MenuItem menuItem)
        {
            if (menuItem.DataContext is ProcessBandwidthUsage item) return item;
            if (menuItem.Parent is ContextMenu cm && cm.PlacementTarget is FrameworkElement fe && fe.DataContext is ProcessBandwidthUsage targetItem)
                return targetItem;
        }
        return null;
    }

    private void ContextSearch_Click(object sender, RoutedEventArgs e)
    {
        var item = GetItemFromMenuSender(sender);
        if (item != null && !string.IsNullOrWhiteSpace(item.DisplayName))
        {
            try
            {
                var query = Uri.EscapeDataString($"{item.DisplayName} process windows");
                Process.Start(new ProcessStartInfo($"https://www.google.com/search?q={query}") { UseShellExecute = true });
            }
            catch { }
        }
    }

    private void ContextCopyPath_Click(object sender, RoutedEventArgs e)
    {
        var item = GetItemFromMenuSender(sender);
        if (item != null && !string.IsNullOrWhiteSpace(item.AppPath))
        {
            try
            {
                WpfClipboard.SetText(item.AppPath);
            }
            catch { }
        }
    }

    private void ContextFolder_Click(object sender, RoutedEventArgs e)
    {
        var item = GetItemFromMenuSender(sender);
        if (item != null && !string.IsNullOrWhiteSpace(item.AppPath) && File.Exists(item.AppPath))
        {
            try
            {
                Process.Start("explorer.exe", $"/select,\"{item.AppPath}\"");
            }
            catch { }
        }
    }

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu cm)
        {
            _activeContextMenu = cm;
            if (cm.PlacementTarget is Border border)
            {
                _activeRowBorder = border;
                if (border.DataContext is ProcessBandwidthUsage item)
                {
                    _activeMenuAppPath = item.AppPath;
                }

                // Highlight source row with vibrant accent border and elevated card background
                border.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
                border.Background = (System.Windows.Media.Brush)FindResource("CardBrush");
            }
        }
    }

    private void ContextMenu_Closed(object sender, RoutedEventArgs e)
    {
        ClearActiveMenuHighlight();
        _activeContextMenu = null;
        _activeMenuAppPath = null;
        UpdateData();
    }

    private void ClearActiveMenuHighlight()
    {
        if (_activeRowBorder != null)
        {
            _activeRowBorder.ClearValue(Border.BorderBrushProperty);
            _activeRowBorder.ClearValue(Border.BackgroundProperty);
            _activeRowBorder = null;
        }
    }
}
