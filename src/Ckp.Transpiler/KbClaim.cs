namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>
/// JSON deserialization DTO matching the Consilience KnowledgeBase claim schema.
/// </summary>
/// <remarks>
/// Not part of the CKP wire format — transpiled into <see cref="Ckp.Core.PackageClaim"/>
/// and discarded. Internal by B2; exposed to <c>Ckp.Tests</c> via
/// <c>InternalsVisibleTo</c>.
/// </remarks>
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
