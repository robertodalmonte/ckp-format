namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>integrations/bridges.json entries.</summary>
/// <remarks>
/// JSON deserialization DTO, shape-locked to the Consilience KnowledgeBase file
/// layout. Internal by B2 — the shape of the legacy KB JSON is not part of any
/// CKP-level contract, and exposing it across the assembly boundary would pin
/// us to a private file format. Exposed to <c>Ckp.Tests</c> via
/// <c>InternalsVisibleTo</c> for construction in unit tests only.
/// </remarks>
internal sealed class BridgeRecord
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("ancientObservationClaimId")]
    public string AncientObservationClaimId { get; init; } = "";

    [JsonPropertyName("modernMechanismClaimId")]
    public string ModernMechanismClaimId { get; init; } = "";

    [JsonPropertyName("quantumFrontierClaimId")]
    public string? QuantumFrontierClaimId { get; init; }

    [JsonPropertyName("classicalMechanismSufficient")]
    public bool ClassicalMechanismSufficient { get; init; }

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }
}
