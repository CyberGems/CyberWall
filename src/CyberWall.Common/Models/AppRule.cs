using System.IO;
using System.Text.Json.Serialization;

namespace CyberWall.Common.Models;

public sealed record AppRule
{
    public required string AppPath { get; init; }
    public string DisplayName => AppIdentity.Resolve(AppPath).ProductName;
    public Verdict Verdict { get; init; } = Verdict.Block;
    public Direction Direction { get; init; } = Direction.Both;
    public Verdict? InboundVerdict { get; init; }
    public Verdict? OutboundVerdict { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string? IconBase64 { get; init; }
    public string? PackageFamilyName { get; init; }
    public string? LastKnownVersion { get; init; }
    public long? LastKnownFileSize { get; init; }
    public DateTime? LastKnownWriteTimeUtc { get; init; }

    [JsonIgnore]
    public Verdict EffectiveInboundVerdict => InboundVerdict ?? (Direction switch
    {
        Direction.Inbound => Verdict,
        Direction.Both => Verdict,
        _ => Verdict.Block
    });

    [JsonIgnore]
    public Verdict EffectiveOutboundVerdict => OutboundVerdict ?? (Direction switch
    {
        Direction.Outbound => Verdict,
        Direction.Both => Verdict,
        _ => Verdict.Block
    });

    public Verdict GetVerdictFor(Direction dir) => dir switch
    {
        Direction.Inbound => EffectiveInboundVerdict,
        Direction.Outbound => EffectiveOutboundVerdict,
        _ => (EffectiveInboundVerdict == Verdict.Allow || EffectiveOutboundVerdict == Verdict.Allow) ? Verdict.Allow : Verdict.Block
    };

    public static string Normalize(string path) => Path.GetFullPath(path).ToLowerInvariant();
}
