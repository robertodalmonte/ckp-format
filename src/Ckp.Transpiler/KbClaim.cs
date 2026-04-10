namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>
/// JSON DTOs matching the Consilience KnowledgeBase file schemas.
/// </summary>

internal sealed class KbClaim
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("identifierDomain")]
    public int IdentifierDomain { get; init; }

    [JsonPropertyName("identifierSequence")]
    public int IdentifierSequence { get; init; }

    [JsonPropertyName("statement")]
    public string Statement { get; init; } = "";

    [JsonPropertyName("tier")]
    public int Tier { get; init; }

    [JsonPropertyName("domain")]
    public int Domain { get; init; }

    [JsonPropertyName("proposedMechanism")]
    public string? ProposedMechanism { get; init; }

    [JsonPropertyName("falsificationCriteria")]
    public string? FalsificationCriteria { get; init; }
}
