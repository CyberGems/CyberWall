using System.Text.Json.Serialization;
using CyberWall.Common.Models;
using CyberWall.Common.Settings;

namespace CyberWall.Common.Backups;

public enum BackupReason
{
    Auto,
    Manual,
    PreClear,
    PreRestore
}

public sealed class BackupSummary
{
    public int TotalRules { get; set; }
    public int AllowedCount { get; set; }
    public int BlockedCount { get; set; }

    [JsonConverter(typeof(FirewallModeConverter))]
    public FirewallMode FirewallMode { get; set; } = FirewallMode.Ask;
}

public sealed class BackupPayload
{
    public List<AppRule> Rules { get; set; } = new();
    public AppSettings? Settings { get; set; }
}

public sealed class BackupDocument
{
    public int SchemaVersion { get; set; } = BackupService.CurrentSchemaVersion;
    public string AppVersion { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public BackupReason Reason { get; set; } = BackupReason.Manual;

    public string ContentSha256 { get; set; } = "";
    public BackupSummary Summary { get; set; } = new();
    public BackupPayload Payload { get; set; } = new();
}

public sealed class BackupEntryInfo
{
    public string FilePath { get; init; } = "";
    public string FileName { get; init; } = "";
    public DateTime CreatedUtc { get; init; }
    public BackupReason Reason { get; init; }
    public int TotalRules { get; init; }
    public int AllowedCount { get; init; }
    public int BlockedCount { get; init; }
    public FirewallMode FirewallMode { get; init; }
    public long SizeBytes { get; init; }
    public bool IsValid { get; init; }
    public bool HasRules { get; init; }
    public bool HasSettings { get; init; }
}

public sealed class BackupCreateResult
{
    public bool Success { get; init; }
    public bool SkippedUnchanged { get; init; }
    public string? FilePath { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed class BackupLoadResult
{
    public bool Success { get; init; }
    public string? ErrorKey { get; init; }
    public string? ErrorDetail { get; init; }
    public BackupDocument? Document { get; init; }
}

