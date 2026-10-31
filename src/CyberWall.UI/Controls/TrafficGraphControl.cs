using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CyberWall.Common.I18n;
using CyberWall.UI.Services;
using Point = System.Windows.Point;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace CyberWall.UI.Controls;

/// <summary>
/// High-performance animated network traffic graph control.
/// Renders smooth GlassWire-style organic splines with glowing gradient fills,
/// continuous hardware-accelerated sliding animation, grid lines, and interactive hover inspection.
/// Supports 1-minute (60s) and 5-minute (300s) views, pause/freeze inspection, and crisp vector typography.
/// </summary>
public class TrafficGraphControl : AnimatedHistoryGraph
{
    public static readonly DependencyProperty DownloadColorProperty = DependencyProperty.Register(
        nameof(DownloadColor), typeof(Color), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(Color.FromRgb(0, 229, 255), FrameworkPropertyMetadataOptions.AffectsRender, OnPaintsChanged));

    public static readonly DependencyProperty UploadColorProperty = DependencyProperty.Register(
        nameof(UploadColor), typeof(Color), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(Color.FromRgb(34, 197, 94), FrameworkPropertyMetadataOptions.AffectsRender, OnPaintsChanged));

    public static readonly DependencyProperty MaxSamplesProperty = DependencyProperty.Register(
        nameof(MaxSamples), typeof(int), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(60, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CompactModeProperty = DependencyProperty.Register(
        nameof(CompactMode), typeof(bool), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowGridProperty = DependencyProperty.Register(
        nameof(ShowGrid), typeof(bool), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowTimeLabelsProperty = DependencyProperty.Register(
        nameof(ShowTimeLabels), typeof(bool), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowHoverInspectionProperty = DependencyProperty.Register(
        nameof(ShowHoverInspection), typeof(bool), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowScaleLabelsProperty = DependencyProperty.Register(
        nameof(ShowScaleLabels), typeof(bool), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlotBackgroundProperty = DependencyProperty.Register(
        nameof(PlotBackground), typeof(Brush), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PlotBorderBrushProperty = DependencyProperty.Register(
        nameof(PlotBorderBrush), typeof(Brush), typeof(TrafficGraphControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Color DownloadColor
    {
        get => (Color)GetValue(DownloadColorProperty);
        set => SetValue(DownloadColorProperty, value);
    }

    public Color UploadColor
    {
        get => (Color)GetValue(UploadColorProperty);
        set => SetValue(UploadColorProperty, value);
    }

    public int MaxSamples
    {
        get => (int)GetValue(MaxSamplesProperty);
        set => SetValue(MaxSamplesProperty, value);
    }

    public bool CompactMode
    {
        get => (bool)GetValue(CompactModeProperty);
        set => SetValue(CompactModeProperty, value);
    }

    public bool ShowGrid
    {
        get => (bool)GetValue(ShowGridProperty);
        set => SetValue(ShowGridProperty, value);
    }

    public bool ShowTimeLabels
    {
        get => (bool)GetValue(ShowTimeLabelsProperty);
        set => SetValue(ShowTimeLabelsProperty, value);
    }

    public bool ShowHoverInspection
    {
        get => (bool)GetValue(ShowHoverInspectionProperty);
        set => SetValue(ShowHoverInspectionProperty, value);
    }

    public bool ShowScaleLabels
    {
        get => (bool)GetValue(ShowScaleLabelsProperty);
        set => SetValue(ShowScaleLabelsProperty, value);
    }

    public Brush? PlotBackground
    {
        get => (Brush?)GetValue(PlotBackgroundProperty);
        set => SetValue(PlotBackgroundProperty, value);
    }

    public Brush? PlotBorderBrush
    {
        get => (Brush?)GetValue(PlotBorderBrushProperty);
        set => SetValue(PlotBorderBrushProperty, value);
    }

    public bool IsPaused
    {
        get => _isPaused;
        set
        {
            if (_isPaused != value)
            {
                _isPaused = value;
                IsAnimationEnabled = !_isPaused;
                InvalidateVisual();
            }
        }
    }

    public double CurrentMaxScale => _targetMax;

    private bool _isPaused;
    private float[]? _downSeries;
    private float[]? _upSeries;
    private double _targetMax = 32.0 * 1024.0;
    private double _renderedMax = 32.0 * 1024.0;

    private readonly TrafficSeriesDrawing _downDrawing = new();
    private readonly TrafficSeriesDrawing _upDrawing = new();

    private Point? _hoverPos;
    private long _lastHoverRenderTimestamp;

    private Color? _cachedDownColor;
    private Color? _cachedUpColor;
    private Brush? _downFill;
    private Pen? _downPen;
    private Brush? _upFill;
    private Pen? _upPen;

    private static readonly Typeface AxisTypeface = new(
        new FontFamily("Segoe UI Variable Text, Segoe UI, -apple-system, Roboto, sans-serif"),
        FontStyles.Normal,
        FontWeights.Normal,
        FontStretches.Normal);

    private static readonly Typeface AxisBoldTypeface = new(
        new FontFamily("Segoe UI Variable Text, Segoe UI, -apple-system, Roboto, sans-serif"),
        FontStyles.Normal,
        FontWeights.SemiBold,
        FontStretches.Normal);

    private static readonly SolidColorBrush DefaultBackgroundBrush = CreateBrush(Color.FromRgb(17, 28, 46));
    private static readonly SolidColorBrush DefaultBorderBrush = CreateBrush(Color.FromArgb(50, 42, 67, 106));
    private static readonly Pen DefaultBorderPen = CreatePen(DefaultBorderBrush, 1.0);
    private static readonly SolidColorBrush GridBrush = CreateBrush(Color.FromArgb(20, 255, 255, 255));
    private static readonly Pen GridPen = CreatePen(GridBrush, 1.0);
    private static readonly SolidColorBrush AxisTextBrush = CreateBrush(Color.FromRgb(139, 162, 196));
    private static readonly Pen ClockPen = CreatePen(AxisTextBrush, 1.1);
    private static readonly SolidColorBrush AxisAccentBrush = CreateBrush(Color.FromRgb(0, 229, 255));
    private static readonly SolidColorBrush TooltipBgBrush = CreateBrush(Color.FromArgb(235, 12, 20, 36));
    private static readonly SolidColorBrush TooltipBorderBrush = CreateBrush(Color.FromArgb(120, 42, 67, 106));
    private static readonly Pen TooltipBorderPen = CreatePen(TooltipBorderBrush, 1.0);
    private static readonly SolidColorBrush WhiteBrush = CreateBrush(Colors.White);

    private static readonly SolidColorBrush PausedBgBrush = CreateBrush(Color.FromArgb(230, 45, 26, 12));
    private static readonly SolidColorBrush PausedBorderBrush = CreateBrush(Color.FromRgb(245, 158, 11));
    private static readonly Pen PausedBorderPen = CreatePen(PausedBorderBrush, 1.0);
    private static readonly SolidColorBrush PausedTextBrush = CreateBrush(Color.FromRgb(251, 191, 36));

    public TrafficGraphControl()
    {
        SnapsToDevicePixels = true;
        ClipToBounds = true;
    }

    public void RefreshLanguage()
    {
        InvalidateVisual();
    }

    /// <summary>
    /// Switches the time window (e.g. 60s vs 300s) instantaneously, immediately recalculating the series,
    /// scale ceiling, and baseline state so there is zero freeze or delay.
    /// </summary>
    public void SwitchTimeframe(int newMaxSamples, IReadOnlyList<TrafficHistoryPoint> history, NetworkSpeedSnapshot snapshot)
    {
        MaxSamples = newMaxSamples;
        int capacity = Math.Max(2, newMaxSamples);

        var down = new float[capacity];
        var up = new float[capacity];

        int historyCount = history.Count;
        int offset = capacity - historyCount;

        for (int i = 0; i < historyCount; i++)
        {
            int slot = offset + i;
            if (slot >= 0 && slot < capacity)
            {
                down[slot] = (float)history[i].DownloadBps;
                up[slot] = (float)history[i].UploadBps;
            }
        }

        down[capacity - 1] = (float)snapshot.DownloadBps;
        up[capacity - 1] = (float)snapshot.UploadBps;

        // Auto-scale with 15% headroom and a 32 KB/s minimum ceiling
        double maxRate = 32.0 * 1024.0;
        for (int i = 0; i < capacity; i++)
        {
            if (down[i] > maxRate) maxRate = down[i];
            if (up[i] > maxRate) maxRate = up[i];
        }

        _targetMax = maxRate * 1.15;
        _renderedMax = _targetMax;

        _downSeries = down;
        _upSeries = up;

        _downDrawing.Reset();
        _upDrawing.Reset();

        SetCurrentState(snapshot, down, up, capacity);
        InvalidateVisual();
    }

    public void UpdateData(IReadOnlyList<TrafficHistoryPoint> history, NetworkSpeedSnapshot snapshot)
    {
        if (_isPaused) return;

        int capacity = Math.Max(2, MaxSamples);
        var down = new float[capacity];
        var up = new float[capacity];

        int historyCount = history.Count;
        int offset = capacity - historyCount;

        for (int i = 0; i < historyCount; i++)
        {
            int slot = offset + i;
            if (slot >= 0 && slot < capacity)
            {
                down[slot] = (float)history[i].DownloadBps;
                up[slot] = (float)history[i].UploadBps;
            }
        }

        // Ensure the latest point reflects the real-time snapshot
        down[capacity - 1] = (float)snapshot.DownloadBps;
        up[capacity - 1] = (float)snapshot.UploadBps;

        // Auto-scale with 15% headroom and a 32 KB/s minimum ceiling
        double maxRate = 32.0 * 1024.0;
        for (int i = 0; i < capacity; i++)
        {
            if (down[i] > maxRate) maxRate = down[i];
            if (up[i] > maxRate) maxRate = up[i];
        }

        _targetMax = maxRate * 1.15;
        if (Math.Abs(_renderedMax - 32.0 * 1024.0) < 1.0)
        {
            _renderedMax = _targetMax;
        }

        _downSeries = down;
        _upSeries = up;

        BeginSample(snapshot, TimeSpan.FromSeconds(1), down, up, capacity);
    }

    protected override void OnAnimationFrame(double elapsedSeconds, bool finished)
    {
        if (_isPaused) return;
        // Smooth scale transition so scale adjustments don't jump vertically
        _renderedMax += (_targetMax - _renderedMax) * 0.18;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (CompactMode || !ShowHoverInspection) return;

        _hoverPos = e.GetPosition(this);
        var now = Stopwatch.GetTimestamp();
        if ((now - _lastHoverRenderTimestamp) * 60 < Stopwatch.Frequency)
        {
            return;
        }

        _lastHoverRenderTimestamp = now;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoverPos.HasValue)
        {
            _hoverPos = null;
            InvalidateVisual();
        }
    }

    [SuppressMessage("Naming", "CA1725", Justification = "The parameter name is part of the WPF override contract.")]
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        double w = ActualWidth;
        double h = ActualHeight;
        if (w < 40 || h < 30) return;

        EnsurePaints();

        double timeAxisHeight = (ShowTimeLabels && !CompactMode) ? 19.0 : 0.0;
        double plotWidth = w;
        double plotHeight = Math.Max(20.0, h - timeAxisHeight);
        var plotRect = new Rect(0, 0, plotWidth, plotHeight);

        // 1. Draw Plot Card Background and subtle border
        var bgBrush = PlotBackground ?? DefaultBackgroundBrush;
        var borderPen = PlotBorderBrush != null ? new Pen(PlotBorderBrush, 1.0) : DefaultBorderPen;
        dc.DrawRoundedRectangle(bgBrush, borderPen, plotRect, 8, 8);

        // 2. Draw Subtle Grid Lines & Scale Markers
        double currentMax = _renderedMax > 0 ? _renderedMax : 32.0 * 1024.0;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;

        if (ShowGrid)
        {
            // Horizontal reference lines (25%, 50%, 75%)
            for (int step = 1; step <= 3; step++)
            {
                double gy = plotRect.Bottom - (step * 0.25 * plotHeight);
                dc.DrawLine(GridPen, new Point(plotRect.Left + 1, gy), new Point(plotRect.Right - 1, gy));

                // Scale value label on the 50% line if scale labels are enabled
                if (ShowScaleLabels && !CompactMode && step == 2)
                {
                    string scaleStr = NetworkSpeedService.FormatSpeed(currentMax * 0.5);
                    var scaleFt = new FormattedText(
                        scaleStr,
                        CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        AxisTypeface,
                        9.0,
                        AxisTextBrush,
                        dpi);
                    dc.DrawText(scaleFt, new Point(plotRect.Right - scaleFt.Width - 8, gy - scaleFt.Height - 2));
                }
            }

            // Subtle vertical division lines
            if (!CompactMode)
            {
                int vSteps = MaxSamples > 60 ? 5 : 4;
                for (int step = 1; step < vSteps; step++)
                {
                    double gx = plotRect.Left + ((double)step / vSteps * plotWidth);
                    dc.DrawLine(GridPen, new Point(gx, plotRect.Top + 1), new Point(gx, plotRect.Bottom - 1));
                }
            }
        }

        // 3. Clip and Render Data Curves
        int capacity = Math.Max(2, MaxSamples);
        var clipGeo = new RectangleGeometry(new Rect(plotRect.Left + 1, plotRect.Top + 1, plotRect.Width - 2, plotRect.Height - 2), 7, 7);
        dc.PushClip(clipGeo);

        SetHistoryWidth(plotWidth, capacity);
        dc.PushTransform(HistoryTransform);

        // Draw Download Spline (Cyan Glow)
        if (_downSeries != null && _downSeries.Length > 0)
        {
            _downDrawing.Draw(dc, _downSeries, plotRect, currentMax, capacity, PrimaryPrefix, _downFill, _downPen);
        }

        // Draw Upload Spline (Emerald Green Glow)
        if (_upSeries != null && _upSeries.Length > 0)
        {
            _upDrawing.Draw(dc, _upSeries, plotRect, currentMax, capacity, SecondaryPrefix, _upFill, _upPen);
        }

        dc.Pop(); // HistoryTransform
        dc.Pop(); // Clip

        // 3b. Render Paused Badge inside plot area if paused
        if (_isPaused)
        {
            string pauseText = "⏸ " + Strings.T("ChartPausedBadge");
            var pauseFt = new FormattedText(
                pauseText,
                CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                AxisBoldTypeface,
                9.5,
                PausedTextBrush,
                dpi);

            var badgeRect = new Rect(plotRect.Left + 10, plotRect.Top + 8, pauseFt.Width + 14, pauseFt.Height + 5);
            dc.DrawRoundedRectangle(PausedBgBrush, PausedBorderPen, badgeRect, 5, 5);
            dc.DrawText(pauseFt, new Point(badgeRect.Left + 7, badgeRect.Top + 2.5));
        }

        // 4. Render Time Axis Labels Below Graph
        if (ShowTimeLabels && !CompactMode)
        {
            double textY = plotRect.Bottom + 4;

            if (MaxSamples <= 60)
            {
                // 1-minute scale
                var ft60 = new FormattedText("-60s", CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, AxisTypeface, 9.5, AxisTextBrush, dpi);
                dc.DrawText(ft60, new Point(plotRect.Left + 4, textY));

                var ft45 = new FormattedText("-45s", CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, AxisTypeface, 9.5, AxisTextBrush, dpi);
                dc.DrawText(ft45, new Point(plotRect.Left + (plotWidth * 0.25) - (ft45.Width / 2.0), textY));

                var ft30 = new FormattedText("-30s", CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, AxisTypeface, 9.5, AxisTextBrush, dpi);
                dc.DrawText(ft30, new Point(plotRect.Left + (plotWidth * 0.50) - (ft30.Width / 2.0), textY));

                var ft15 = new FormattedText("-15s", CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, AxisTypeface, 9.5, AxisTextBrush, dpi);
                dc.DrawText(ft15, new Point(plotRect.Left + (plotWidth * 0.75) - (ft15.Width / 2.0), textY));
            }
            else
            {
                // 5-minute scale (300s): ticks at -5m, -4m, -3m, -2m, -1m, Ahora
                string[] labels = { "-5m", "-4m", "-3m", "-2m", "-1m" };
                for (int i = 0; i < 5; i++)
                {
                    double ratio = i / 5.0;
                    var ft = new FormattedText(labels[i], CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, AxisTypeface, 9.5, AxisTextBrush, dpi);
                    double posX = i == 0 ? plotRect.Left + 4 : plotRect.Left + (plotWidth * ratio) - (ft.Width / 2.0);
                    dc.DrawText(ft, new Point(posX, textY));
                }
            }

            // Ahora / Now
            string nowLabel = Strings.T("TimeNow");
            var ftNow = new FormattedText(nowLabel, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, AxisBoldTypeface, 9.5, AxisAccentBrush, dpi);
            dc.DrawText(ftNow, new Point(plotRect.Right - ftNow.Width - 4, textY));
        }

        // 5. Interactive Hover Guide & Floating Tooltip Pill
        if (_hoverPos.HasValue && !CompactMode && ShowHoverInspection && _downSeries != null && _downSeries.Length > 0)
        {
            double mx = _hoverPos.Value.X;
            if (mx >= plotRect.Left && mx <= plotRect.Right)
            {
                double stepX = plotWidth / (capacity - 1);
                int count = _downSeries.Length;
                double startX = plotRect.Left + plotWidth - (count - 1) * stepX;
                int hoverIdx = (int)Math.Round((mx - HistoryOffset - startX) / stepX);

                if (hoverIdx >= 0 && hoverIdx < count)
                {
                    double ptX = startX + hoverIdx * stepX + HistoryOffset;
                    float downVal = _downSeries[hoverIdx];
                    float upVal = _upSeries != null && hoverIdx < _upSeries.Length ? _upSeries[hoverIdx] : 0f;

                    // Draw vertical guide line
                    var guidePen = new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 1.0)
                    {
                        DashStyle = DashStyles.Dot
                    };
                    guidePen.Freeze();
                    dc.DrawLine(guidePen, new Point(ptX, plotRect.Top + 2), new Point(ptX, plotRect.Bottom - 2));

                    // Highlight dots on the curves
                    double usableH = Math.Max(1.0, plotHeight - 6.0);
                    double normDown = Math.Clamp(downVal / currentMax, 0.0, 1.0);
                    double normUp = Math.Clamp(upVal / currentMax, 0.0, 1.0);
                    double ptYDown = (plotRect.Bottom - 1.0) - (normDown * usableH);
                    double ptYUp = (plotRect.Bottom - 1.0) - (normUp * usableH);

                    // Download Dot (Cyan)
                    var downHaloBrush = new SolidColorBrush(Color.FromArgb(70, DownloadColor.R, DownloadColor.G, DownloadColor.B));
                    downHaloBrush.Freeze();
                    var downCoreBrush = new SolidColorBrush(DownloadColor);
                    downCoreBrush.Freeze();
                    dc.DrawEllipse(downHaloBrush, null, new Point(ptX, ptYDown), 5.5, 5.5);
                    dc.DrawEllipse(downCoreBrush, null, new Point(ptX, ptYDown), 3.0, 3.0);
                    dc.DrawEllipse(WhiteBrush, null, new Point(ptX, ptYDown), 1.2, 1.2);

                    // Upload Dot (Green)
                    var upHaloBrush = new SolidColorBrush(Color.FromArgb(70, UploadColor.R, UploadColor.G, UploadColor.B));
                    upHaloBrush.Freeze();
                    var upCoreBrush = new SolidColorBrush(UploadColor);
                    upCoreBrush.Freeze();
                    dc.DrawEllipse(upHaloBrush, null, new Point(ptX, ptYUp), 5.5, 5.5);
                    dc.DrawEllipse(upCoreBrush, null, new Point(ptX, ptYUp), 3.0, 3.0);
                    dc.DrawEllipse(WhiteBrush, null, new Point(ptX, ptYUp), 1.2, 1.2);

                    // Formatted Tooltip text
                    int secondsAgo = count - 1 - hoverIdx;
                    string timeText = FormatRelativeTime(secondsAgo);

                    string downSpeedStr = NetworkSpeedService.FormatSpeed(downVal);
                    string upSpeedStr = NetworkSpeedService.FormatSpeed(upVal);

                    var ftTime = new FormattedText(
                        timeText,
                        CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        AxisBoldTypeface,
                        9.5,
                        AxisTextBrush,
                        dpi);

                    var ftDown = new FormattedText(
                        $"↓ {downSpeedStr}",
                        CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        AxisBoldTypeface,
                        10.0,
                        downCoreBrush,
                        dpi);

                    var ftUp = new FormattedText(
                        $"↑ {upSpeedStr}",
                        CultureInfo.CurrentCulture,
                        System.Windows.FlowDirection.LeftToRight,
                        AxisBoldTypeface,
                        10.0,
                        upCoreBrush,
                        dpi);

                    double tipW = Math.Max(ftTime.Width + 15.0, Math.Max(ftDown.Width, ftUp.Width)) + 18.0;
                    double tipH = ftTime.Height + ftDown.Height + ftUp.Height + 14.0;

                    double tipX = ptX + 12;
                    if (tipX + tipW > plotRect.Right - 8)
                    {
                        tipX = ptX - tipW - 12;
                    }

                    double tipY = Math.Clamp(_hoverPos.Value.Y - tipH / 2.0, plotRect.Top + 6, plotRect.Bottom - tipH - 6);
                    var tipRect = new Rect(tipX, tipY, tipW, tipH);

                    dc.DrawRoundedRectangle(TooltipBgBrush, TooltipBorderPen, tipRect, 6, 6);

                    // Draw crisp vector clock icon: circle + hour hand + minute hand
                    double clockRadius = 4.0;
                    double clockLeft = tipX + 8.0;
                    double clockCenterY = tipY + 4.0 + (ftTime.Height / 2.0);
                    var clockCenter = new Point(clockLeft + clockRadius, clockCenterY);

                    dc.DrawEllipse(null, ClockPen, clockCenter, clockRadius, clockRadius);
                    dc.DrawLine(ClockPen, clockCenter, new Point(clockCenter.X, clockCenter.Y - 2.5));
                    dc.DrawLine(ClockPen, clockCenter, new Point(clockCenter.X + 2.0, clockCenter.Y));

                    // Draw time text next to clock
                    dc.DrawText(ftTime, new Point(clockLeft + (clockRadius * 2.0) + 5.0, tipY + 4.0));

                    dc.DrawText(ftDown, new Point(tipX + 8, tipY + 4 + ftTime.Height + 2));
                    dc.DrawText(ftUp, new Point(tipX + 8, tipY + 4 + ftTime.Height + ftDown.Height + 4));
                }
            }
        }
    }

    private static string FormatRelativeTime(int secondsAgo)
    {
        if (secondsAgo <= 0) return Strings.T("TimeNow");
        if (secondsAgo < 60) return string.Format(Strings.T("TimeSecondsAgo"), secondsAgo);

        int m = secondsAgo / 60;
        int s = secondsAgo % 60;
        if (s == 0) return string.Format(Strings.T("TimeMinutesAgo"), m);
        return Strings.Current == Lang.Es
            ? $"hace {m}m {s}s"
            : $"{m}m {s}s ago";
    }

    private static void OnPaintsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrafficGraphControl c)
        {
            c._cachedDownColor = null;
            c._cachedUpColor = null;
            c.InvalidateVisual();
        }
    }

    private void EnsurePaints()
    {
        if (_cachedDownColor != DownloadColor)
        {
            var col = DownloadColor;
            _cachedDownColor = col;
            _downFill = CreateGradient(col, 110, 10);
            _downPen = CreatePen(col, 1.8);
        }

        if (_cachedUpColor != UploadColor)
        {
            var col = UploadColor;
            _cachedUpColor = col;
            _upFill = CreateGradient(col, 115, 12);
            _upPen = CreatePen(col, 1.8);
        }
    }

    private static LinearGradientBrush CreateGradient(Color color, byte topAlpha, byte bottomAlpha)
    {
        var brush = new LinearGradientBrush(
            Color.FromArgb(topAlpha, color.R, color.G, color.B),
            Color.FromArgb(bottomAlpha, color.R, color.G, color.B),
            90.0);
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Color color, double thickness)
    {
        var pen = new Pen(CreateBrush(color), thickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static SolidColorBrush CreateBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
