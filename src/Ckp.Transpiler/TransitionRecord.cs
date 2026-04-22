namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>integrations/transitions.json entries.</summary>
/// <remarks>
/// JSON deserialization DTO, shape-locked to the KnowledgeBase layout. Transpiled
/// into <see cref="Ckp.Core.TierHistoryEntry"/> entries on claims. Internal by B2;
/// exposed to <c>Ckp.Tests</c> via <c>InternalsVisibleTo</c>.
/// </remarks>
internal sealed class TransitionRecord
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("claimId")]
    public string ClaimId { get; init; } = "";

    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("fromTier")]
    public int FromTier { get; init; }

    [JsonPropertyName("toTier")]
    public int ToTier { get; init; }

    [JsonPropertyName("justification")]
    public string? Justification { get; init; }

    [JsonPropertyName("evidenceSummary")]
    public string? EvidenceSummary { get; init; }

    [JsonPropertyName("transitionDate")]
    public string? TransitionDate { get; init; }
}
