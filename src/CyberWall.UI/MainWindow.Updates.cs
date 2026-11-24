using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;
using CyberWall.UI.Dialogs;
using CyberWall.UI.Services;
using Forms = System.Windows.Forms;

namespace CyberWall.UI;

public partial class MainWindow
{
    private readonly CancellationTokenSource _updateLifetime = new();
    private Task<UpdateCheckResult>? _updateCheck;
    private bool _hasCheckedUpdates;
    private bool _manualUpdateRequested;
    private bool _updatesClosed;
    private UpdateCheckResult? _activeUpdateResult;
    private UpdateNoticeWindow? _updateNotice;
    private UpdatePhase _updatePhase;
    private CancellationTokenSource? _updateDownload;
    private UpdateDownloadProgress _updateProgress;
    private string? _updateError;
    private string? _verifiedPackage;
    private bool _installRequested;

    internal async void CheckForUpdatesOnStartup()
    {
        _notifications.PurgeObsoleteUpdateNotifications(UpdateService.GetCurrentVersion());
        if (Version.TryParse(App.Settings.SkippedUpdateVersion, out var skipped))
            _notifications.PurgeObsoleteUpdateNotifications(skipped);
        if (_svc.IsMasterOn)
            _notifications.PurgeObsoleteProtectionOffNotifications();

        if (_hasCheckedUpdates || !App.Settings.AutoCheckForUpdates) return;
        _hasCheckedUpdates = true;
        try
        {
            await Task.Delay(3000, _updateLifetime.Token);
            if (_manualUpdateRequested || !App.Settings.AutoCheckForUpdates) return;
            var result = await GetUpdateResultAsync();
            if (_updatesClosed || _manualUpdateRequested || !App.Settings.AutoCheckForUpdates) return;

            RecordUpdateResult(result, manual: false);
            if (_activeUpdateResult is not null && !AppSettings.ShouldSuppressRegularFloatingAlert(App.Settings))
                ShowUpdateNotice(activate: false);
        }
        catch (OperationCanceledException) when (_updatesClosed) { }
    }

    private async Task<UpdateCheckResult> GetUpdateResultAsync()
    {
        if (_updatesClosed) return UpdateService.CheckFailed("UpdateCheckConnectionError");
        var check = _updateCheck ??= UpdateService.CheckForUpdatesAsync(_updateLifetime.Token);
        try
        {
            return await check;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Could not check for updates: {ex}");
            return UpdateService.CheckFailed("UpdateCheckConnectionError");
        }
        finally
        {
            if (ReferenceEquals(_updateCheck, check)) _updateCheck = null;
        }
    }

    private async Task<UpdateCheckResult> CheckForUpdateFromAboutAsync()
    {
        _manualUpdateRequested = true;
        var result = await GetUpdateResultAsync();
        if (_updatesClosed) return result;

        RecordUpdateResult(result, manual: true);
        if (result.IsUpdateAvailable || _updatePhase != UpdatePhase.Available)
            TryOpenPendingUpdate();
        return result;
    }

    private void RecordUpdateResult(UpdateCheckResult result, bool manual)
    {
        if (result.IsCheckError || _updatePhase != UpdatePhase.Available) return;
        bool skipped = result.LatestVersion is { } latest &&
            latest.ToString() == App.Settings.SkippedUpdateVersion;
        if (!result.IsUpdateAvailable)
        {
            ClearPendingUpdate();
            _notifications.RemoveRelated(AppNotificationKind.UpdateAvailable, null);
            return;
        }

        if (skipped && !manual) return;

        if (_activeUpdateResult?.LatestVersion != result.LatestVersion)
            _updateError = null;
        _activeUpdateResult = result;
        if (!skipped && !_notifications.All.Any(n => n.Kind == AppNotificationKind.UpdateAvailable &&
            string.Equals(n.Detail, result.LatestVersionLabel, StringComparison.OrdinalIgnoreCase)))
            _notifications.Add(AppNotificationKind.UpdateAvailable, detail: result.LatestVersionLabel);
        UpdateAvailableButtonState();
        RenderUpdateNotice();
    }

    private bool TryOpenPendingUpdate()
    {
        if (_activeUpdateResult is null || _updatesClosed) return false;
        _notifications.MarkRelatedRead(AppNotificationKind.UpdateAvailable, null);
        ShowUpdateNotice(activate: true);
        return true;
    }

    private async void OpenUpdateFromNotification()
    {
        if (TryOpenPendingUpdate()) return;
        _manualUpdateRequested = true;
        var result = await GetUpdateResultAsync();
        if (_updatesClosed) return;

        RecordUpdateResult(result, manual: true);
        if (TryOpenPendingUpdate()) return;
        OpenAbout();
        AboutTabContent.ApplyCheckResult(result);
    }

