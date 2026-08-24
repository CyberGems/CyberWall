using System.IO;
namespace CyberWall.Common.Models;

public sealed record AppRule
{
    public required string AppPath { get; init; }
    public string DisplayName
    {
        get
        {
            var fn = Path.GetFileName(AppPath);
            if (fn.Equals("git-remote-https.exe", StringComparison.OrdinalIgnoreCase) ||
                fn.Equals("git-remote-http.exe", StringComparison.OrdinalIgnoreCase))
            {
                return $"Git ({fn})";
            }
            return fn;
        }
    }
    public Verdict Verdict { get; init; } = Verdict.Block;
    public Direction Direction { get; init; } = Direction.Both;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? IconBase64 { get; init; }

    public static string Normalize(string path) => Path.GetFullPath(path).ToLowerInvariant();
}
