namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>mechanisms/*.json — single claim with evidence and signatures.</summary>
/// <remarks>
/// JSON deserialization DTO, shape-locked to the KnowledgeBase layout. Internal
/// by B2; exposed to <c>Ckp.Tests</c> via <c>InternalsVisibleTo</c>.
/// </remarks>
internal sealed class MechanismFile
{
    [JsonPropertyName("claim")]
    public KbClaim Claim { get; init; } = null!;

    [JsonPropertyName("evidence")]
    public List<KbEvidence> Evidence { get; init; } = [];

    [JsonPropertyName("signatures")]
    public KbSignatures? Signatures { get; init; }
}
