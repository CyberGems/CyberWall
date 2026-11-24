using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Threading;
using CyberWall.Common.I18n;
using CyberWall.Common.Settings;
using CyberWall.UI.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace CyberWall.UI.Dialogs;

internal enum UpdatePhase { Available, Downloading, Ready }

public partial class UpdateNoticeWindow : Window
{
    private int _monitorIndex = -1;
    private PopupPosition _position;
    private bool _positionQueued;
    private UpdateCheckResult? _release;
    private UpdatePhase _phase;
    private UpdateDownloadProgress _progress;
    private bool _cancelling;
    private bool _installing;
    private string? _error;

    internal event Action? OpenReleaseRequested;
    internal event Action? SkipRequested;
    internal event Action? CancelDownloadRequested;
    internal event Func<Task>? PrimaryRequested;

    public UpdateNoticeWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => PositionAtMonitorEdge();
    }

    internal void Present(int monitorIndex, PopupPosition position, bool activate)
    {
        _monitorIndex = monitorIndex;
        _position = position;
        var screens = PopupWindowHelper.GetSortedScreens();
        if (monitorIndex >= 0 && monitorIndex < screens.Length)
        {
            var screen = screens[monitorIndex];
            var (scaleX, scaleY) = PopupWindowHelper.GetMonitorScale(screen);
            Width = Math.Min(480, Math.Max(1, screen.WorkingArea.Width / scaleX - 16));
            MaxHeight = Math.Max(1, screen.WorkingArea.Height / scaleY - 16);
        }

        if (!IsVisible)
        {
            Opacity = 0;
            PopupWindowHelper.PositionWindow(this, _position, _monitorIndex, explicitStackIndex: 0);
            Show();
        }
        UpdateLayout();
        PopupWindowHelper.PositionWindow(this, _position, _monitorIndex, explicitStackIndex: 0);
        Opacity = 1;
        if (activate)
        {
            Activate();
            LaterButton.Focus();
        }
    }

    private void PositionAtMonitorEdge()
    {
        if (!IsVisible || _positionQueued) return;
        _positionQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Render, () =>
        {
            _positionQueued = false;
            if (IsVisible)
                PopupWindowHelper.PositionWindow(this, _position, _monitorIndex, explicitStackIndex: 0);
        });
    }

    internal void Render(UpdateCheckResult release, UpdatePhase phase, UpdateDownloadProgress progress,
        bool cancelling, bool installing, string? error)
    {
        _release = release;
        _phase = phase;
        _progress = progress;
        _cancelling = cancelling;
        _installing = installing;
        _error = error;
        RefreshLocalization();
    }

    internal void RefreshLocalization()
    {
        if (_release is not { } release) return;
        bool available = _phase == UpdatePhase.Available;
        bool downloading = _phase == UpdatePhase.Downloading;
        Title = StageTitle.Text = Strings.T(_phase switch
        {
            UpdatePhase.Downloading => _cancelling ? "UpdateCancelling" : "UpdateDownloadingTitle",
            UpdatePhase.Ready => "UpdateReady",
            _ => "UpdateAvailableTitle"
        });
        VersionLabel.Text = $"{Strings.T("InstalledVersion")} {UpdateService.GetCurrentVersionLabel()} · " +
            $"{Strings.T("AvailableVersion")} {release.LatestVersionLabel}" + (release.PublishedAt is { } published
            ? $" · {published.ToLocalTime().ToString("d", CultureInfo.CurrentCulture)}" : "");

        NotesTitle.Text = Strings.T("UpdateHighlights");
        var notes = ReleaseNotes.Peek(release.ReleaseNotes, Strings.Current);
        NotesList.ItemsSource = notes;
        NotesUnavailable.Text = Strings.T("UpdateNotesUnavailable");
        NotesUnavailable.Visibility = VisibleWhen(notes.Length == 0);
        var error = _error ?? (available ? release.PackageError : null);
        ErrorText.Text = error is null ? "" : Strings.T(error);
        ErrorText.Visibility = VisibleWhen(error is not null);

        GitHubButton.Content = Strings.T("UpdateViewGitHub");
        GitHubButton.Visibility = VisibleWhen(available);
        GitHubButton.IsEnabled = UpdateService.IsGitHubUrl(release.ReleaseUrl);
        SkipButton.Content = Strings.T("UpdateSkipVersion");
        SkipButton.Visibility = VisibleWhen(available);
        LaterButton.Content = Strings.T(downloading ? "UpdateDownloadBackground" : "Later");
        CancelButton.Content = Strings.T("UpdateCancelDownload");
        CancelButton.Visibility = VisibleWhen(downloading);
        CancelButton.IsEnabled = !_cancelling;
        PrimaryButton.Content = Strings.T(available ? "UpdateNow" :
            string.Equals(Path.GetExtension(release.AssetName), ".zip", StringComparison.OrdinalIgnoreCase)
                ? "UpdateOpenDownloadFolder" : "UpdateRestart");
        PrimaryButton.Visibility = VisibleWhen(!downloading);
        PrimaryButton.IsEnabled = !_installing && (!available || release.PackageError is null);
        var dismiss = Strings.T(downloading ? "UpdateDownloadBackground" : "Close");
        DismissButton.ToolTip = dismiss;
        AutomationProperties.SetName(DismissButton, dismiss);
        DownloadPanel.Visibility = VisibleWhen(downloading);
        if (downloading) RenderProgress(_progress);
    }

    internal void RenderProgress(UpdateDownloadProgress progress)
    {
        _progress = progress;
        var label = Strings.T("UpdateDownloadProgress");
        var downloaded = $"{progress.Downloaded / 1_000_000d:0.0} MB";
        var total = progress.Total is > 0 ? $" / {progress.Total.Value / 1_000_000d:0.0} MB" : "";
        ProgressText.Text = $"{label}   {downloaded}{total}";
        AutomationProperties.SetName(DownloadProgressBar, label);
        DownloadProgressBar.IsIndeterminate = progress.Total is not > 0;
        DownloadProgressBar.Value = progress.Total is > 0
            ? Math.Min(100, 100d * progress.Downloaded / progress.Total.Value)
            : 0;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close();
    }

    private void GitHub_Click(object sender, RoutedEventArgs e) => OpenReleaseRequested?.Invoke();
    private void Skip_Click(object sender, RoutedEventArgs e) => SkipRequested?.Invoke();
    private void Cancel_Click(object sender, RoutedEventArgs e) => CancelDownloadRequested?.Invoke();
    private void Dismiss_Click(object sender, RoutedEventArgs e) => Close();

    private async void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (PrimaryRequested is { } action) await action();
    }

    private static Visibility VisibleWhen(bool visible) => visible ? Visibility.Visible : Visibility.Collapsed;
}
