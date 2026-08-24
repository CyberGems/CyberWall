using System.Windows;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.UI.Popup;

public partial class ConnectionPopup : Window
{
    private static ConnectionPopup? _activePreview;
    private static DispatcherTimer? _previewTimer;

    public ConnectionEvent Event { get; }
    public Verdict ResultVerdict { get; private set; } = Verdict.Block;
    public bool Remember => RememberChk.IsChecked == true;
    public bool IsPreview { get; }
    public event Action<ConnectionPopup>? ClosedWithVerdict;

    public ConnectionPopup(ConnectionEvent ev, bool isPreview = false)
    {
        InitializeComponent();
        Event = ev;
        IsPreview = isPreview;
        DataContext = this;

        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += (_, _) =>
        {
            var pos = App.Settings.NotificationPosition;
            var mon = App.Settings.NotificationMonitor;
            PopupWindowHelper.PositionPopup(this, pos, mon, isPreview ? 0 : null);
        };

        TitleLbl.Text = isPreview
            ? (Strings.Current == Lang.Es ? "Vista Previa de Alerta" : "Alert Preview")
            : Strings.T("NewConnection");
        AppLbl.Text = Strings.T("AppWantsToConnect", ev.DisplayName);
        BlockBtn.Content = Strings.T("Block");
        AllowBtn.Content = Strings.T("Allow");
        RememberChk.Content = Strings.T("Remember");
        if (isPreview)
        {
            RememberChk.Visibility = Visibility.Collapsed;
        }

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

        DetailLbl.Text = $"{ev.Protocol} • {ev.RemoteAddress}:{ev.RemotePort} • PID {ev.ProcessId}";
        PathLbl.Text = ev.AppPath;
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    public static void ShowPreview(PopupPosition position, int monitorIndex)
    {
        DismissPreview();

        var ev = new ConnectionEvent
        {
            AppPath = @"C:\Program Files\Demo\DemoApp.exe",
            RemoteAddress = "142.250.190.46",
            RemotePort = 443,
            Protocol = "TCP",
            Direction = Direction.Outbound,
            ProcessId = 1337
        };

        var popup = new ConnectionPopup(ev, isPreview: true);
        _activePreview = popup;

        popup.Loaded += (_, _) =>
        {
            PopupWindowHelper.PositionPopup(popup, position, monitorIndex, explicitStackIndex: 0);
        };

        popup.Show();

        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3.5)
        };
        _previewTimer.Tick += (_, _) =>
        {
            DismissPreview();
        };
        _previewTimer.Start();
    }

    public static void DismissPreview()
    {
        if (_previewTimer != null)
        {
            _previewTimer.Stop();
            _previewTimer = null;
        }
        if (_activePreview != null)
        {
            try
            {
                _activePreview.Close();
            }
            catch { }
            _activePreview = null;
        }
    }

    private void Allow_Click(object s, RoutedEventArgs e)
    {
        ResultVerdict = Verdict.Allow;
        if (IsPreview) { DismissPreview(); return; }
        Close();
        ClosedWithVerdict?.Invoke(this);
    }

    private void Block_Click(object s, RoutedEventArgs e)
    {
        ResultVerdict = Verdict.Block;
        if (IsPreview) { DismissPreview(); return; }
        Close();
        ClosedWithVerdict?.Invoke(this);
    }

    private void Close_Click(object s, RoutedEventArgs e)
    {
        ResultVerdict = Verdict.Block;
        if (IsPreview) { DismissPreview(); return; }
        Close();
        ClosedWithVerdict?.Invoke(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (_activePreview == this) _activePreview = null;
        if (!IsPreview) ClosedWithVerdict?.Invoke(this);
    }
}
