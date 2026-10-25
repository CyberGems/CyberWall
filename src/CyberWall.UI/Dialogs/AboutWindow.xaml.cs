using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;

namespace CyberWall.UI.Dialogs;

public partial class AboutWindow : Window, IModalAttentionWindow
{
    private const string RepoUrl = "https://github.com/CyberGems/CyberWall";
    private const string WebsiteUrl = "https://cybergems.org";
    private const string WikiUrl = "https://github.com/CyberGems/CyberWall/wiki";
    private const string DonateUrl = "https://github.com/CyberGems/CyberWall#%EF%B8%8F-donate";

    private readonly AppSettings _settings;
    private DateTime _lastAttentionTime = DateTime.MinValue;
    private readonly IReadOnlyList<SuiteApp> _suiteApps = SuiteCatalog.Pick(5);

    public void TriggerAttention()
    {
        ModalAttentionHelper.Trigger(this, OuterBorder, WindowScale, WindowGlow, ref _lastAttentionTime);
    }

    public AboutWindow(AppSettings settings, bool checkUpdatesNow = false)
    {
        _settings = settings;
        InitializeComponent();
        Icon = AppIconHelper.CreateShieldImageSource(64);
        CyberWallWindowChrome.Apply(this, 12);
        Title = Strings.T("WindowAbout");
        LoadContent();

        if (checkUpdatesNow)
        {
            Loaded += (_, _) =>
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
                {
                    UpdateCheckButton_Click(UpdateBtn, new RoutedEventArgs());
                });
            };
        }
    }

    private void LoadContent()
    {
        AutoCheckUpdateCheck.IsChecked = _settings.AutoCheckForUpdates;
        RefreshLocalization();
    }

    public void RefreshLocalization()
    {
        var es = Strings.Current == Lang.Es;
        Title = Strings.T("AboutTitle");
        AboutTitleText.Text = Strings.T("AboutTitle");
        var currentVerLabel = UpdateService.GetCurrentVersionLabel();
        AboutVersionText.Text = (es ? "Versión " : "Version ") + currentVerLabel;
        AboutDescriptionText.Text = Strings.T("AboutDescription");
        SuiteSectionLbl.Text = Strings.T("MoreAppsFromCyberGems");
        SuiteMoreBtn.Content = Strings.T("SeeMoreApps");
        RefreshSuiteApps();
        UpdatesSectionLbl.Text = Strings.T("UpdatesMaintenance");
        AutoUpdateTitleLbl.Text = Strings.T("AutoUpdateTitle");
        AutoUpdateDescLbl.Text = Strings.T("AutoUpdateDesc");
        CheckUpdateTitleLbl.Text = Strings.T("CheckUpdates");
        CheckUpdateDescLbl.Text = Strings.T("CheckUpdatesDesc");
        UpdateBtn.Content = Strings.T("CheckNow");
        AboutFooterCopyright.ToolTip = Strings.T("VisitWebsite");
        AboutFooterWebsiteBtn.ToolTip = Strings.T("VisitWebsite");
        AboutFooterDocsBtn.ToolTip = Strings.T("OnlineDocs");
        AboutFooterGithubBtn.ToolTip = Strings.T("ViewGitHub");
        AboutFooterIssuesBtn.ToolTip = Strings.T("ReportIssue");
        AboutFooterReleasesBtn.ToolTip = Strings.T("ViewReleases");
        AboutFooterDonateBtn.ToolTip = Strings.T("DonateToProject");
    }

    private void RefreshSuiteApps()
    {
        SuiteAppsHost.Children.Clear();
        foreach (var app in _suiteApps)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri($"pack://application:,,,/Assets/Suite/{app.Slug}.png", UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 96;
            bitmap.EndInit();
            bitmap.Freeze();

            var image = new System.Windows.Controls.Image
            {
                Width = 28, Height = 28, Source = bitmap,
                Stretch = Stretch.Uniform, Opacity = 0.7, IsHitTestVisible = false
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            var button = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("SuiteAppButton"), Content = image,
                ToolTip = $"{app.Name}: {Strings.T(app.PitchKey)}"
            };
            System.Windows.Automation.AutomationProperties.SetName(button, app.Name);
            void UpdateOpacity() => image.Opacity = button.IsMouseOver || button.IsKeyboardFocused ? 1 : 0.7;
            button.MouseEnter += (_, _) => UpdateOpacity();
            button.MouseLeave += (_, _) => UpdateOpacity();
            button.GotKeyboardFocus += (_, _) => UpdateOpacity();
            button.LostKeyboardFocus += (_, _) => UpdateOpacity();
            button.Click += (_, _) => OpenUrl(app.Site);
            SuiteAppsHost.Children.Add(button);
        }
    }

    private void SuiteMoreBtn_Click(object sender, RoutedEventArgs e) => OpenUrl("https://cybergems.org/#apps");

    private void AutoCheckUpdateCheck_Click(object sender, RoutedEventArgs e)
    {
        _settings.AutoCheckForUpdates = AutoCheckUpdateCheck.IsChecked == true;
        _settings.Save();
    }

    private async void UpdateCheckButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateBtn.IsEnabled = false;
        UpdateBtn.Content = Strings.T("CheckingUpdates");

        try
        {
            var result = await UpdateService.CheckForUpdatesAsync();
            UpdateBtn.IsEnabled = true;
            UpdateBtn.Content = Strings.T("CheckNow");

            if (result.IsUpdateAvailable)
            {
                new CyberWall.Common.Notifications.NotificationStore().Add(CyberWall.Common.Models.AppNotificationKind.UpdateAvailable, detail: result.LatestVersionLabel);
                var currentLabel = Strings.T("Current");
                var latestLabel = Strings.T("Latest");
                var promptMessage = Strings.T("UpdatePrompt");
                var currentVerLabel = UpdateService.GetCurrentVersionLabel();
                var msg = $"{currentLabel} {currentVerLabel}\n{latestLabel} {result.LatestVersionLabel}\n\n{promptMessage}";

                var choice = ConfirmDialog.Show(
                    this,
                    Strings.T("UpdateAvailable", result.LatestVersionLabel),
                    msg,
                    Strings.T("Download"),
                    Strings.T("Later"));

                if (choice)
                {
                    await StartUpdateDownloadAsync(result);
                }
            }
            else
            {
                ConfirmDialog.Show(
                    this,
                    Strings.T("CheckUpdates"),
                    result.StatusMessage,
                    Strings.T("Ok"),
                    string.Empty,
                    ConfirmIconType.Check);
            }
        }
        catch (Exception ex)
        {
            UpdateBtn.IsEnabled = true;
            UpdateBtn.Content = Strings.T("CheckNow");
            ConfirmDialog.Show(
                this,
                Strings.T("CheckUpdates"),
                ex.Message,
                Strings.T("Ok"),
                string.Empty);
        }
    }

    public async Task StartUpdateDownloadAsync(UpdateCheckResult result)
    {
        UpdateProgressPanel.Visibility = Visibility.Visible;
        UpdateBtn.IsEnabled = false;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var updatesFolder = Path.Combine(appData, "CyberWall", "Updates");
        var filename = result.AssetName ?? $"CyberWall_setup_{UpdateService.GetRuntimeChannel()}.exe";
        var installerPath = Path.Combine(updatesFolder, filename);

        var progress = new Progress<double>(val =>
        {
            UpdateProgressBar.Value = val;
            UpdateProgressText.Text = string.Format(Strings.T("DownloadingUpdate"), val);
        });

        try
        {
            UpdateProgressBar.Value = 0;
            UpdateProgressText.Text = string.Format(Strings.T("DownloadingUpdate"), 0.0);

            if (string.IsNullOrEmpty(result.DownloadUrl))
                throw new Exception("Direct download link is not available for this release.");

            await UpdateService.DownloadVerifiedAsync(result.DownloadUrl, installerPath, result.AssetSha256, progress);

            UpdateProgressText.Text = Strings.T("DownloadCompleted");

            ConfirmDialog.Show(
                this,
                Strings.T("DownloadComplete"),
                Strings.T("DownloadCompleteDesc"),
                Strings.T("Ok"),
                string.Empty,
                ConfirmIconType.Check);

            UpdateService.LaunchInstallerAndExit(installerPath);
        }
        catch (Exception ex)
        {
            UpdateProgressPanel.Visibility = Visibility.Collapsed;
            UpdateBtn.IsEnabled = true;

            var errorChoice = ConfirmDialog.Show(
                this,
                Strings.T("DownloadFailed"),
                $"{ex.Message}\n\n{(Strings.Current == Lang.Es ? "¿Deseas abrir la página de descargas de GitHub en el navegador?" : "Would you like to open the GitHub release page instead?")}",
                Strings.Current == Lang.Es ? "Abrir navegador" : "Open Browser",
                Strings.T("Cancel"));

            if (errorChoice && !string.IsNullOrEmpty(result.ReleaseUrl))
            {
                OpenUrl(result.ReleaseUrl);
            }
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void AboutFooterWebsite_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(WebsiteUrl);
    }

    private void AboutFooterDocs_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(WikiUrl);
    }

    private void AboutFooterGithub_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(RepoUrl);
    }

    private void AboutFooterIssues_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl($"{RepoUrl}/issues");
    }

    private void AboutFooterReleases_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl($"{RepoUrl}/releases");
    }

    private void AboutFooterDonate_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(DonateUrl);
    }

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
