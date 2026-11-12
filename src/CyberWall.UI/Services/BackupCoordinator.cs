using CyberWall.Common.Backups;
using CyberWall.Common.Settings;
using CyberWall.Service.Engine;

namespace CyberWall.UI.Services;

/// <summary>
/// Coordinates automatic scheduled/on-change backups, manual snapshots, and safety backups.
/// </summary>
public sealed class BackupCoordinator : IDisposable
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ChangeDebounce = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PeriodicCheckInterval = TimeSpan.FromHours(1);

    private readonly FirewallService _fw;
    private readonly AppSettings _settings;
    private readonly Func<string?> _versionProvider;
    private readonly object _gate = new();
    private readonly System.Threading.Timer _periodicTimer;
    private System.Threading.Timer? _debounceTimer;
    private bool _disposed;
    private bool _suppressSettingsEvent;

    public event Action<BackupCreateResult>? BackupCompleted;

    public BackupCoordinator(FirewallService fw, AppSettings settings, Func<string?>? versionProvider = null)
    {
        _fw = fw ?? throw new ArgumentNullException(nameof(fw));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _versionProvider = versionProvider ?? (() => UpdateService.GetCurrentVersionLabel());

        _fw.Store.Changed += OnRuleStoreChanged;
        AppSettings.Saved += OnSettingsSaved;

        _periodicTimer = new System.Threading.Timer(
            _ => TryRunAutoBackup(),
            null,
            StartupDelay,
            PeriodicCheckInterval);
    }

    public BackupCreateResult CreateManualBackup(string? explicitFilePath = null)
    {
        BackupCreateResult result;
        lock (_gate)
        {
            if (_disposed)
            {
                return new BackupCreateResult { Success = false, ErrorMessage = "Coordinator disposed." };
            }

            result = BackupService.CreateBackup(
                _fw.Store.All,
                _settings,
                BackupReason.Manual,
                _versionProvider(),
                explicitFilePath);
        }

        if (result.Success && !result.SkippedUnchanged)
        {
            BackupCompleted?.Invoke(result);
        }

        return result;
    }

    public BackupCreateResult CreateSafetyBackup(BackupReason reason)
    {
        BackupCreateResult result;
        lock (_gate)
        {
            if (_disposed)
            {
                return new BackupCreateResult { Success = false, ErrorMessage = "Coordinator disposed." };
            }

            result = BackupService.CreateBackup(
                _fw.Store.All,
                _settings,
                reason,
                _versionProvider());
        }

        if (result.Success && !result.SkippedUnchanged)
        {
            BackupCompleted?.Invoke(result);
        }

        return result;
    }

    public void TriggerAutoCheckSoon()
    {
        lock (_gate)
        {
            if (_disposed || !_settings.AutoBackupEnabled) return;
            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(
                _ => TryRunAutoBackup(),
                null,
                TimeSpan.FromSeconds(1),
                Timeout.InfiniteTimeSpan);
        }
    }

    private void OnRuleStoreChanged()
    {
        ScheduleDebounceIfOnChange();
    }

    private void OnSettingsSaved()
    {
        if (_suppressSettingsEvent) return;
        ScheduleDebounceIfOnChange();
    }

    private void ScheduleDebounceIfOnChange()
    {
        lock (_gate)
        {
            if (_disposed || !_settings.AutoBackupEnabled) return;
            if (_settings.AutoBackupFrequency != BackupFrequency.OnChange) return;

            _debounceTimer?.Dispose();
            _debounceTimer = new System.Threading.Timer(
                _ => TryRunAutoBackup(),
                null,
                ChangeDebounce,
                Timeout.InfiniteTimeSpan);
        }
    }

    private void TryRunAutoBackup()
    {
        BackupCreateResult? result = null;

        lock (_gate)
        {
            if (_disposed || !_settings.AutoBackupEnabled) return;

            result = BackupService.RunAutoBackupIfDue(
                _fw.Store.All,
                _settings,
                _versionProvider());

            if (result.Success && !result.SkippedUnchanged)
            {
                _suppressSettingsEvent = true;
                try
                {
                    _settings.Save();
                }
                finally
                {
                    _suppressSettingsEvent = false;
                }
            }
        }

        if (result is { Success: true, SkippedUnchanged: false })
        {
            BackupCompleted?.Invoke(result);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _fw.Store.Changed -= OnRuleStoreChanged;
            AppSettings.Saved -= OnSettingsSaved;
            _periodicTimer.Dispose();
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }
}

