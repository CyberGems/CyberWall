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
using WpfClipboard = System.Windows.Clipboard;

namespace CyberWall.UI.Views;

public partial class TrafficMonitorView : UserControl
{
    public event Action<string>? QuickBlockRequested;

    private readonly DispatcherTimer _refreshTimer;
    private bool _isActive;
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

    private void ChartContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isActive) UpdateData();
    }

    public void RefreshLanguage()
    {
        DownTitleLbl.Text = Strings.T("LegendDownload");
        UpTitleLbl.Text = Strings.T("LegendUpload");
        ChartTitleText.Text = Strings.T("TrafficTelemetryTitle");
        LegendDownText.Text = Strings.T("LegendDownload");
        LegendUpText.Text = Strings.T("LegendUpload");

        var peakText = Strings.T("PeakSpeed") + " (60s)";
        var avgText = Strings.T("AvgSpeed");
        var sessionText = Strings.T("StatsSessionData");

        DownPeakLbl.Text = $"{peakText}: ";
        UpPeakLbl.Text = $"{peakText}: ";
        DownAvgLbl.Text = $"{avgText}: ";
        SessionTitleLbl.Text = sessionText;

        TimeNowLbl.Text = Strings.T("TimeNow");
        AdapterHeaderLbl.Text = Strings.T("TrafficActiveAdapter").Replace(":", "");
        AppsActiveLbl.Text = Strings.T("ActiveTransmittingApps") + ": ";
        ConsumersTitle.Text = Strings.T("TopConsumersHeader");
        ConsumersSubTitle.Text = Strings.T("TopConsumersSubtitle");
        EmptyStateText.Text = Strings.T("BandwidthNoTraffic");

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
        var history = NetworkSpeedService.Instance.GetHistory(60);

        // Speeds and totals
        DownCurrentSpeedVal.Text = NetworkSpeedService.FormatSpeed(snapshot.DownloadBps);
        UpCurrentSpeedVal.Text = NetworkSpeedService.FormatSpeed(snapshot.UploadBps);
        DownTotalVal.Text = NetworkSpeedService.FormatBytes(snapshot.TotalBytesReceived);
        UpTotalVal.Text = NetworkSpeedService.FormatBytes(snapshot.TotalBytesSent);
        AdapterValueLbl.Text = string.IsNullOrWhiteSpace(snapshot.AdapterName) ? "None" : snapshot.AdapterName;

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
                DrawChart(history, snapshot);
                return;
            }
        }

        ConsumersList.ItemsSource = top;
        bool hasConsumers = top.Count > 0;
        ConsumersList.Visibility = hasConsumers ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateBorder.Visibility = hasConsumers ? Visibility.Collapsed : Visibility.Visible;
        AppsActiveCountVal.Text = $"{top.Count} apps";
        TotalThroughputText.Text = $"↓ {NetworkSpeedService.FormatSpeed(snapshot.DownloadBps)}  ↑ {NetworkSpeedService.FormatSpeed(snapshot.UploadBps)}";

        // Draw Chart
        DrawChart(history, snapshot);
    }

    private void DrawChart(IReadOnlyList<TrafficHistoryPoint> history, NetworkSpeedSnapshot snapshot)
    {
        var width = ChartContainer.ActualWidth;
        var height = ChartContainer.ActualHeight;

        if (width <= 10 || height <= 10) return;

        // Pad history into fixed 60-second array (index 0 = 59s ago, index 59 = now)
        const int totalSlots = 60;
        var downSeries = new double[totalSlots];
        var upSeries = new double[totalSlots];

        int historyCount = history.Count;
        int offset = totalSlots - historyCount;

        for (int i = 0; i < historyCount; i++)
        {
            int slot = offset + i;
            if (slot >= 0 && slot < totalSlots)
            {
                downSeries[slot] = history[i].DownloadBps;
                upSeries[slot] = history[i].UploadBps;
            }
        }

        // Make sure the very latest slot matches current snapshot
        downSeries[totalSlots - 1] = snapshot.DownloadBps;
        upSeries[totalSlots - 1] = snapshot.UploadBps;

        // Dynamic scale with min headroom of 32 KB/s
        double maxRate = 32.0 * 1024.0;
        for (int i = 0; i < totalSlots; i++)
        {
            if (downSeries[i] > maxRate) maxRate = downSeries[i];
            if (upSeries[i] > maxRate) maxRate = upSeries[i];
        }

        double maxScale = maxRate * 1.15; // 15% visual headroom
        ChartPeakRateText.Text = $"▲ {NetworkSpeedService.FormatSpeed(maxScale)}";

        // Build Geometry
        var downPoints = new WPoint[totalSlots];
        var upPoints = new WPoint[totalSlots];

        double stepX = width / (totalSlots - 1);

        for (int i = 0; i < totalSlots; i++)
        {
            double x = i * stepX;
            double downRatio = Math.Clamp(downSeries[i] / maxScale, 0.0, 1.0);
            double upRatio = Math.Clamp(upSeries[i] / maxScale, 0.0, 1.0);

            // Invert Y coordinate so 0 is at bottom
            double yDown = height - (downRatio * (height - 4)) - 2;
            double yUp = height - (upRatio * (height - 4)) - 2;

            downPoints[i] = new WPoint(x, yDown);
            upPoints[i] = new WPoint(x, yUp);
        }

        // Download Line & Area
        var downLineGeom = new StreamGeometry();
        using (var ctx = downLineGeom.Open())
        {
            ctx.BeginFigure(downPoints[0], false, false);
            for (int i = 1; i < totalSlots; i++)
            {
                ctx.LineTo(downPoints[i], true, true);
            }
        }
        downLineGeom.Freeze();
        DownloadLinePath.Data = downLineGeom;

        var downAreaGeom = new StreamGeometry();
        using (var ctx = downAreaGeom.Open())
        {
            ctx.BeginFigure(new WPoint(0, height), true, true);
            ctx.LineTo(downPoints[0], true, false);
            for (int i = 1; i < totalSlots; i++)
            {
                ctx.LineTo(downPoints[i], true, true);
            }
            ctx.LineTo(new WPoint(width, height), true, false);
        }
        downAreaGeom.Freeze();
        DownloadAreaPath.Data = downAreaGeom;

        // Upload Line & Area
        var upLineGeom = new StreamGeometry();
        using (var ctx = upLineGeom.Open())
        {
            ctx.BeginFigure(upPoints[0], false, false);
            for (int i = 1; i < totalSlots; i++)
            {
                ctx.LineTo(upPoints[i], true, true);
            }
        }
        upLineGeom.Freeze();
        UploadLinePath.Data = upLineGeom;

        var upAreaGeom = new StreamGeometry();
        using (var ctx = upAreaGeom.Open())
        {
            ctx.BeginFigure(new WPoint(0, height), true, true);
            ctx.LineTo(upPoints[0], true, false);
            for (int i = 1; i < totalSlots; i++)
            {
                ctx.LineTo(upPoints[i], true, true);
            }
            ctx.LineTo(new WPoint(width, height), true, false);
        }
        upAreaGeom.Freeze();
        UploadAreaPath.Data = upAreaGeom;
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
