using System.Net.Http;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using MediaColor = System.Windows.Media.Color;

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
    }

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private readonly DispatcherTimer _connectivityTimer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly List<Packet> _packets = new();
    private CancellationTokenSource? _connectivityCts;
    private DateTime _lastTickUtc;
    private bool _isActive;
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

    public void SetActive(bool active)
    {
        _isActive = active;
        RefreshAppearance();
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        LiveText.Text = Strings.T(_connectivity == ConnectivityState.Offline ? "TrafficOffline" : "TrafficLive");
        var stateKey = _connectivity switch
        {
            ConnectivityState.Offline => "TrafficOffline",
            ConnectivityState.Unknown => "TrafficChecking",
            _ => _isActive ? "TrafficProtected" : "TrafficUnfiltered"
        };
        StateText.Text = Strings.T(stateKey);
        ToolTip = Strings.T(stateKey);
    }

    private void CreatePackets()
    {
        var random = new Random(9173);
        for (var i = 0; i < 6; i++)
        {
            var packet = new Border
            {
                Width = 3 + random.Next(0, 4),
                Height = i % 2 == 0 ? 3 : 2,
                CornerRadius = new CornerRadius(2),
                Opacity = 0.55 + random.NextDouble() * 0.4,
                IsHitTestVisible = false
            };
            var item = new Packet
            {
                Element = packet,
                Position = -8 - random.NextDouble() * 180,
                Speed = 34 + random.NextDouble() * 32,
                Top = 6 + random.NextDouble() * 8,
                Phase = random.NextDouble() * Math.PI * 2
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

        foreach (var packet in _packets)
        {
            var flowFactor = _connectivity switch
            {
                ConnectivityState.Offline => 0,
                ConnectivityState.Unknown => 0.7,
                _ => _isActive ? 1 : 0.55
            };
            packet.Position += packet.Speed * elapsed * flowFactor;
            if (packet.Position > width + 8)
                packet.Position = -8 - packet.Speed * 0.15;

            Canvas.SetLeft(packet.Element, packet.Position);
            Canvas.SetTop(packet.Element, packet.Top);
            packet.Element.Opacity = (0.45 + (Math.Sin(now.TimeOfDay.TotalSeconds * 3 + packet.Phase) + 1) * 0.2)
                * (_connectivity == ConnectivityState.Offline ? 0.25 : _isActive ? 1 : 0.7);
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
        var key = _connectivity == ConnectivityState.Offline
            ? "BadgeBlockFgBrush"
            : _isActive ? "AccentBrush" : "BadgeWarnFgBrush";
        if (FindResource(key) is not SolidColorBrush baseBrush)
            return;

        var color = baseBrush.Color;
        StatusDot.Fill = baseBrush;
        StateText.Foreground = baseBrush;
        RootBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(100, color.R, color.G, color.B));
        foreach (var packet in _packets)
            packet.Element.Background = new SolidColorBrush(MediaColor.FromArgb(210, color.R, color.G, color.B));
    }
}
