using System.IO;

namespace CyberWall.Common.Models;

public sealed record AppRule
{
    public required string AppPath { get; init; }
    public string DisplayName => AppIdentity.Resolve(AppPath).ProductName;
    public Verdict Verdict { get; init; } = Verdict.Block;
    public Direction Direction { get; init; } = Direction.Both;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? IconBase64 { get; init; }
    public string? PackageFamilyName { get; init; }

    public static string Normalize(string path) => Path.GetFullPath(path).ToLowerInvariant();
}
