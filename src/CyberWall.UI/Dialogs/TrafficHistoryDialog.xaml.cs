using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CyberWall.Common.I18n;
using CyberWall.UI.Services;
using WPoint = System.Windows.Point;

namespace CyberWall.UI.Dialogs;

public partial class TrafficHistoryDialog : Window
{
    public TrafficHistoryDialog()
    {
        InitializeComponent();
        CyberWallWindowChrome.Apply(this, 12);
        Icon = AppIconHelper.CreateShieldImageSource(64);

        RefreshLanguage();

        Loaded += (_, _) =>
        {
            NetworkSpeedService.Instance.SpeedUpdated += OnSpeedUpdated;
            UpdateData();
        };

        Closed += (_, _) =>
        {
            NetworkSpeedService.Instance.SpeedUpdated -= OnSpeedUpdated;
        };
    }

    private void OnSpeedUpdated(NetworkSpeedSnapshot snapshot)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnSpeedUpdated(snapshot));
            return;
        }

        UpdateData();
    }

    private void ChartContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateData();
    }

    public void RefreshLanguage()
    {
        DialogTitleText.Text = Strings.T("TrafficHistoryTitle");
        DialogSubtitleText.Text = Strings.T("TrafficHistorySubtitle");
        DownTitleLbl.Text = Strings.T("LegendDownload");
        UpTitleLbl.Text = Strings.T("LegendUpload");
        LegendDownText.Text = Strings.T("LegendDownload");
        LegendUpText.Text = Strings.T("LegendUpload");

        var peakText = Strings.T("PeakSpeed") + " (60s)";
        var avgText = Strings.T("AvgSpeed");
        var sessionText = Strings.T("TrafficSessionIn").Replace(":", "");
        if (sessionText.Contains("recibido", StringComparison.OrdinalIgnoreCase) || sessionText.Contains("received", StringComparison.OrdinalIgnoreCase))
            sessionText = Strings.Current == Lang.Es ? "Sesión" : "Session";

        DownPeakLbl.Text = peakText;
        UpPeakLbl.Text = peakText;
        DownAvgLbl.Text = avgText;
        UpAvgLbl.Text = avgText;
        DownTotalLbl.Text = sessionText;
        UpTotalLbl.Text = sessionText;

        TimeNowLbl.Text = Strings.T("TimeNow");
        AdapterHeaderLbl.Text = Strings.T("TrafficActiveAdapter");
        CloseBtnText.Text = Strings.T("Close");
    }

    private void UpdateData()
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
            UpAvgVal.Text = NetworkSpeedService.FormatSpeed(sumUp / history.Count);
        }
        else
        {
            DownPeakVal.Text = NetworkSpeedService.FormatSpeed(snapshot.DownloadBps);
            UpPeakVal.Text = NetworkSpeedService.FormatSpeed(snapshot.UploadBps);
            DownAvgVal.Text = NetworkSpeedService.FormatSpeed(snapshot.DownloadBps);
            UpAvgVal.Text = NetworkSpeedService.FormatSpeed(snapshot.UploadBps);
        }

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

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
