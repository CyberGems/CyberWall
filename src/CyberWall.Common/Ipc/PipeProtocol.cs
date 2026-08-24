using CyberWall.Common.Models;

namespace CyberWall.Common.Ipc;

public static class PipeProtocol
{
    public const string PipeName = "CyberWall_Engine";
}

public sealed record IpcMessage
{
    public string Type { get; init; } = "";
    public string PayloadJson { get; init; } = "";
}

public static class IpcTypes
{
    public const string ConnectionAsk = "ask";
    public const string VerdictReply = "verdict";
    public const string RulesSync = "rules";
    public const string ModeChange = "mode";
    public const string Ping = "ping";
}

public sealed record VerdictReply
{
    public required string AppPath { get; init; }
    public required Verdict Verdict { get; init; }
    public bool Permanent { get; init; }
}
