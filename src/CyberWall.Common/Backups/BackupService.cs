using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CyberWall.Common.IO;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;

namespace CyberWall.Common.Backups;

public static partial class BackupService
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxSafetySnapshots = 3;
    public const string FileExtension = ".cwbackup";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        WriteIndented = false
    };

    [GeneratedRegex(@"^cyberwall-auto-\d{8}-\d{6}(?:-\d+)?\.cwbackup$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AutoBackupNameRegex();

    [GeneratedRegex(@"^cyberwall-safety-\d{8}-\d{6}(?:-\d+)?\.cwbackup$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SafetyBackupNameRegex();

    public static string DefaultBackupDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CyberWall", "backups");

    public static string GetEffectiveDirectory(AppSettings settings)
    {
        if (settings != null && !string.IsNullOrWhiteSpace(settings.BackupDirectory))
        {
            try
            {
                return Path.GetFullPath(settings.BackupDirectory.Trim());
            }
            catch
            {
                return DefaultBackupDirectory;
            }
        }
        return DefaultBackupDirectory;
    }

    public static bool ValidateWritableDirectory(string path, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Path cannot be empty.";
            return false;
        }

        try
        {
            var full = Path.GetFullPath(path.Trim());
            Directory.CreateDirectory(full);
            var probe = Path.Combine(full, $".cw-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    public static bool IsAutoBackupDue(AppSettings settings, string currentDedupHash, DateTime? nowUtc = null, string? overrideDirectory = null)
    {
        if (!settings.AutoBackupEnabled) return false;

        var now = nowUtc ?? DateTime.UtcNow;
        var dir = overrideDirectory ?? GetEffectiveDirectory(settings);

        // If the destination folder currently has no auto backup file, create one right away
        // as long as the interval / change rules apply or no prior backup exists there.
        bool hasAnyAutoFileInDir = false;
        try
        {
            if (Directory.Exists(dir))
            {
                hasAnyAutoFileInDir = Directory.EnumerateFiles(dir, $"*{FileExtension}", SearchOption.TopDirectoryOnly)
                    .Any(p => AutoBackupNameRegex().IsMatch(Path.GetFileName(p)));
            }
        }
        catch { }

        if (!hasAnyAutoFileInDir) return true;
        if (settings.LastAutoBackupUtc is not DateTime lastUtc) return true;

        var utcLast = lastUtc.Kind switch
        {
            DateTimeKind.Utc => lastUtc,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(lastUtc, DateTimeKind.Utc),
            _ => lastUtc.ToUniversalTime()
        };

        var elapsed = now - utcLast;
        return settings.AutoBackupFrequency switch
        {
            BackupFrequency.OnChange => !string.Equals(settings.LastAutoBackupHash, currentDedupHash, StringComparison.Ordinal),
            BackupFrequency.Weekly => elapsed >= TimeSpan.FromDays(7)
                && !string.Equals(settings.LastAutoBackupHash, currentDedupHash, StringComparison.Ordinal),
            _ => elapsed >= TimeSpan.FromHours(24)
                && !string.Equals(settings.LastAutoBackupHash, currentDedupHash, StringComparison.Ordinal)
        };
    }

    public static BackupCreateResult RunAutoBackupIfDue(
        IEnumerable<AppRule> rules,
        AppSettings settings,
        string? appVersion = null,
        string? overrideDirectory = null,
        DateTime? nowUtc = null)
    {
        var ruleList = NormalizeRulesSnapshot(rules);
        var dedupHash = ComputeDeduplicationHash(ruleList, settings);
        var now = nowUtc ?? DateTime.UtcNow;

        if (!IsAutoBackupDue(settings, dedupHash, now, overrideDirectory))
        {
            return new BackupCreateResult { Success = true, SkippedUnchanged = true };
        }

        var result = CreateBackup(ruleList, settings, BackupReason.Auto, appVersion, explicitFilePath: null, overrideDirectory, now);
        if (result.Success)
        {
            settings.LastAutoBackupUtc = now;
            settings.LastAutoBackupHash = dedupHash;
        }
        return result;
    }

    public static BackupCreateResult CreateBackup(
        IEnumerable<AppRule> rules,
        AppSettings settings,
        BackupReason reason,
        string? appVersion = null,
        string? explicitFilePath = null,
        string? overrideDirectory = null,
        DateTime? nowUtc = null)
    {
        try
        {
            var now = nowUtc ?? DateTime.UtcNow;
            var dir = overrideDirectory ?? GetEffectiveDirectory(settings);
            Directory.CreateDirectory(dir);

            var sortedRules = NormalizeRulesSnapshot(rules);
            var settingsSnapshot = BuildPersistentSettingsSnapshot(settings, includeSessionGeometry: true);

            var payload = new BackupPayload
            {
                Rules = sortedRules,
                Settings = settingsSnapshot
            };

            var payloadJson = JsonSerializer.Serialize(payload, CompactJsonOptions);
            using var payloadDoc = JsonDocument.Parse(payloadJson);
            var contentHash = ComputeCanonicalElementSha256(payloadDoc.RootElement);

            int allowed = sortedRules.Count(r => r.EffectiveOutboundVerdict == Verdict.Allow || r.EffectiveInboundVerdict == Verdict.Allow);
            int blocked = sortedRules.Count - allowed;

            var doc = new BackupDocument
            {
                SchemaVersion = CurrentSchemaVersion,
                AppVersion = string.IsNullOrWhiteSpace(appVersion) ? GetDefaultAppVersion() : appVersion,
                CreatedUtc = now,
                Reason = reason,
                ContentSha256 = contentHash,
                Summary = new BackupSummary
                {
                    TotalRules = sortedRules.Count,
                    AllowedCount = allowed,
                    BlockedCount = blocked,
                    FirewallMode = settings.FirewallMode
                },
                Payload = payload
            };

            var targetPath = explicitFilePath ?? BuildUniqueFilePath(dir, reason, now.ToLocalTime());
            var serialized = JsonSerializer.Serialize(doc, JsonOptions);
            AtomicFile.WriteAllText(targetPath, serialized);

            if (explicitFilePath == null)
            {
                if (reason == BackupReason.Auto)
                {
                    PruneMatchingBackups(dir, AutoBackupNameRegex(), settings.BackupRetentionCount);
                }
                else if (reason is BackupReason.PreClear or BackupReason.PreRestore)
                {
                    PruneMatchingBackups(dir, SafetyBackupNameRegex(), MaxSafetySnapshots);
                }
            }

            return new BackupCreateResult
            {
                Success = true,
                FilePath = targetPath
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BackupService: backup failed: {ex.Message}");
            return new BackupCreateResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public static BackupLoadResult LoadAndVerify(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return new BackupLoadResult { Success = false, ErrorKey = "BackupErrNotFound" };
        }

        try
        {
            var rawJson = File.ReadAllText(filePath, Encoding.UTF8);
            using var jsonDoc = JsonDocument.Parse(rawJson);
            var root = jsonDoc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return new BackupLoadResult { Success = false, ErrorKey = "BackupErrInvalidFormat" };

            if (!TryGetPropertyIgnoreCase(root, "schemaVersion", out var verEl) || !verEl.TryGetInt32(out int schemaVer) || schemaVer < 1 || schemaVer > CurrentSchemaVersion)
                return new BackupLoadResult { Success = false, ErrorKey = "BackupErrUnsupportedVersion" };

            if (!TryGetPropertyIgnoreCase(root, "contentSha256", out var hashEl) || hashEl.ValueKind != JsonValueKind.String)
                return new BackupLoadResult { Success = false, ErrorKey = "BackupErrChecksumMismatch" };

            if (!TryGetPropertyIgnoreCase(root, "payload", out var payloadEl) || payloadEl.ValueKind != JsonValueKind.Object)
                return new BackupLoadResult { Success = false, ErrorKey = "BackupErrInvalidFormat" };

            var storedHash = hashEl.GetString() ?? "";
            var computedHash = ComputeCanonicalElementSha256(payloadEl);
            if (!string.Equals(storedHash, computedHash, StringComparison.OrdinalIgnoreCase))
                return new BackupLoadResult { Success = false, ErrorKey = "BackupErrChecksumMismatch" };

            var doc = JsonSerializer.Deserialize<BackupDocument>(rawJson, JsonOptions);
            if (doc?.Payload == null || (doc.Payload.Rules == null && doc.Payload.Settings == null))
                return new BackupLoadResult { Success = false, ErrorKey = "BackupErrInvalidFormat" };

            doc.Payload.Rules ??= new List<AppRule>();
            doc.Payload.Rules = doc.Payload.Rules
                .Where(r => r != null && !string.IsNullOrWhiteSpace(r.AppPath))
                .ToList();

            return new BackupLoadResult
            {
                Success = true,
                Document = doc
            };
        }
        catch (JsonException ex)
        {
            return new BackupLoadResult { Success = false, ErrorKey = "BackupErrInvalidFormat", ErrorDetail = ex.Message };
        }
        catch (Exception ex)
        {
            return new BackupLoadResult { Success = false, ErrorKey = "BackupErrReadFailed", ErrorDetail = ex.Message };
        }
    }

    public static List<BackupEntryInfo> ListBackups(AppSettings settings, string? overrideDirectory = null)
    {
        var dir = overrideDirectory ?? GetEffectiveDirectory(settings);
        var list = new List<BackupEntryInfo>();
        if (!Directory.Exists(dir)) return list;

        foreach (var path in Directory.EnumerateFiles(dir, $"*{FileExtension}", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var fi = new FileInfo(path);
                var loaded = LoadAndVerify(path);
                if (loaded.Success && loaded.Document != null)
                {
                    var doc = loaded.Document;
                    list.Add(new BackupEntryInfo
                    {
                        FilePath = path,
                        FileName = fi.Name,
                        CreatedUtc = doc.CreatedUtc,
                        Reason = doc.Reason,
                        TotalRules = doc.Summary?.TotalRules ?? doc.Payload.Rules.Count,
                        AllowedCount = doc.Summary?.AllowedCount ?? doc.Payload.Rules.Count(r => r.EffectiveOutboundVerdict == Verdict.Allow || r.EffectiveInboundVerdict == Verdict.Allow),
                        BlockedCount = doc.Summary?.BlockedCount ?? 0,
                        FirewallMode = doc.Summary?.FirewallMode ?? doc.Payload.Settings?.FirewallMode ?? FirewallMode.Ask,
                        SizeBytes = fi.Length,
                        IsValid = true,
                        HasRules = doc.Payload.Rules != null,
                        HasSettings = doc.Payload.Settings != null
                    });
                }
                else
                {
                    list.Add(new BackupEntryInfo
                    {
                        FilePath = path,
                        FileName = fi.Name,
                        CreatedUtc = fi.LastWriteTimeUtc,
                        Reason = BackupReason.Manual,
                        SizeBytes = fi.Length,
                        IsValid = false
                    });
                }
            }
            catch { }
        }

        return list.OrderByDescending(b => b.CreatedUtc).ThenByDescending(b => b.FileName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static bool DeleteBackup(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string ComputeDeduplicationHash(IEnumerable<AppRule> rules, AppSettings settings)
    {
        var normalizedRules = NormalizeRulesSnapshot(rules);
        var persistentSettings = BuildPersistentSettingsSnapshot(settings, includeSessionGeometry: false);
        var payload = new BackupPayload
        {
            Rules = normalizedRules,
            Settings = persistentSettings
        };
        var json = JsonSerializer.Serialize(payload, CompactJsonOptions);
        using var doc = JsonDocument.Parse(json);
        return ComputeCanonicalElementSha256(doc.RootElement);
    }

    internal static void PruneMatchingBackups(string directory, Regex pattern, int keepCount)
    {
        if (!Directory.Exists(directory)) return;
        int keep = Math.Clamp(keepCount, 1, AppSettings.MaxBackupRetention);

        var matching = Directory.EnumerateFiles(directory, $"*{FileExtension}", SearchOption.TopDirectoryOnly)
            .Where(p => pattern.IsMatch(Path.GetFileName(p)))
            .OrderByDescending(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        for (int i = keep; i < matching.Count; i++)
        {
            try { File.Delete(matching[i]); } catch { }
        }
    }

    private static List<AppRule> NormalizeRulesSnapshot(IEnumerable<AppRule> rules)
    {
        return rules
            .Where(r => r != null && !string.IsNullOrWhiteSpace(r.AppPath))
            .OrderBy(r => AppRule.Normalize(r.AppPath), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AppSettings BuildPersistentSettingsSnapshot(AppSettings source, bool includeSessionGeometry)
    {
        var raw = JsonSerializer.Serialize(source, CompactJsonOptions);
        var clone = JsonSerializer.Deserialize<AppSettings>(raw) ?? new AppSettings();
        clone.LastAutoBackupUtc = null;
        clone.LastAutoBackupHash = null;
        clone.PolicyRestoreFailed = false;

        if (!includeSessionGeometry)
        {
            clone.FloatingAlertsPausedPermanent = false;
            clone.FloatingAlertsPausedUntilUtc = null;
            clone.RecentSearches = new List<string>();
            clone.MainWindowBoundsSaved = false;
            clone.MainWindowMonitor = "";
            clone.MainWindowLeft = 0;
            clone.MainWindowTop = 0;
            clone.MainWindowWidth = 0;
            clone.MainWindowHeight = 0;
            clone.MainWindowMaximized = false;
            clone.TrafficWidgetBoundsSaved = false;
            clone.TrafficWidgetLeft = 0;
            clone.TrafficWidgetTop = 0;
        }

        return clone;
    }

    private static string BuildUniqueFilePath(string dir, BackupReason reason, DateTime localNow)
    {
        var prefix = reason switch
        {
            BackupReason.Auto => "cyberwall-auto",
            BackupReason.PreClear or BackupReason.PreRestore => "cyberwall-safety",
            _ => "cyberwall-manual"
        };

        var stamp = localNow.ToString("yyyyMMdd-HHmmss");
        var candidate = Path.Combine(dir, $"{prefix}-{stamp}{FileExtension}");
        int suffix = 2;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(dir, $"{prefix}-{stamp}-{suffix}{FileExtension}");
            suffix++;
        }
        return candidate;
    }

    internal static string ComputeCanonicalElementSha256(JsonElement element)
    {
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms))
        {
            WriteCanonicalElement(writer, element);
        }
        var hash = SHA256.HashData(ms.ToArray());
        return Convert.ToHexStringLower(hash);
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteCanonicalElement(writer, prop.Value);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalElement(writer, item);
                }
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = prop.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string GetDefaultAppVersion()
    {
        var v = Assembly.GetEntryAssembly()?.GetName().Version;
        return v != null ? $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}" : "2.4.0";
    }
}

