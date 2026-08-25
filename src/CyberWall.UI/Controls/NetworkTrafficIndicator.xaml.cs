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
    private readonly List<Packet> _packets = new();
    private DateTime _lastTickUtc;
    private bool _isActive;

    public NetworkTrafficIndicator()
    {
        InitializeComponent();
        CreatePackets();
        RefreshLanguage();
        _timer.Tick += OnTick;
        Loaded += (_, _) =>
        {
            _lastTickUtc = DateTime.UtcNow;
            _timer.Start();
            RefreshPackets();
        };
        Unloaded += (_, _) => _timer.Stop();
        SizeChanged += (_, _) => RefreshPackets();
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        RefreshAppearance();
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        LiveText.Text = Strings.T("TrafficLive");
        StateText.Text = Strings.T(_isActive ? "TrafficProtected" : "TrafficUnfiltered");
        ToolTip = Strings.T(_isActive ? "TrafficProtected" : "TrafficUnfiltered");
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
            packet.Position += packet.Speed * elapsed * (_isActive ? 1 : 0.55);
            if (packet.Position > width + 8)
                packet.Position = -8 - packet.Speed * 0.15;

            Canvas.SetLeft(packet.Element, packet.Position);
            Canvas.SetTop(packet.Element, packet.Top);
            packet.Element.Opacity = (0.45 + (Math.Sin(now.TimeOfDay.TotalSeconds * 3 + packet.Phase) + 1) * 0.2)
                * (_isActive ? 1 : 0.7);
        }
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
        var key = _isActive ? "AccentBrush" : "BadgeWarnFgBrush";
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
