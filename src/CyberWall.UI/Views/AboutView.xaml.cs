using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;
using UserControl = System.Windows.Controls.UserControl;
using WpfApplication = System.Windows.Application;

namespace CyberWall.UI.Views;

public partial class AboutView : UserControl
{
    private enum InlineUpdateState
    {
        None,
        Checking,
        UpToDate,
        UpdateAvailable,
        CheckError
    }

    private const string RepoUrl = "https://github.com/CyberGems/CyberWall";
    private const string WebsiteUrl = "https://cybergems.org";
    private const string WikiUrl = "https://github.com/CyberGems/CyberWall/wiki";
    private const string DonateUrl = "https://github.com/CyberGems/CyberWall#%EF%B8%8F-donate";

    private readonly AppSettings _settings;
    private readonly IReadOnlyList<SuiteApp> _suiteApps = SuiteCatalog.Pick(5);
    private bool _isChecking;
    private InlineUpdateState _inlineState;
    private UpdateCheckResult? _lastResult;
    private DispatcherTimer? _diagTipTimer;

    public event EventHandler? BackRequested;
    internal Func<Task<UpdateCheckResult>>? CheckForUpdates { get; set; }

    public AboutView()
        : this(App.Settings ?? new AppSettings())
    {
    }

    internal AboutView(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        Loaded += (_, _) => LoadContent();
    }

    public void Activate()
    {
        LoadContent();
    }

    public void Deactivate()
    {
        _diagTipTimer?.Stop();
        if (AboutFooterDiagBtn != null)
        {
            AboutFooterDiagBtn.ToolTip = Strings.T("CopyDiagInfo");
        }
    }

    private void LoadContent()
    {
        SuiteSection.Visibility = _settings.ShowAppRecommendations ? Visibility.Visible : Visibility.Collapsed;
        RefreshLanguage();
    }

    public void RefreshLanguage()
    {
        var es = Strings.Current == Lang.Es;
        AboutTitleText.Text = Strings.T("AboutTitle");
        var currentVerLabel = UpdateService.GetCurrentVersionLabel();
        AboutVersionText.Text = (es ? "Versión " : "Version ") + currentVerLabel;
        AboutDescriptionText.Text = Strings.T("AboutDescription");

        BackBtnText.Text = Strings.T("Back");
        AutomationProperties.SetName(BackBtn, Strings.T("Back"));

        SuiteSectionLbl.Text = Strings.T("MoreAppsFromCyberGems");
        SuiteMoreBtn.Content = Strings.T("SeeMoreApps");
        RefreshSuiteApps();

        UpdatesSectionLbl.Text = Strings.T("UpdatesMaintenance");
        CheckUpdateTitleLbl.Text = Strings.T("CheckUpdates");
        CheckUpdateDescLbl.Text = Strings.T("CheckUpdatesDesc");
        if (!_isChecking)
        {
            UpdateBtn.Content = Strings.T("CheckNow");
        }
        else if (_isChecking)
        {
            UpdateBtn.Content = Strings.T("CheckingUpdates");
        }
        RenderInlineUpdateState();

        AboutFooterCopyright.ToolTip = Strings.T("VisitWebsite");
        AboutFooterWebsiteBtn.ToolTip = Strings.T("VisitWebsite");
        AutomationProperties.SetName(AboutFooterWebsiteBtn, Strings.T("VisitWebsite"));
        AboutFooterDocsBtn.ToolTip = Strings.T("OnlineDocs");
        AutomationProperties.SetName(AboutFooterDocsBtn, Strings.T("OnlineDocs"));
        AboutFooterGithubBtn.ToolTip = Strings.T("ViewGitHub");
        AutomationProperties.SetName(AboutFooterGithubBtn, Strings.T("ViewGitHub"));
        AboutFooterIssuesBtn.ToolTip = Strings.T("ReportIssue");
        AutomationProperties.SetName(AboutFooterIssuesBtn, Strings.T("ReportIssue"));
        AboutFooterDiagBtn.ToolTip = Strings.T("CopyDiagInfo");
        AutomationProperties.SetName(AboutFooterDiagBtn, Strings.T("CopyDiagInfo"));
        AboutFooterReleasesBtn.ToolTip = Strings.T("ViewReleases");
        AutomationProperties.SetName(AboutFooterReleasesBtn, Strings.T("ViewReleases"));
        AboutFooterDonateBtn.ToolTip = Strings.T("DonateToProject");
        AutomationProperties.SetName(AboutFooterDonateBtn, Strings.T("DonateToProject"));
    }

