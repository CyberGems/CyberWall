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
        public double Speed { get; init; }
        public double Top { get; init; }
        public double Phase { get; init; }
        public double Length { get; init; }
    }

    private readonly Stopwatch _stopwatch = new();
    private TimeSpan _lastRenderingTime;
    private readonly List<Packet> _packets = new();
    private bool _isActive;
    private FirewallMode _mode = FirewallMode.Ask;
    private ConnectivityState _connectivity = ConnectivityState.Unknown;
    private NetworkSpeedSnapshot? _latestSnapshot;

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

        for (var i = 0; i < 7; i++)
        {
            var length = 8.0 + random.Next(0, 12);
            var height = (i % 3 == 0) ? 2.2 : 1.8;
            var packet = new Border
            {
                Width = length,
                Height = height,
                CornerRadius = new CornerRadius(height / 2.0),
                IsHitTestVisible = false
            };
            var item = new Packet
            {
                Element = packet,
                Position = -20 - random.NextDouble() * 160,
                Speed = 38 + random.NextDouble() * 40,
                Top = 4.0 + random.NextDouble() * 6.5,
                Phase = random.NextDouble() * Math.PI * 2,
                Length = length
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

        var width = TrafficCanvas.ActualWidth;
        if (width <= 0) return;

        var flowFactor = _connectivity switch
        {
            ConnectivityState.Offline => 0.0,
            ConnectivityState.Unknown => 0.6,
            _ => !_isActive ? 0.45 : (_mode switch
            {
                FirewallMode.Killswitch => 0.0,
                FirewallMode.BlockAll => 1.25,
                _ => 1.0
            })
        };

        // Real throughput speed modulation (TrafficMonitor inspired dynamic particle velocity)
        double speedMod = 0.85;
        if (_latestSnapshot != null && _latestSnapshot.IsConnected && (_latestSnapshot.DownloadBps + _latestSnapshot.UploadBps > 1024))
        {
            var totalThroughput = _latestSnapshot.DownloadBps + _latestSnapshot.UploadBps;
            // Modulate between 0.9x and 2.6x based on log throughput
            speedMod = Math.Min(2.6, 0.85 + Math.Log10(totalThroughput / 512.0) * 0.45);
        }

        var nowSeconds = current.TotalSeconds;
        if (_connectivity == ConnectivityState.Offline || _mode == FirewallMode.Killswitch)
        {
            StatusHalo.Opacity = 0.08;
        }
        else
        {
            var haloPulse = 0.16 + (Math.Sin(nowSeconds * 3.2) + 1.0) * 0.12;
            StatusHalo.Opacity = haloPulse;
        }

        foreach (var packet in _packets)
        {
            if (flowFactor > 0)
            {
                packet.Position += packet.Speed * elapsed * flowFactor * speedMod;
                if (packet.Position > width + packet.Length + 4)
                    packet.Position = -packet.Length - (packet.Speed * 0.15);

                Canvas.SetLeft(packet.Element, packet.Position);
                Canvas.SetTop(packet.Element, packet.Top);
            }

            var pulse = 0.65 + (Math.Sin(nowSeconds * 3.5 + packet.Phase) + 1.0) * 0.175;
            packet.Element.Opacity = _connectivity switch
            {
                ConnectivityState.Offline => 0.08,
                ConnectivityState.Unknown => pulse * 0.5,
                _ => _mode == FirewallMode.Killswitch ? 0.08 : (!_isActive ? pulse * 0.5 : pulse)
            };
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
