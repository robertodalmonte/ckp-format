namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>mechanisms/*.json — single claim with evidence and signatures.</summary>
internal sealed class MechanismFile
{
    [JsonPropertyName("claim")]
    public KbClaim Claim { get; init; } = null!;

    [JsonPropertyName("evidence")]
    public List<KbEvidence> Evidence { get; init; } = [];

    [JsonPropertyName("signatures")]
    public KbSignatures? Signatures { get; init; }
}
