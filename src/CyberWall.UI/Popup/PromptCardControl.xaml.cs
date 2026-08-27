using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CyberWall.Common;
using CyberWall.Common.Geo;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.UI.Services;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using UserControl = System.Windows.Controls.UserControl;

namespace CyberWall.UI.Popup;

public partial class PromptCardControl : UserControl
{
    private readonly DispatcherTimer _feedbackTimer;
    private readonly CancellationTokenSource _cts = new();
    private AppIdentityInfo _identity;
    private string? _resolvedHost;

    public ConnectionEvent Event { get; }
    public bool IsPreview { get; }
    public event Action<PromptCardControl, PopupDecision>? DecisionMade;

    public PromptCardControl(ConnectionEvent ev, bool isPreview = false)
    {
        InitializeComponent();
        Event = ev;
        IsPreview = isPreview;
        DataContext = this;

        _identity = isPreview
            ? new AppIdentityInfo("DemoApp.exe", "Demo App", "Demo Software", true, false, false)
            : AppIdentity.Resolve(ev.AppPath);

        _feedbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        _feedbackTimer.Tick += (_, _) =>
        {
            _feedbackTimer.Stop();
            FeedbackLbl.Visibility = Visibility.Collapsed;
        };

        ApplyCopy();
        ApplyIdentity(ev.AppPath);
        ApplyDirection(ev);
        ApplyEndpoint(ev);
        ApplyButtonRoles(ev.Direction == Direction.Inbound);

        PathLbl.Text = ev.AppPath;
        ScopeLbl.Text = Strings.T("DecisionAppliesToProgram");

        GeoCountry.Updated += OnGeoUpdated;
        Unloaded += (_, _) =>
        {
            GeoCountry.Updated -= OnGeoUpdated;
            _cts.Cancel();
            _cts.Dispose();
            _feedbackTimer.Stop();
        };

        DismissBtn.ToolTip = Strings.T("DismissPromptTooltip");
        SearchBtn.ToolTip = Strings.T("SearchProcessWeb");
        CopyPathBtn.ToolTip = Strings.T("CopyFullPath");
        OpenFolderBtn.ToolTip = Strings.T("OpenExeFolder");

        AutomationProperties.SetName(DismissBtn, Strings.T("DismissPromptTooltip"));
        AutomationProperties.SetName(SearchBtn, Strings.T("SearchProcessWeb"));
        AutomationProperties.SetName(CopyPathBtn, Strings.T("CopyFullPath"));
        AutomationProperties.SetName(OpenFolderBtn, Strings.T("OpenExeFolder"));
        AutomationProperties.SetName(BlockBtn, Strings.T("Block"));
        AutomationProperties.SetName(AllowOnceBtn, Strings.T("AllowOnce"));
        AutomationProperties.SetName(AllowBtn, Strings.T("AllowAlways"));

        Loaded += (_, _) =>
        {
            if (!IsPreview)
            {
                _ = ResolveHostAsync(Event);
            }
        };
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
            var fg = new SolidColorBrush(Color.FromRgb(192, 132, 252));
            DirectionPill.Background = new SolidColorBrush(Color.FromArgb(45, 168, 85, 247));
            DirectionPill.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 168, 85, 247));
            DirectionPill.BorderThickness = new Thickness(1);
            DirectionPillText.Foreground = fg;
            DirectionPillText.Text = Strings.T("Inbound");
            DirectionArrow.Stroke = fg;
            DirectionArrow.Fill = System.Windows.Media.Brushes.Transparent;
            DirectionArrow.Data = Geometry.Parse("M 5 1.5 L 5 11 M 5 11 L 1.6 6.7 M 5 11 L 8.4 6.7");
            InboundWarn.Visibility = Visibility.Visible;
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
            InboundWarn.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyEndpoint(ConnectionEvent ev, string? host = null)
    {
        DetailLbl.Text = NetworkEndpoint.FormatPrimary(ev.Protocol, ev.RemoteAddress, ev.RemotePort, host);
        MetaLbl.Text = NetworkEndpoint.FormatSecondary(ev.RemoteAddress, ev.ProcessId, host != null);
        var geo = GeoCountry.Lookup(ev.RemoteAddress);
        CountryMark.Apply(geo);
        CountryLbl.Text = geo.Kind == GeoKind.Unknown ? "" : CountryDisplay.Label(geo);
        CountryLbl.Visibility = string.IsNullOrEmpty(CountryLbl.Text) ? Visibility.Collapsed : Visibility.Visible;
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

    private void OnGeoUpdated()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsLoaded) return;
            ApplyEndpoint(Event, _resolvedHost);
        });
    }

    private async Task ResolveHostAsync(ConnectionEvent ev)
    {
        try
        {
            var host = await NetworkEndpoint.TryResolveHostAsync(ev.RemoteAddress, _cts.Token).ConfigureAwait(true);
            if (host == null || !IsLoaded) return;
            _resolvedHost = host;
            ApplyEndpoint(ev, host);
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void Allow_Click(object s, RoutedEventArgs e) => TriggerDecision(PopupDecision.AllowAlways);
    private void AllowOnce_Click(object s, RoutedEventArgs e) => TriggerDecision(PopupDecision.AllowOnce);
    private void Block_Click(object s, RoutedEventArgs e) => TriggerDecision(PopupDecision.BlockAlways);
    private void Dismiss_Click(object s, RoutedEventArgs e) => TriggerDecision(PopupDecision.Dismiss);

    public void TriggerDecision(PopupDecision decision)
    {
        DecisionMade?.Invoke(this, decision);
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

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
