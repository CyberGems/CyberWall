using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.UI.Services;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.Brush;
using WPoint = System.Windows.Point;

namespace CyberWall.UI.Controls;

public partial class NetworkTrafficIndicator : System.Windows.Controls.UserControl
{
    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _lastRenderingTime;
    private bool _isActive;
    private FirewallMode _mode = FirewallMode.Ask;
    private ConnectivityState _connectivity = ConnectivityState.Unknown;
    private NetworkSpeedSnapshot? _latestSnapshot;

    private double _smoothedRatio = 0.0;
    private double _wavePhase = 0.0;
    private const int WaveSegments = 48;

    private enum ConnectivityState
    {
        Unknown,
        Online,
        Offline
    }

    public NetworkTrafficIndicator()
    {
        InitializeComponent();
        RefreshLanguage();
        Loaded += (_, _) =>
        {
            _stopwatch.Restart();
            _lastRenderingTime = _stopwatch.Elapsed;
            CompositionTarget.Rendering += OnRendering;

            ConnectivityService.Instance.ConnectivityChanged += OnConnectivityChanged;
            SetConnectivity(ConnectivityService.Instance.IsOnline ? ConnectivityState.Online : ConnectivityState.Offline);

            NetworkSpeedService.Instance.SpeedUpdated += OnSpeedUpdated;
            NetworkSpeedService.Instance.Start();
            OnSpeedUpdated(NetworkSpeedService.Instance.CurrentSnapshot);
        };
        Unloaded += (_, _) =>
        {
            CompositionTarget.Rendering -= OnRendering;
            _stopwatch.Stop();

            ConnectivityService.Instance.ConnectivityChanged -= OnConnectivityChanged;

            NetworkSpeedService.Instance.SpeedUpdated -= OnSpeedUpdated;
            NetworkSpeedService.Instance.Stop();
        };
    }

    private void OnConnectivityChanged(bool online)
    {
        SetConnectivity(online ? ConnectivityState.Online : ConnectivityState.Offline);
    }