    private void RefreshSuiteApps()
    {
        SuiteAppsHost.Children.Clear();
        foreach (var app in _suiteApps)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/CyberWall;component/Assets/Suite/{app.Slug}.png", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 96;
            bitmap.EndInit();
            bitmap.Freeze();

            var image = new System.Windows.Controls.Image
            {
                Width = 28, Height = 28, Source = bitmap,
                Stretch = Stretch.Uniform, Opacity = 0.78, IsHitTestVisible = false
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var button = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("SuiteAppButton"), Content = image,
                ToolTip = $"{app.Name}: {Strings.T(app.PitchKey)}"
            };
            AutomationProperties.SetName(button, app.Name);
            void UpdateOpacity() => image.Opacity = button.IsMouseOver || button.IsKeyboardFocused ? 1 : 0.78;
            button.MouseEnter += (_, _) => UpdateOpacity();
            button.MouseLeave += (_, _) => UpdateOpacity();
            button.GotKeyboardFocus += (_, _) => UpdateOpacity();
            button.LostKeyboardFocus += (_, _) => UpdateOpacity();
            button.Click += (_, _) => OpenUrl(app.Site);
            SuiteAppsHost.Children.Add(button);
        }
    }

    public void CheckUpdatesNow()
    {
        if (_isChecking) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (!_isChecking)
                UpdateCheckButton_Click(UpdateBtn, new RoutedEventArgs());
        });
    }

    private Window? OwnerWindow => Window.GetWindow(this) ?? WpfApplication.Current.MainWindow;

    private void BackBtn_Click(object sender, RoutedEventArgs e) => BackRequested?.Invoke(this, EventArgs.Empty);

    private void SuiteMoreBtn_Click(object sender, RoutedEventArgs e) => OpenUrl("https://cybergems.org/#apps");

    private async void UpdateCheckButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isChecking || CheckForUpdates is not { } check) return;
        _isChecking = true;
        UpdateBtn.IsEnabled = false;
        UpdateBtn.Content = Strings.T("CheckingUpdates");
        _inlineState = InlineUpdateState.Checking;
        RenderInlineUpdateState();

        try
        {
            ApplyCheckResult(await check());
        }
        finally
        {
            _isChecking = false;
            UpdateBtn.IsEnabled = true;
            UpdateBtn.Content = Strings.T("CheckNow");
        }
    }

    internal void ApplyCheckResult(UpdateCheckResult result)
    {
        _lastResult = result;
        _inlineState = result.IsUpdateAvailable ? InlineUpdateState.UpdateAvailable :
            result.IsCheckError ? InlineUpdateState.CheckError : InlineUpdateState.UpToDate;
        RenderInlineUpdateState();
    }

    private void RenderInlineUpdateState()
    {
        if (_inlineState == InlineUpdateState.None)
        {
            UpdateStatusBanner.Visibility = Visibility.Collapsed;
            return;
        }

        UpdateStatusBanner.Visibility = Visibility.Visible;

        switch (_inlineState)
        {
            case InlineUpdateState.Checking:
                SetBannerStyle("CardSecondaryBrush", "BorderBrush", "SelectionBgBrush", "BorderBrush", "AccentBrush", "TextBrush",
                    "M 4 4 V 9 H 9 M 20 20 V 15 H 15 M 20 9 A 9 9 0 0 0 5.64 5.64 L 4 9 M 4 15 A 9 9 0 0 0 18.36 18.36 L 20 15");
                UpdateStatusTitle.Text = Strings.T("CheckingUpdates");
                UpdateStatusDetail.Text = Strings.T("UpdateCheckingDetail", UpdateService.GetCurrentVersionLabel());
                break;

            case InlineUpdateState.UpToDate:
                SetBannerStyle("BadgeAllowBgBrush", "BadgeAllowFgBrush", "CardBrush", "BadgeAllowFgBrush", "BadgeAllowFgBrush", "BadgeAllowFgBrush",
                    "M 5 13 L 10 18 L 19 7");
                UpdateStatusTitle.Text = Strings.T("UpToDateHeadline");
                UpdateStatusDetail.Text = Strings.T("UpToDate", UpdateService.GetCurrentVersionLabel());
                break;

            case InlineUpdateState.UpdateAvailable:
                SetBannerStyle("SelectionBgBrush", "AccentBrush", "CardBrush", "AccentBrush", "AccentBrush", "AccentBrush",
                    "M 12 4 V 15 M 7 10 L 12 15 L 17 10 M 5 20 H 19");
                var verLabel = _lastResult?.LatestVersionLabel ?? "";
                UpdateStatusTitle.Text = Strings.T("UpdateAvailable", verLabel);
                UpdateStatusDetail.Text = "";
                break;

            case InlineUpdateState.CheckError:
                SetBannerStyle("BadgeWarnBgBrush", "BadgeWarnFgBrush", "CardBrush", "BadgeWarnFgBrush", "BadgeWarnFgBrush", "BadgeWarnFgBrush",
                    "M 12 3 L 22 20 H 2 Z M 12 10 V 14 M 12 17 V 17.5");
                UpdateStatusTitle.Text = Strings.T("UpdateCheckErrorTitle");
                UpdateStatusDetail.Text = _lastResult?.StatusMessage ?? "";
                break;
        }
    }

    private void SetBannerStyle(
        string bannerBgKey,
        string bannerBorderKey,
        string iconBoxBgKey,
        string iconBoxBorderKey,
        string iconStrokeKey,
        string titleFgKey,
        string pathData)
    {
        UpdateStatusBanner.SetResourceReference(BackgroundProperty, bannerBgKey);
        UpdateStatusBanner.SetResourceReference(BorderBrushProperty, bannerBorderKey);
        UpdateStatusIconBox.SetResourceReference(BackgroundProperty, iconBoxBgKey);
        UpdateStatusIconBox.SetResourceReference(BorderBrushProperty, iconBoxBorderKey);
        UpdateStatusIconPath.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, iconStrokeKey);
        UpdateStatusTitle.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, titleFgKey);
        UpdateStatusIconPath.Data = Geometry.Parse(pathData);
    }

    private void AboutFooterWebsite_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(WebsiteUrl);
    }

    private void AboutFooterWebsiteBtn_Click(object sender, RoutedEventArgs e) => OpenUrl(WebsiteUrl);

    private void AboutFooterDocs_Click(object sender, RoutedEventArgs e) => OpenUrl(WikiUrl);

    private void AboutFooterGithub_Click(object sender, RoutedEventArgs e) => OpenUrl(RepoUrl);

    private void AboutFooterIssues_Click(object sender, RoutedEventArgs e) => OpenUrl($"{RepoUrl}/issues");

    private void AboutFooterDiag_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(BuildDiagnosticReport());
        }
        catch
        {
            return;
        }

        AboutFooterDiagBtn.ToolTip = Strings.T("DiagInfoCopied");
        _diagTipTimer?.Stop();
        _diagTipTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        _diagTipTimer.Tick += (_, _) =>
        {
            _diagTipTimer?.Stop();
            AboutFooterDiagBtn.ToolTip = Strings.T("CopyDiagInfo");
        };
        _diagTipTimer.Start();
    }

    private string BuildDiagnosticReport()
    {
        var win = OwnerWindow;
        var displayLine = "Unknown";
        try
        {
            if (win != null)
            {
                var hwnd = new WindowInteropHelper(win).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    var screen = System.Windows.Forms.Screen.FromHandle(hwnd);
                    var dpi = VisualTreeHelper.GetDpi(win);
                    displayLine = $"{screen.Bounds.Width}x{screen.Bounds.Height} at ({screen.Bounds.X}, {screen.Bounds.Y}), DPI {dpi.PixelsPerInchX:0} ({dpi.DpiScaleX * 100:0}%)";
                }
            }
        }
        catch { }

        return string.Join(Environment.NewLine,
            "CyberWall diagnostic report",
            "",
            $"- Version: {UpdateService.GetCurrentVersionLabel()}",
            $"- OS: {RuntimeInformation.OSDescription}",
            $"- Display: {displayLine}",
            $"- Language: {_settings.Language} (resolved {Strings.Current})",
            $"- Theme: {_settings.Theme}",
            $"- Firewall enabled: {(_settings.FirewallEnabled ? "On" : "Off")} ({_settings.FirewallMode})",
            $"- Run at startup: {(_settings.RunAtStartup ? "On" : "Off")} (minimized: {(_settings.StartMinimized ? "On" : "Off")})",
            $"- Auto-backups: {(_settings.AutoBackupEnabled ? "On" : "Off")} ({_settings.AutoBackupFrequency}, keep {_settings.BackupRetentionCount})",
            $"- Check for updates on startup: {(_settings.AutoCheckForUpdates ? "On" : "Off")}",
            $"- Runtime: {RuntimeInformation.FrameworkDescription}",
            $"- Local time: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz} ({TimeZoneInfo.Local.DisplayName})");
    }

    private void AboutFooterReleases_Click(object sender, RoutedEventArgs e) => OpenUrl($"{RepoUrl}/releases");

    private void AboutFooterDonate_Click(object sender, RoutedEventArgs e) => OpenUrl(DonateUrl);

    private static void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { }
    }
}

