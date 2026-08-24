using System.Windows;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.UI.Services;

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
        TimeLbl.Text = DateTime.Now.ToString("d MMM, HH:mm");
        DescLbl.Text = Strings.T("FirstNetworkActivityDesc", ev.DisplayName);

        if (ev.Direction == Direction.Inbound)
        {
            DirectionPill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 168, 85, 247));
            DirectionPill.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 168, 85, 247));
            DirectionPill.BorderThickness = new Thickness(1);
            DirectionPillText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(192, 132, 252));
            DirectionPillText.Text = "↓ " + Strings.T("Inbound");
        }
        else
        {
            DirectionPill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 0, 229, 255));
            DirectionPill.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(80, 0, 229, 255));
            DirectionPill.BorderThickness = new Thickness(1);
            DirectionPillText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 229, 255));
            DirectionPillText.Text = "↑ " + Strings.T("Outbound");
        }

        EndpointLbl.Text = $"{ev.Protocol} • {ev.RemoteAddress}:{ev.RemotePort}";

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
