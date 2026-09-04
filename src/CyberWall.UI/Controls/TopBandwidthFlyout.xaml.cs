using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.UI.Services;
using UserControl = System.Windows.Controls.UserControl;
using Button = System.Windows.Controls.Button;

namespace CyberWall.UI.Controls;

public partial class TopBandwidthFlyout : UserControl
{
    public event Action<string>? QuickBlockRequested;
    public event Action? ShowHistoryRequested;

    private readonly DispatcherTimer _refreshTimer;

    public TopBandwidthFlyout()
    {
        InitializeComponent();
        RefreshLanguage();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
        _refreshTimer.Tick += (_, _) => UpdateData();

        FlyoutPopup.Opened += (_, _) =>
        {
            UpdateData();
            _refreshTimer.Start();
        };

        FlyoutPopup.Closed += (_, _) =>
        {
            _refreshTimer.Stop();
        };
    }

    public void Toggle(UIElement placementTarget)
    {
        FlyoutPopup.PlacementTarget = placementTarget;
        FlyoutPopup.IsOpen = !FlyoutPopup.IsOpen;
    }

    public void Close()
    {
        FlyoutPopup.IsOpen = false;
    }

    public void RefreshLanguage()
    {
        FlyoutTitle.Text = Strings.T("BandwidthTitle");
        FlyoutSubTitle.Text = Strings.T("BandwidthTopProcesses");
        EmptyStateText.Text = Strings.T("BandwidthNoTraffic");
        FooterText.Text = Strings.T("BandwidthFooter");
        HistoryBtn.Content = Strings.T("BandwidthHistory");
    }

    private void UpdateData()
    {
        var top = ProcessBandwidthService.Instance.GetTopConsumers(6);
        ConsumersList.ItemsSource = top;

        bool hasItems = top.Count > 0;
        ConsumersList.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
        EmptyStateBorder.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;

        var snapshot = NetworkSpeedService.Instance.CurrentSnapshot;
        double totalBps = snapshot.DownloadBps + snapshot.UploadBps;
        TotalThroughputText.Text = $"↓ {NetworkSpeedService.FormatSpeed(snapshot.DownloadBps)}  ↑ {NetworkSpeedService.FormatSpeed(snapshot.UploadBps)}";
    }

    private void QuickBlock_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string appPath && !string.IsNullOrWhiteSpace(appPath))
        {
            QuickBlockRequested?.Invoke(appPath);
            UpdateData();
        }
    }

    private void HistoryBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
        ShowHistoryRequested?.Invoke();
    }
}
