using System.IO;
namespace CyberWall.Common.Models;

public sealed record ConnectionEvent
{
    public required string AppPath { get; init; }
    public string AppName => Path.GetFileName(AppPath);
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
    public required string RemoteAddress { get; init; }
    public int RemotePort { get; init; }
    public Direction Direction { get; init; }
    public string Protocol { get; init; } = "TCP";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int ProcessId { get; init; }
}
