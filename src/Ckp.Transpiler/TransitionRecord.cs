namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>integrations/transitions.json entries.</summary>
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
