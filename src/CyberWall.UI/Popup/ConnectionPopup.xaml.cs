using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace CyberWall.UI.Popup;

public partial class ConnectionPopup : Window
{
    private const int DefaultTimeoutSeconds = 300;

    private static ConnectionPopup? _activePreview;
    private static DispatcherTimer? _previewTimer;

    private readonly DispatcherTimer _countdownTimer;
    private readonly DispatcherTimer _feedbackTimer;
    private readonly CancellationTokenSource _cts = new();
    private readonly bool _autoBlockEnabled;
    private int _remaining = DefaultTimeoutSeconds;
    private bool _countdownPaused;
    private AppIdentityInfo _identity = AppIdentity.Resolve(null);

    public ConnectionEvent Event { get; }
    public PopupDecision Decision { get; private set; } = PopupDecision.None;
    public bool TimedOut { get; private set; }
    public bool IsPreview { get; }
    public event Action<ConnectionPopup>? ClosedWithVerdict;

    public ConnectionPopup(ConnectionEvent ev, bool isPreview = false)
    {
        InitializeComponent();
        Event = ev;
        IsPreview = isPreview;
        DataContext = this;

        var timeoutSeconds = Math.Clamp(App.Settings.PopupAutoBlockSeconds, 15, 3600);
        _autoBlockEnabled = !isPreview && App.Settings.PopupAutoBlockEnabled;
        _remaining = timeoutSeconds;

        SourceInitialized += (_, _) => PopupWindowHelper.ApplyNoActivateChrome(this);
        Loaded += OnLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        MouseEnter += (_, _) => _countdownPaused = true;
        MouseLeave += (_, _) => _countdownPaused = false;
        HeaderBar.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState != MouseButtonState.Pressed) return;
            if (IsInsideButton(e.OriginalSource as DependencyObject)) return;
            try { DragMove(); } catch { }
        };

        ApplyCopy();
        ApplyIdentity(ev.AppPath);
        ApplyDirection(ev);
        ApplyEndpoint(ev);
        ApplyButtonRoles(ev.Direction == Direction.Inbound);

        PathLbl.Text = ev.AppPath;
        ScopeLbl.Text = Strings.T("DecisionAppliesToProgram");

        CloseBtn.ToolTip = Strings.T("CloseWithoutSaving");
        SearchBtn.ToolTip = Strings.T("SearchProcessWeb");
        CopyPathBtn.ToolTip = Strings.T("CopyFullPath");
        OpenFolderBtn.ToolTip = Strings.T("OpenExeFolder");
        AutomationProperties.SetName(CloseBtn, Strings.T("CloseWithoutSaving"));
        AutomationProperties.SetName(SearchBtn, Strings.T("SearchProcessWeb"));
        AutomationProperties.SetName(CopyPathBtn, Strings.T("CopyFullPath"));
        AutomationProperties.SetName(OpenFolderBtn, Strings.T("OpenExeFolder"));
        AutomationProperties.SetName(BlockBtn, Strings.T("Block"));
        AutomationProperties.SetName(AllowOnceBtn, Strings.T("AllowOnce"));
        AutomationProperties.SetName(AllowBtn, Strings.T("AllowAlways"));

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => TickCountdown();
        _feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        _feedbackTimer.Tick += (_, _) =>
        {
            _feedbackTimer.Stop();
            FeedbackLbl.Visibility = Visibility.Collapsed;
        };

        if (isPreview || !_autoBlockEnabled)
        {
            CountdownLbl.Visibility = Visibility.Collapsed;
        }
        else
        {
            UpdateCountdownLabel();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateLayout();
        var pos = App.Settings.NotificationPosition;
        var mon = App.Settings.NotificationMonitor;
        PopupWindowHelper.PositionPopup(this, pos, mon, IsPreview ? 0 : null);
        if (IsPreview) return;
        if (_autoBlockEnabled && !_countdownTimer.IsEnabled)
            _countdownTimer.Start();
        _ = ResolveHostAsync(Event);
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Dismiss();
    }

    private void ApplyCopy()
    {
        BlockBtn.Content = Strings.T("Block");
        AllowOnceBtn.Content = Strings.T("AllowOnce");
        AllowBtn.Content = Strings.T("AllowAlways");
        InboundWarnLbl.Text = Strings.T("InboundWarning");
    }

    private void ApplyIdentity(string appPath)
    {
        _identity = IsPreview
            ? new AppIdentityInfo("DemoApp.exe", "Demo App", "Demo Software", true, false, false)
            : AppIdentity.Resolve(appPath);

        TitleLbl.Text = _identity.ProductName;

        var inbound = Event.Direction == Direction.Inbound;
        var sameName = _identity.ProductName.Equals(_identity.FileName, StringComparison.OrdinalIgnoreCase);
        if (sameName)
            AppLbl.Text = inbound ? Strings.T("WantsInbound") : Strings.T("WantsToConnect");
        else if (inbound)
            AppLbl.Text = Strings.T("AppWantsInbound", _identity.FileName);
        else
            AppLbl.Text = Strings.T("AppWantsToConnect", _identity.FileName);

        string badge;
        bool trusted = _identity.IsMicrosoft || _identity.IsSystemPath || _identity.IsSigned;
        if (_identity.IsMicrosoft && _identity.IsSigned)
            badge = Strings.T("SignedMicrosoft");
        else if (_identity.IsSystemPath)
            badge = Strings.T("WindowsSystem");
        else if (_identity.IsSigned && !string.IsNullOrEmpty(_identity.Publisher))
            badge = Strings.T("SignedBy", Truncate(_identity.Publisher!, 36));
        else if (_identity.IsSigned)
            badge = Strings.T("Signed");
        else
            badge = Strings.T("Unsigned");

        PublisherLbl.Text = badge;
        if (trusted && FindResource("AccentBrush") is SolidColorBrush accent)
        {
            var c = accent.Color;
            PublisherPill.Background = new SolidColorBrush(Color.FromArgb(40, c.R, c.G, c.B));
            PublisherPill.BorderBrush = new SolidColorBrush(Color.FromArgb(80, c.R, c.G, c.B));
            PublisherPill.BorderThickness = new Thickness(1);
            PublisherLbl.Foreground = accent;
            ShieldPath.Fill = accent;
        }
        else
        {
            PublisherPill.Background = (Brush)FindResource("BadgeWarnBgBrush");
            PublisherPill.BorderBrush = (Brush)FindResource("BadgeWarnFgBrush");
            PublisherPill.BorderThickness = new Thickness(1);
            PublisherLbl.Foreground = (Brush)FindResource("BadgeWarnFgBrush");
            ShieldPath.Fill = (Brush)FindResource("BadgeWarnFgBrush");
        }

        if (inbound)
            AppLbl.Foreground = (Brush)FindResource("BadgeBlockFgBrush");
    }

    private void ApplyDirection(ConnectionEvent ev)
    {
        if (ev.Direction == Direction.Inbound)
        {
            DirectionPill.Background = new SolidColorBrush(Color.FromArgb(45, 168, 85, 247));
            DirectionPill.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 168, 85, 247));
            DirectionPill.BorderThickness = new Thickness(1);
            DirectionPillText.Foreground = new SolidColorBrush(Color.FromRgb(192, 132, 252));
            DirectionPillText.Text = "↓ " + Strings.T("Inbound");
            InboundWarn.Visibility = Visibility.Visible;
        }
        else
        {
            DirectionPill.Background = new SolidColorBrush(Color.FromArgb(40, 0, 229, 255));
            DirectionPill.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 0, 229, 255));
            DirectionPill.BorderThickness = new Thickness(1);
            DirectionPillText.Foreground = new SolidColorBrush(Color.FromRgb(0, 229, 255));
            DirectionPillText.Text = "↑ " + Strings.T("Outbound");
        }
    }

    private void ApplyEndpoint(ConnectionEvent ev, string? host = null)
    {
        DetailLbl.Text = NetworkEndpoint.FormatPrimary(ev.Protocol, ev.RemoteAddress, ev.RemotePort, host);
        MetaLbl.Text = NetworkEndpoint.FormatSecondary(ev.RemoteAddress, ev.ProcessId, host != null);
    }

    private void ApplyButtonRoles(bool inbound)
    {
        if (inbound)
        {
            BlockBtn.Style = (Style)FindResource("PopupDangerButton");
            AllowBtn.Style = (Style)FindResource("PopupBlockButton");
            AllowBtn.Foreground = (Brush)FindResource("TextBrush");
        }
        else
        {
            BlockBtn.Style = (Style)FindResource("PopupBlockButton");
            AllowBtn.Style = (Style)FindResource("PopupPrimaryButton");
        }
    }

    private async Task ResolveHostAsync(ConnectionEvent ev)
    {
        try
        {
            var host = await NetworkEndpoint.TryResolveHostAsync(ev.RemoteAddress, _cts.Token).ConfigureAwait(true);
            if (host == null || !IsLoaded) return;
            ApplyEndpoint(ev, host);
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void TickCountdown()
    {
        if (_countdownPaused) return;
        _remaining--;
        if (_remaining <= 0)
        {
            TimedOut = true;
            Complete(PopupDecision.BlockAlways);
            return;
        }
        UpdateCountdownLabel();
    }

    private void UpdateCountdownLabel()
    {
        var clock = TimeSpan.FromSeconds(Math.Max(0, _remaining)).ToString(@"m\:ss");
        CountdownLbl.Text = Strings.T("BlockIn", clock);
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
            popup.UpdateLayout();
            PopupWindowHelper.PositionPopup(popup, position, monitorIndex, explicitStackIndex: 0);
        };

        popup.Show();

        _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _previewTimer.Tick += (_, _) => DismissPreview();
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
            try { _activePreview.Close(); } catch { }
            _activePreview = null;
        }
    }

    private void Allow_Click(object s, RoutedEventArgs e) => Complete(PopupDecision.AllowAlways);
    private void AllowOnce_Click(object s, RoutedEventArgs e) => Complete(PopupDecision.AllowOnce);
    private void Block_Click(object s, RoutedEventArgs e) => Complete(PopupDecision.BlockAlways);
    private void Close_Click(object s, RoutedEventArgs e) => Dismiss();

    private void Dismiss() => Complete(PopupDecision.Dismiss);

    private void Complete(PopupDecision decision)
    {
        Decision = decision;
        if (IsPreview) { DismissPreview(); return; }
        Close();
    }

    private void Search_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var q = _identity.FileName;
            if (!string.IsNullOrEmpty(_identity.Publisher))
                q = $"\"{_identity.FileName}\" {_identity.Publisher}";
            else if (!string.IsNullOrEmpty(_identity.ProductName) &&
                     !_identity.ProductName.Equals(_identity.FileName, StringComparison.OrdinalIgnoreCase))
                q = $"\"{_identity.FileName}\" {_identity.ProductName}";

            Process.Start(new ProcessStartInfo("https://www.google.com/search?q=" + Uri.EscapeDataString(q))
            {
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void CopyPath_Click(object s, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(Event.AppPath);
            ShowFeedback(Strings.T("CopiedToClipboard"));
        }
        catch { }
    }

    private void OpenFolder_Click(object s, RoutedEventArgs e)
    {
        try
        {
            var path = Event.AppPath;
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
                return;
            }
            ShowFeedback(Strings.T("FolderOpenFailed"));
        }
        catch
        {
            ShowFeedback(Strings.T("FolderOpenFailed"));
        }
    }

    private void ShowFeedback(string text)
    {
        FeedbackLbl.Text = text;
        FeedbackLbl.Visibility = Visibility.Visible;
        _feedbackTimer.Stop();
        _feedbackTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _countdownTimer.Stop();
        _feedbackTimer.Stop();
        _cts.Cancel();
        _cts.Dispose();
        base.OnClosed(e);
        if (_activePreview == this) _activePreview = null;
        if (!IsPreview)
        {
            if (Decision == PopupDecision.None)
                Decision = PopupDecision.Dismiss;
            var handler = ClosedWithVerdict;
            ClosedWithVerdict = null;
            handler?.Invoke(this);
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is System.Windows.Controls.Button) return true;
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }
}
