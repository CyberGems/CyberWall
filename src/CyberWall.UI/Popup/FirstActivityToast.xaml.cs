using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.UI.Services;
using Color = System.Windows.Media.Color;

namespace CyberWall.UI.Popup;

public partial class FirstActivityToast : Window
{
    private static readonly List<FirstActivityToast> _activeToasts = new();
    private readonly DispatcherTimer _autoCloseTimer;

    public ConnectionEvent Event { get; }

    public FirstActivityToast(ConnectionEvent ev)
    {
        InitializeComponent();
        Event = ev;
        DataContext = this;

        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += (_, _) =>
        {
            var pos = App.Settings.NotificationPosition;
            var mon = App.Settings.NotificationMonitor;
            PopupWindowHelper.PositionWindow(this, pos, mon);
        };

        TitleLbl.Text = Strings.T("FirstNetworkActivity");
        TimeLbl.Text = Strings.T("Now");
        NewBadgeLbl.Text = Strings.T("NewBadge");
        DescLbl.Text = Strings.T("FirstNetworkActivityDesc", ev.DisplayName);
        CloseBtn.ToolTip = Strings.T("Close");
        AutomationProperties.SetName(CloseBtn, Strings.T("Close"));

        if (ev.Direction == Direction.Inbound)
        {
            var fg = new SolidColorBrush(Color.FromRgb(192, 132, 252));
            DirectionPill.Background = new SolidColorBrush(Color.FromArgb(45, 168, 85, 247));
            DirectionPill.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 168, 85, 247));
            DirectionPill.BorderThickness = new Thickness(1);
            DirectionPillText.Foreground = fg;
            DirectionPillText.Text = Strings.T("Inbound");
            DirectionArrow.Stroke = fg;
            DirectionArrow.Fill = System.Windows.Media.Brushes.Transparent;
            DirectionArrow.Data = Geometry.Parse("M 5 1.5 L 5 11 M 5 11 L 1.6 6.7 M 5 11 L 8.4 6.7");
        }
        else
        {
            var fg = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            DirectionPill.Background = new SolidColorBrush(Color.FromArgb(40, 0, 229, 255));
            DirectionPill.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 229, 255));
            DirectionPill.BorderThickness = new Thickness(1);
            DirectionPillText.Foreground = fg;
            DirectionPillText.Text = Strings.T("Outbound");
            DirectionArrow.Stroke = fg;
            DirectionArrow.Fill = System.Windows.Media.Brushes.Transparent;
            DirectionArrow.Data = Geometry.Parse("M 5 11 L 5 1.5 M 5 1.5 L 1.6 5.8 M 5 1.5 L 8.4 5.8");
        }

        EndpointLbl.Text = NetworkEndpoint.FormatPrimary(ev.Protocol, ev.RemoteAddress, ev.RemotePort);

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5.5)
        };
        _autoCloseTimer.Tick += (_, _) =>
        {
            _autoCloseTimer.Stop();
            CloseToast();
        };
        _autoCloseTimer.Start();

        MouseLeftButtonDown += (_, _) =>
        {
            _autoCloseTimer.Stop();
            CloseToast();
            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
            {
                if (mw.WindowState == WindowState.Minimized) mw.WindowState = WindowState.Normal;
                mw.Show();
                mw.Activate();
            }
        };
    }

    public static void ShowToast(ConnectionEvent ev)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var toast = new FirstActivityToast(ev);
            _activeToasts.Add(toast);
            toast.Show();
        });
    }

    private void Close_Click(object s, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        CloseToast();
    }

    private void CloseToast()
    {
        _activeToasts.Remove(this);
        try { Close(); } catch { }
    }
}
