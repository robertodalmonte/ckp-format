namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>traditions/*.json — multiple claims with shared evidence and tradition metadata.</summary>
/// <remarks>
/// JSON deserialization DTO, shape-locked to the KnowledgeBase layout. Internal
/// by B2; exposed to <c>Ckp.Tests</c> via <c>InternalsVisibleTo</c>.
/// </remarks>
internal sealed class TraditionFile
{
    [JsonPropertyName("claims")]
    public List<KbClaim> Claims { get; init; } = [];

    [JsonPropertyName("evidence")]
    public List<KbEvidence> Evidence { get; init; } = [];

    [JsonPropertyName("traditionSignatures")]
    public KbTraditionSignatures? TraditionSignatures { get; init; }
}