    private void ShowUpdateNotice(bool activate)
    {
        if (_updatesClosed || _activeUpdateResult is null) return;
        if (!activate && PopupWindowHelper.HasOpenPermissionPopup()) return;

        if (_updateNotice is null)
        {
            var notice = new UpdateNoticeWindow();
            notice.Closed += (_, _) => _updateNotice = null;
            notice.OpenReleaseRequested += OpenUpdateRelease;
            notice.SkipRequested += SkipPendingUpdate;
            notice.CancelDownloadRequested += CancelUpdateDownload;
            notice.PrimaryRequested += RunUpdateActionAsync;
            _updateNotice = notice;
        }

        RenderUpdateNotice();
        var screen = activate && IsVisible && WindowState != WindowState.Minimized
            ? Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle)
            : Forms.Screen.FromPoint(Forms.Cursor.Position);
        var screens = PopupWindowHelper.GetSortedScreens();
        int selectedMonitor = App.Settings.NotificationMonitor;
        int monitor = !activate && selectedMonitor >= 0 && selectedMonitor < screens.Length
            ? selectedMonitor : Array.FindIndex(screens, candidate => candidate.DeviceName == screen.DeviceName);
        _updateNotice.Present(monitor, App.Settings.NotificationPosition, activate);
    }

    private void RenderUpdateNotice()
    {
        if (_activeUpdateResult is { } result)
            _updateNotice?.Render(result, _updatePhase, _updateProgress,
                _updateDownload?.IsCancellationRequested == true, _installRequested, _updateError);
    }

    private void OpenUpdateRelease()
    {
        if (_activeUpdateResult is not { } result || !UpdateService.IsGitHubUrl(result.ReleaseUrl)) return;
        try { Process.Start(new ProcessStartInfo(result.ReleaseUrl) { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) { ReportUpdateError(ex, "UpdateOpenReleaseFailed"); }
    }

    private void SkipPendingUpdate()
    {
        if (_updatePhase != UpdatePhase.Available || _activeUpdateResult?.LatestVersion is not { } version) return;
        var previous = App.Settings.SkippedUpdateVersion;
        App.Settings.SkippedUpdateVersion = version.ToString();
        if (!App.Settings.TrySave())
        {
            App.Settings.SkippedUpdateVersion = previous;
            _updateError = "UpdateSkipFailed";
            RenderUpdateNotice();
            return;
        }

        _notifications.PurgeObsoleteUpdateNotifications(version);
        ClearPendingUpdate();
    }

    private void ClearPendingUpdate()
    {
        _activeUpdateResult = null;
        _updateError = null;
        _updateNotice?.Close();
        UpdateAvailableButtonState();
    }

    private Task RunUpdateActionAsync() => _updatePhase switch
    {
        UpdatePhase.Available => DownloadPendingUpdateAsync(),
        UpdatePhase.Ready => InstallPendingUpdateAsync(),
        _ => Task.CompletedTask
    };

    private async Task DownloadPendingUpdateAsync()
    {
        if (_activeUpdateResult is not { PackageError: null } release || _updateDownload is not null) return;
        using var download = CancellationTokenSource.CreateLinkedTokenSource(_updateLifetime.Token);
        _updateDownload = download;
        _updatePhase = UpdatePhase.Downloading;
        _updateProgress = default;
        _updateError = null;
        RenderUpdateNotice();
        var progress = new Progress<UpdateDownloadProgress>(value =>
        {
            if (!ReferenceEquals(_updateDownload, download) || download.IsCancellationRequested) return;
            _updateProgress = value;
            _updateNotice?.RenderProgress(value);
        });

        try
        {
            var destination = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CyberWall", "Updates", release.LatestVersion!.ToString(), release.AssetName!);
            await UpdateService.DownloadVerifiedAsync(release.DownloadUrl!, destination, release.AssetSha256, progress,
                download.Token);
            _verifiedPackage = destination;
            _updatePhase = UpdatePhase.Ready;
        }
        catch (OperationCanceledException) when (download.IsCancellationRequested)
        {
            _updatePhase = UpdatePhase.Available;
        }
        catch (Exception ex)
        {
            _updatePhase = UpdatePhase.Available;
            ReportUpdateError(ex, ex is InvalidDataException ? "UpdateVerifyFailed" : "UpdateDownloadFailed");
        }
        finally
        {
            _updateDownload = null;
        }

        if (_updatesClosed) return;
        if (_updateNotice is null && (_updatePhase == UpdatePhase.Ready || _updateError is not null))
            ShowUpdateNotice(activate: false);
        else
            RenderUpdateNotice();
    }

    private void CancelUpdateDownload()
    {
        _updateDownload?.Cancel();
        RenderUpdateNotice();
    }

    private async Task InstallPendingUpdateAsync()
    {
        if (_installRequested || _activeUpdateResult is not { } release || _verifiedPackage is not { } package) return;
        _installRequested = true;
        _updateError = null;
        RenderUpdateNotice();
        bool archive = Path.GetExtension(package).Equals(".zip", StringComparison.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(package) || !await UpdateService.VerifyDownloadAsync(package, release.AssetSha256,
                _updateLifetime.Token))
            {
                _verifiedPackage = null;
                _updatePhase = UpdatePhase.Available;
                _updateError = File.Exists(package) ? "UpdateVerifyFailed" : "UpdatePackageMissing";
                return;
            }

            if (_updatesClosed) return;
            if (archive)
                Process.Start(new ProcessStartInfo(Path.GetDirectoryName(package)!) { UseShellExecute = true })?.Dispose();
            else
                UpdateService.LaunchInstallerAndExit(package);
        }
        catch (OperationCanceledException) when (_updatesClosed) { }
        catch (Exception ex)
        {
            ReportUpdateError(ex, archive ? "UpdateOpenFolderFailed" : "UpdateInstallFailed");
        }
        finally
        {
            _installRequested = false;
            RenderUpdateNotice();
        }
    }

    private void ReportUpdateError(Exception exception, string messageKey)
    {
        Debug.WriteLine($"Update: {exception}");
        _updateError = messageKey;
        RenderUpdateNotice();
    }

    private void CloseUpdateFlow()
    {
        _updatesClosed = true;
        _updateLifetime.Cancel();
        _updateLifetime.Dispose();
        _updateNotice?.Close();
    }
}