    private void OnSpeedUpdated(NetworkSpeedSnapshot snapshot)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => OnSpeedUpdated(snapshot));
            return;
        }

        _latestSnapshot = snapshot;

        if (_connectivity == ConnectivityState.Offline || _mode == FirewallMode.Killswitch)
        {
            DownloadSpeedText.Text = "0.0 B/s";
            UploadSpeedText.Text = "0.0 B/s";
            TooltipDownVal.Text = "0.0 B/s";
            TooltipUpVal.Text = "0.0 B/s";
        }
        else
        {
            DownloadSpeedText.Text = NetworkSpeedService.FormatSpeed(snapshot.DownloadBps);
            UploadSpeedText.Text = NetworkSpeedService.FormatSpeed(snapshot.UploadBps);
            TooltipDownVal.Text = NetworkSpeedService.FormatSpeed(snapshot.DownloadBps);
            TooltipUpVal.Text = NetworkSpeedService.FormatSpeed(snapshot.UploadBps);
        }

        TooltipAdapterVal.Text = snapshot.AdapterName;
        TooltipTotalDownVal.Text = NetworkSpeedService.FormatBytes(snapshot.TotalBytesReceived);
        TooltipTotalUpVal.Text = NetworkSpeedService.FormatBytes(snapshot.TotalBytesSent);
    }

    public void SetActive(bool active, FirewallMode mode = FirewallMode.Ask)
    {
        _isActive = active;
        _mode = mode;
        RefreshAppearance();
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        var isOfflineOrKillswitch = _connectivity == ConnectivityState.Offline || _mode == FirewallMode.Killswitch;
        LiveText.Text = Strings.T(isOfflineOrKillswitch ? "TrafficOffline" : "TrafficLive");
        var stateKey = _connectivity switch
        {
            ConnectivityState.Offline => "TrafficDisconnected",
            ConnectivityState.Unknown => "TrafficChecking",
            _ => !_isActive ? "TrafficUnfiltered" : (_mode switch
            {
                FirewallMode.Killswitch => "TrafficKillswitch",
                FirewallMode.BlockAll => "TrafficStrict",
                _ => "TrafficProtected"
            })
        };
        StateText.Text = Strings.T(stateKey);

        // Tooltip localization
        TooltipHeaderLbl.Text = Strings.T("TrafficTelemetryTitle");
        TooltipAdapterLbl.Text = Strings.T("TrafficActiveAdapter");
        TooltipDownLbl.Text = Strings.T("TrafficDownload");
        TooltipUpLbl.Text = Strings.T("TrafficUpload");
        TooltipTotalDownLbl.Text = Strings.T("TrafficSessionIn");
        TooltipTotalUpLbl.Text = Strings.T("TrafficSessionOut");
        TooltipFilterLbl.Text = Strings.T("TrafficFilterStatus");
        TooltipFilterVal.Text = Strings.T(stateKey);
        TooltipClickHint.Text = Strings.T("BandwidthClickPrompt");
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var current = _stopwatch.Elapsed;
        var elapsed = (current - _lastRenderingTime).TotalSeconds;
        _lastRenderingTime = current;
        if (elapsed <= 0.0) return;
        if (elapsed > 0.1) elapsed = 0.033;

        var animMode = App.Settings?.TrafficAnimation ?? Common.Settings.TrafficAnimationMode.FluidStream;

        var nowSeconds = current.TotalSeconds;
        if (_connectivity == ConnectivityState.Offline || _mode == FirewallMode.Killswitch)
        {
            StatusHalo.Opacity = 0.08;
        }
        else
        {
            var haloPulse = 0.16 + (Math.Sin(nowSeconds * 2.8) + 1.0) * 0.10;
            StatusHalo.Opacity = haloPulse;
        }

        if (animMode == Common.Settings.TrafficAnimationMode.Disabled || animMode == Common.Settings.TrafficAnimationMode.PulseGlow)
        {
            if (TrafficWaveCanvas.Visibility != Visibility.Collapsed)
                TrafficWaveCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        if (TrafficWaveCanvas.Visibility != Visibility.Visible)
            TrafficWaveCanvas.Visibility = Visibility.Visible;

        var width = TrafficWaveCanvas.ActualWidth;
        var height = TrafficWaveCanvas.ActualHeight;
        if (width <= 10 || height <= 6) return;

        var flowFactor = _connectivity switch
        {
            ConnectivityState.Offline => 0.0,
            ConnectivityState.Unknown => 0.6,
            _ => !_isActive ? 0.45 : (_mode switch
            {
                FirewallMode.Killswitch => 0.0,
                FirewallMode.BlockAll => 1.15,
                _ => 1.0
            })
        };

        double currentThroughput = 0;
        if (_latestSnapshot != null && _latestSnapshot.IsConnected && flowFactor > 0)
        {
            currentThroughput = _latestSnapshot.DownloadBps + _latestSnapshot.UploadBps;
        }

        // Perceptual logarithmic / power scaling: visible even at 10 KB/s, scaled up at 5 MB/s
        double targetRatio = currentThroughput > 100 ? Math.Min(1.0, Math.Pow(currentThroughput / 4_000_000.0, 0.38)) : 0.0;

        // Smooth acceleration / deceleration without freeze
        _smoothedRatio += (targetRatio - _smoothedRatio) * Math.Min(1.0, elapsed * 3.0);

        // Advance wave phase continuously (2.5 rad/s idle to 8.5 rad/s max)
        double phaseSpeed = (2.5 + _smoothedRatio * 6.0) * flowFactor;
        _wavePhase += elapsed * phaseSpeed;

        // Generate wave points
        var points = new WPoint[WaveSegments + 1];
        double stepX = width / WaveSegments;

        if (flowFactor <= 0.0)
        {
            // Flat baseline when offline or killswitch
            for (int i = 0; i <= WaveSegments; i++)
            {
                points[i] = new WPoint(i * stepX, height - 2);
            }
        }
        else
        {
            // Alive wave: subtle idle breathing (1.5 - 2.5px) + dynamic throughput activity
            double idleBreathing = (1.5 + Math.Sin(nowSeconds * 2.2) * 0.6);
            double activeAmp = _smoothedRatio * (height - 5);
            double totalAmp = Math.Min(height - 3, idleBreathing + activeAmp);

            for (int i = 0; i <= WaveSegments; i++)
            {
                double x = i * stepX;
                // Spatial frequency across width
                double spatial = (x / width) * 3.5 * Math.PI;
                // Double harmonic sine wave for organic oscilloscope pulse
                double wave = Math.Sin(spatial - _wavePhase) * 0.68 + Math.Sin(spatial * 2.2 - _wavePhase * 1.5) * 0.32;
                // Invert Y coordinate so peaks rise from bottom baseline
                double y = (height - 2) - Math.Clamp(totalAmp * (0.5 + wave * 0.5), 1.0, height - 2);
                points[i] = new WPoint(x, y);
            }
        }

        // Render Stroke Line
        var lineGeom = new StreamGeometry();
        using (var ctx = lineGeom.Open())
        {
            ctx.BeginFigure(points[0], false, false);
            for (int i = 1; i <= WaveSegments; i++)
            {
                ctx.LineTo(points[i], true, true);
            }
        }
        lineGeom.Freeze();
        WaveLinePath.Data = lineGeom;

        // Render Area Fill
        var areaGeom = new StreamGeometry();
        using (var ctx = areaGeom.Open())
        {
            ctx.BeginFigure(new WPoint(0, height), true, true);
            ctx.LineTo(points[0], true, false);
            for (int i = 1; i <= WaveSegments; i++)
            {
                ctx.LineTo(points[i], true, true);
            }
            ctx.LineTo(new WPoint(width, height), true, false);
        }
        areaGeom.Freeze();
        WaveAreaPath.Data = areaGeom;
    }

    private void SetConnectivity(ConnectivityState state)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(() => SetConnectivity(state));
            return;
        }

        if (_connectivity == state) return;
        _connectivity = state;
        RefreshAppearance();
        RefreshLanguage();
    }

    private void RefreshAppearance()
    {
        string fgKey;
        string bgKey;

        if (_connectivity == ConnectivityState.Offline || _mode == FirewallMode.Killswitch)
        {
            fgKey = "BadgeBlockFgBrush";
            bgKey = "BadgeBlockBgBrush";
        }
        else if (!_isActive)
        {
            fgKey = "BadgeWarnFgBrush";
            bgKey = "BadgeWarnBgBrush";
        }
        else if (_mode == FirewallMode.BlockAll)
        {
            fgKey = "AccentBrush";
            bgKey = "BadgeAllowBgBrush";
        }
        else
        {
            fgKey = "BadgeAllowFgBrush";
            bgKey = "BadgeAllowBgBrush";
        }

        if (FindResource(fgKey) is not SolidColorBrush fgBrush)
            return;

        var fgColor = fgBrush.Color;
        var bgBrush = (FindResource(bgKey) is MediaBrush b) ? b : new SolidColorBrush(MediaColor.FromArgb(40, fgColor.R, fgColor.G, fgColor.B));

        StatusDot.Fill = fgBrush;
        StatusHalo.Fill = fgBrush;
        StateText.Foreground = fgBrush;
        StatePill.Background = bgBrush;
        StatePill.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(_mode == FirewallMode.BlockAll ? (byte)120 : (byte)70, fgColor.R, fgColor.G, fgColor.B));
        RootBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(_mode == FirewallMode.BlockAll ? (byte)80 : (byte)45, fgColor.R, fgColor.G, fgColor.B));

        if (TooltipFilterVal != null)
        {
            TooltipFilterVal.Foreground = fgBrush;
        }

        WaveLinePath.Stroke = fgBrush;
        WaveGradientStop1.Color = MediaColor.FromArgb(60, fgColor.R, fgColor.G, fgColor.B);
        WaveGradientStop2.Color = MediaColor.FromArgb(0, fgColor.R, fgColor.G, fgColor.B);
    }

    public event Action? IndicatorClicked;

    private void RootBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IndicatorClicked != null)
        {
            IndicatorClicked.Invoke();
            return;
        }

        try
        {
            var win = Window.GetWindow(this);
            var dlg = new Dialogs.TrafficHistoryDialog { Owner = win };
            dlg.ShowDialog();
        }
        catch { }
    }
}
