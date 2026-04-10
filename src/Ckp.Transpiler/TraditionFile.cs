namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>traditions/*.json — multiple claims with shared evidence and tradition metadata.</summary>
internal sealed class TraditionFile
{
    [JsonPropertyName("claims")]
    public List<KbClaim> Claims { get; init; } = [];

    [JsonPropertyName("evidence")]
    public List<KbEvidence> Evidence { get; init; } = [];

    [JsonPropertyName("traditionSignatures")]
    public KbTraditionSignatures? TraditionSignatures { get; init; }
}
