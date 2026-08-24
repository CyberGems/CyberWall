using System.IO;
namespace CyberWall.Common.Models;

public sealed record ConnectionEvent
{
    public required string AppPath { get; init; }
    public string AppName => Path.GetFileName(AppPath);
    public required string RemoteAddress { get; init; }
    public int RemotePort { get; init; }
    public Direction Direction { get; init; }
    public string Protocol { get; init; } = "TCP";
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int ProcessId { get; init; }
}
