using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
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

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _connectivityTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly List<Packet> _packets = new();
    private CancellationTokenSource? _connectivityCts;
    private DateTime _lastTickUtc;
    private bool _isActive;
    private FirewallMode _mode = FirewallMode.Ask;
    private ConnectivityState _connectivity = ConnectivityState.Unknown;

    private static readonly HttpClient ConnectivityClient = new()
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

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
        _timer.Tick += OnTick;
        _connectivityTimer.Tick += (_, _) => _ = CheckConnectivityAsync();
        Loaded += (_, _) =>
        {
            _lastTickUtc = DateTime.UtcNow;
            _timer.Start();
            _connectivityTimer.Start();
            _ = CheckConnectivityAsync();
            RefreshPackets();
        };
        Unloaded += (_, _) =>
        {
            _timer.Stop();
            _connectivityTimer.Stop();
            _connectivityCts?.Cancel();
            _connectivityCts = null;
        };
        SizeChanged += (_, _) => RefreshPackets();
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
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
        LiveText.Text = Strings.T(_connectivity == ConnectivityState.Offline ? "TrafficOffline" : "TrafficLive");
        var stateKey = _connectivity switch
        {
            ConnectivityState.Offline => "TrafficDisconnected",
            ConnectivityState.Unknown => "TrafficChecking",
            _ => !_isActive ? "TrafficUnfiltered" : (_mode == FirewallMode.BlockAll ? "TrafficStrict" : "TrafficProtected")
        };
        StateText.Text = Strings.T(stateKey);
        ToolTip = Strings.T(stateKey);
    }

    private void CreatePackets()
    {
        TrafficCanvas.Children.Clear();
        _packets.Clear();
        var random = new Random(9173);

        for (var i = 0; i < 8; i++)
        {
            var length = 10.0 + random.Next(0, 15);
            var height = (i % 3 == 0) ? 2.5 : 2.0;
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
                Position = -20 - random.NextDouble() * 200,
                Speed = 42 + random.NextDouble() * 45,
                Top = 4.0 + random.NextDouble() * 6.5,
                Phase = random.NextDouble() * Math.PI * 2,
                Length = length
            };
            _packets.Add(item);
            TrafficCanvas.Children.Add(packet);
        }
        RefreshAppearance();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsed = Math.Clamp((now - _lastTickUtc).TotalSeconds, 0, 0.2);
        _lastTickUtc = now;
        var width = TrafficCanvas.ActualWidth;
        if (width <= 0) return;

        var flowFactor = _connectivity switch
        {
            ConnectivityState.Offline => 0.0,
            ConnectivityState.Unknown => 0.6,
            _ => !_isActive ? 0.45 : (_mode == FirewallMode.BlockAll ? 1.25 : 1.0)
        };

        if (_connectivity == ConnectivityState.Offline)
        {
            StatusHalo.Opacity = 0.08;
        }
        else
        {
            var haloPulse = 0.16 + (Math.Sin(now.TimeOfDay.TotalSeconds * 3.2) + 1.0) * 0.12;
            StatusHalo.Opacity = haloPulse;
        }

        foreach (var packet in _packets)
        {
            packet.Position += packet.Speed * elapsed * flowFactor;
            if (packet.Position > width + packet.Length + 4)
                packet.Position = -packet.Length - (packet.Speed * 0.15);

            Canvas.SetLeft(packet.Element, packet.Position);
            Canvas.SetTop(packet.Element, packet.Top);

            var pulse = 0.65 + (Math.Sin(now.TimeOfDay.TotalSeconds * 3.5 + packet.Phase) + 1.0) * 0.175;
            packet.Element.Opacity = _connectivity switch
            {
                ConnectivityState.Offline => 0.08,
                ConnectivityState.Unknown => pulse * 0.5,
                _ => _isActive ? pulse : pulse * 0.7
            };
        }
    }

    private void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded) return;
            if (!e.IsAvailable)
                SetConnectivity(ConnectivityState.Offline);
            else
                _ = CheckConnectivityAsync();
        });
    }

    private async Task CheckConnectivityAsync()
    {
        if (!IsLoaded || Interlocked.CompareExchange(ref _connectivityCheck, 1, 0) != 0)
            return;

        _connectivityCts?.Cancel();
        _connectivityCts?.Dispose();
        _connectivityCts = new CancellationTokenSource();
        var token = _connectivityCts.Token;
        try
        {
            if (!NetworkInterface.GetIsNetworkAvailable() || !HasDefaultRoute())
            {
                SetConnectivity(ConnectivityState.Offline);
                return;
            }

            using var response = await ConnectivityClient.GetAsync(
                "http://www.msftconnecttest.com/connecttest.txt",
                HttpCompletionOption.ResponseHeadersRead,
                token).ConfigureAwait(true);
            SetConnectivity(response.IsSuccessStatusCode
                ? ConnectivityState.Online
                : ConnectivityState.Offline);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch
        {
            SetConnectivity(ConnectivityState.Offline);
        }
        finally
        {
            Interlocked.Exchange(ref _connectivityCheck, 0);
        }
    }

    private static bool HasDefaultRoute()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces().Any(nic =>
                nic.OperationalStatus == OperationalStatus.Up &&
                nic.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                nic.GetIPProperties().GatewayAddresses.Count > 0);
        }
        catch
        {
            return false;
        }
    }

    private int _connectivityCheck;

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

        if (_connectivity == ConnectivityState.Offline)
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
