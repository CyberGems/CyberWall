using System.IO;
using System.Text.Json;
using CyberWall.Common.Backups;
using CyberWall.Common.I18n;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;

namespace CyberWall.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly string _tempDir;

    public BackupServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"CyberWall_BackupTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    private static List<AppRule> SampleRules(int count = 1)
    {
        var list = new List<AppRule>();
        for (int i = 0; i < count; i++)
        {
            list.Add(new AppRule
            {
                AppPath = $@"C:\Apps\Sample{i}.exe",
                LastKnownVersion = $"1.0.{i}",
                OutboundVerdict = i % 2 == 0 ? Verdict.Allow : Verdict.Block,
                InboundVerdict = Verdict.Allow,
                CreatedAt = new DateTime(2026, 1, 1, 0, i, 0, DateTimeKind.Utc)
            });
        }
        return list;
    }

    [Fact]
    public void BackupRetentionCount_IsClampedBetweenOneAndSeven_AndDefaultsToThree()
    {
        var s = new AppSettings();
        Assert.Equal(3, s.BackupRetentionCount);
        Assert.Equal(1, AppSettings.MinBackupRetention);
        Assert.Equal(7, AppSettings.MaxBackupRetention);

        s.BackupRetentionCount = 0;
        Assert.Equal(1, s.BackupRetentionCount);

        s.BackupRetentionCount = -10;
        Assert.Equal(1, s.BackupRetentionCount);

        s.BackupRetentionCount = 30;
        Assert.Equal(7, s.BackupRetentionCount);

        s.BackupRetentionCount = 5;
        Assert.Equal(5, s.BackupRetentionCount);
    }

    [Fact]
    public void AutoBackup_DeduplicatesTransientChanges_AndCreatesBackupWhenRealStateChanges()
    {
        var settings = new AppSettings
        {
            BackupDirectory = _tempDir,
            AutoBackupEnabled = true,
            AutoBackupFrequency = BackupFrequency.OnChange,
            BackupRetentionCount = 3
        };
        var rules = SampleRules(2);
        var t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

        var first = BackupService.RunAutoBackupIfDue(rules, settings, "2.4.0", nowUtc: t0);
        Assert.True(first.Success);
        Assert.False(first.SkippedUnchanged);
        Assert.NotNull(first.FilePath);

        // Changing window geometry or temporary alert pause should NOT trigger a duplicate snapshot.
        settings.MainWindowWidth = 1440;
        settings.MainWindowHeight = 900;
        settings.FloatingAlertsPausedUntilUtc = t0.AddMinutes(30);

        var transientCheck = BackupService.RunAutoBackupIfDue(rules, settings, "2.4.0", nowUtc: t0.AddMinutes(5));
        Assert.True(transientCheck.Success);
        Assert.True(transientCheck.SkippedUnchanged);

        // Adding a rule IS a meaningful change and creates a second snapshot in OnChange mode.
        rules.Add(new AppRule
        {
            AppPath = @"C:\Apps\NewApp.exe",
            LastKnownVersion = "2.0.0",
            OutboundVerdict = Verdict.Block
        });

        var changed = BackupService.RunAutoBackupIfDue(rules, settings, "2.4.0", nowUtc: t0.AddMinutes(6));
        Assert.True(changed.Success);
        Assert.False(changed.SkippedUnchanged);
        Assert.NotEqual(first.FilePath, changed.FilePath);
    }

    [Fact]
    public void AutoBackup_RotationStrictlyKeepsLimit_AndPreservesManualAndUnrelatedFiles()
    {
        var settings = new AppSettings
        {
            BackupDirectory = _tempDir,
            AutoBackupEnabled = true,
            AutoBackupFrequency = BackupFrequency.OnChange,
            BackupRetentionCount = 3
        };

        var unrelatedTxt = Path.Combine(_tempDir, "important-notes.txt");
        File.WriteAllText(unrelatedTxt, "keep me intact");

        var t0 = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);
        var manual = BackupService.CreateBackup(SampleRules(1), settings, BackupReason.Manual, "2.4.0", nowUtc: t0);
        Assert.True(manual.Success);
        Assert.True(File.Exists(manual.FilePath));

        for (int i = 1; i <= 6; i++)
        {
            var res = BackupService.RunAutoBackupIfDue(
                SampleRules(i),
                settings,
                "2.4.0",
                nowUtc: t0.AddMinutes(i));
            Assert.True(res.Success);
            Assert.False(res.SkippedUnchanged);
        }

        var autoFiles = Directory.GetFiles(_tempDir, "cyberwall-auto-*.cwbackup");
        Assert.Equal(3, autoFiles.Length);
        Assert.True(File.Exists(manual.FilePath));
        Assert.True(File.Exists(unrelatedTxt));
    }

    [Fact]
    public void SafetyBackup_IsCappedAtThreeSnapshots()
    {
        var settings = new AppSettings { BackupDirectory = _tempDir };
        var t0 = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
        {
            var reason = i % 2 == 0 ? BackupReason.PreClear : BackupReason.PreRestore;
            var res = BackupService.CreateBackup(SampleRules(i + 1), settings, reason, "2.4.0", nowUtc: t0.AddSeconds(i * 10));
            Assert.True(res.Success);
        }

        var safetyFiles = Directory.GetFiles(_tempDir, "cyberwall-safety-*.cwbackup");
        Assert.Equal(BackupService.MaxSafetySnapshots, safetyFiles.Length);
    }

    [Fact]
    public void LoadAndVerify_DetectsTamperedPayload_WithChecksumMismatch()
    {
        var settings = new AppSettings { BackupDirectory = _tempDir };
        var res = BackupService.CreateBackup(SampleRules(2), settings, BackupReason.Manual, "2.4.0");
        Assert.True(res.Success);
        Assert.NotNull(res.FilePath);

        var ok = BackupService.LoadAndVerify(res.FilePath!);
        Assert.True(ok.Success);
        Assert.NotNull(ok.Document);
        Assert.Equal(2, ok.Document!.Payload.Rules.Count);

        // Tamper with payload without recomputing ContentSha256
        var raw = File.ReadAllText(res.FilePath!);
        var tampered = raw.Replace("Sample0.exe", "Tampered.exe", StringComparison.Ordinal);
        Assert.NotEqual(raw, tampered);
        File.WriteAllText(res.FilePath!, tampered);

        var failed = BackupService.LoadAndVerify(res.FilePath!);
        Assert.False(failed.Success);
        Assert.Equal("BackupErrChecksumMismatch", failed.ErrorKey);
    }

    [Fact]
    public void ValidateWritableDirectory_CreatesNestedFolder_AndRejectsEmpty()
    {
        var nested = Path.Combine(_tempDir, "nested", "backups");
        Assert.True(BackupService.ValidateWritableDirectory(nested, out var err1));
        Assert.Null(err1);
        Assert.True(Directory.Exists(nested));

        Assert.False(BackupService.ValidateWritableDirectory("   ", out var err2));
        Assert.False(string.IsNullOrWhiteSpace(err2));
    }

    [Fact]
    public void ApplyRestoredPreferences_RestoresUserChoices_WithoutClobberingWindowGeometry()
    {
        var live = new AppSettings
        {
            Language = Lang.En,
            Theme = AppTheme.CyberWall,
            MainWindowWidth = 1333,
            MainWindowHeight = 888
        };

        var snapshot = new AppSettings
        {
            Language = Lang.Es,
            Theme = AppTheme.Light,
            FirewallEnabled = true,
            FirewallMode = FirewallMode.BlockAll,
            BackupDirectory = _tempDir,
            AutoBackupFrequency = BackupFrequency.Weekly,
            MainWindowWidth = 800,
            MainWindowHeight = 600
        };
        // Simulate JSON deserializing out-of-bounds value via serializer
        var json = JsonSerializer.Serialize(snapshot).Replace("\"BackupRetentionCount\":3", "\"BackupRetentionCount\":99");
        Assert.Contains("\"BackupRetentionCount\":99", json);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json)!;

        live.ApplyRestoredPreferences(deserialized);

        Assert.Equal(Lang.Es, live.Language);
        Assert.Equal(AppTheme.Light, live.Theme);
        Assert.Equal(FirewallMode.BlockAll, live.FirewallMode);
        Assert.Equal(BackupFrequency.Weekly, live.AutoBackupFrequency);
        Assert.Equal(_tempDir, live.BackupDirectory);
        Assert.Equal(7, live.BackupRetentionCount);
        Assert.Equal(1333, live.MainWindowWidth);
        Assert.Equal(888, live.MainWindowHeight);
    }
}

