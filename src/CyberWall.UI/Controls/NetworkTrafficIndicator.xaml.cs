using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
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
    private sealed class Packet
    {
        public required Border Element { get; init; }
        public double Position { get; set; }
        public double BaseSpeed { get; init; }
        public double Top { get; init; }
        public double BaseLength { get; init; }
        public double CurrentOpacity { get; set; }
    }

    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _lastRenderingTime;
    private readonly List<Packet> _packets = new();
    private bool _isActive;
    private FirewallMode _mode = FirewallMode.Ask;
    private ConnectivityState _connectivity = ConnectivityState.Unknown;
    private NetworkSpeedSnapshot? _latestSnapshot;

    private double _smoothedRatio = 0.0;
    private double _observedPeakBps = 2_000_000.0; // Starts with 2 MB/s minimum baseline
    private DateTime _lastPeakDecay = DateTime.UtcNow;

    private enum ConnectivityState
    {
        Unknown,
        Online,
        Offline
    }

    public NetworkTrafficIndicator()
    {
        InitializeComponent();
        CreatePackets();
        RefreshLanguage();
        Loaded += (_, _) =>
        {
            _stopwatch.Restart();
            _lastRenderingTime = _stopwatch.Elapsed;
            CompositionTarget.Rendering += OnRendering;
            RefreshPackets();

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
        SizeChanged += (_, _) => RefreshPackets();
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
    }

    private void CreatePackets()
    {
        TrafficCanvas.Children.Clear();
        _packets.Clear();
        var random = new Random(9173);

        // Pool of 12 scalable packets for dynamic density
        for (var i = 0; i < 12; i++)
        {
            var length = 8.0 + (i % 4) * 3.5;
            var height = 2.0;
            var packet = new Border
            {
                Width = length,
                Height = height,
                CornerRadius = new CornerRadius(1.0),
                IsHitTestVisible = false,
                Opacity = 0
            };
            var item = new Packet
            {
                Element = packet,
                Position = -20 - (i * 22.0) - random.NextDouble() * 8,
                BaseSpeed = 36.0 + (i % 3) * 6.0,
                Top = 4.5 + (i % 3) * 2.4,
                BaseLength = length,
                CurrentOpacity = 0
            };
            _packets.Add(item);
            TrafficCanvas.Children.Add(packet);
        }
        RefreshAppearance();
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
            if (TrafficCanvas.Visibility != Visibility.Collapsed)
                TrafficCanvas.Visibility = Visibility.Collapsed;
            return;
        }

        if (TrafficCanvas.Visibility != Visibility.Visible)
            TrafficCanvas.Visibility = Visibility.Visible;

        var width = TrafficCanvas.ActualWidth;
        if (width <= 0) return;

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

        // Track live throughput and adapt peak bandwidth
        double currentThroughput = 0;
        if (_latestSnapshot != null && _latestSnapshot.IsConnected)
        {
            currentThroughput = _latestSnapshot.DownloadBps + _latestSnapshot.UploadBps;
        }

        if (currentThroughput > _observedPeakBps)
        {
            _observedPeakBps = currentThroughput;
        }

        // Slow decay of peak to allow recalibration over time
        var now = DateTime.UtcNow;
        if ((now - _lastPeakDecay).TotalSeconds > 10)
        {
            _lastPeakDecay = now;
            _observedPeakBps = Math.Max(2_000_000.0, _observedPeakBps * 0.995);
        }

        // Calculate instantaneous utilization ratio [0.0 = idle, 1.0 = 100% capacity]
        double rawRatio = currentThroughput > 1024 ? Math.Clamp(currentThroughput / _observedPeakBps, 0.0, 1.0) : 0.0;

        // Exponential smoothing (smooth acceleration/deceleration)
        _smoothedRatio += (rawRatio - _smoothedRatio) * Math.Min(1.0, elapsed * 2.0);

        // Dynamic density: 2 packets at idle -> up to 12 packets at max capacity
        int activeCount = flowFactor > 0 ? Math.Clamp(2 + (int)Math.Round(_smoothedRatio * 10.0), 2, _packets.Count) : 0;

        // Dynamic velocity multiplier: 0.65x at idle -> up to 2.5x at max capacity
        double speedMultiplier = 0.65 + _smoothedRatio * 1.85;

        for (var i = 0; i < _packets.Count; i++)
        {
            var packet = _packets[i];
            var isActive = i < activeCount && flowFactor > 0;

            if (isActive)
            {
                // Dynamic packet stretch at high speeds
                var dynamicLength = packet.BaseLength * (1.0 + _smoothedRatio * 0.6);
                packet.Element.Width = dynamicLength;

                packet.Position += packet.BaseSpeed * elapsed * flowFactor * speedMultiplier;
                if (packet.Position > width + dynamicLength + 4)
                    packet.Position = -dynamicLength - 10;

                Canvas.SetLeft(packet.Element, packet.Position);
                Canvas.SetTop(packet.Element, packet.Top);

                // Smooth edge fade
                var progress = Math.Clamp((packet.Position + dynamicLength) / (width + dynamicLength * 2), 0.0, 1.0);
                var edgeFade = Math.Sin(progress * Math.PI);
                var targetOpacity = (0.25 + _smoothedRatio * 0.65) * edgeFade;

                packet.CurrentOpacity += (targetOpacity - packet.CurrentOpacity) * Math.Min(1.0, elapsed * 4.0);
            }
            else
            {
                // Inactive packet fades out smoothly
                packet.CurrentOpacity += (0.0 - packet.CurrentOpacity) * Math.Min(1.0, elapsed * 4.0);
            }

            packet.Element.Opacity = Math.Max(0.0, packet.CurrentOpacity);
        }
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

    private void RefreshPackets()
    {
        foreach (var packet in _packets)
        {
            Canvas.SetLeft(packet.Element, packet.Position);
            Canvas.SetTop(packet.Element, packet.Top);
        }
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

        foreach (var packet in _packets)
        {
            var trailBrush = new LinearGradientBrush
            {
                StartPoint = new WPoint(0, 0.5),
                EndPoint = new WPoint(1, 0.5)
            };
            trailBrush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(20, fgColor.R, fgColor.G, fgColor.B), 0.0));
            trailBrush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(140, fgColor.R, fgColor.G, fgColor.B), 0.65));
            trailBrush.GradientStops.Add(new GradientStop(MediaColor.FromArgb(250, fgColor.R, fgColor.G, fgColor.B), 1.0));
            trailBrush.Freeze();

            packet.Element.Background = trailBrush;
        }
    }
}
