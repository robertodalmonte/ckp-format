namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>integrations/bridges.json entries.</summary>
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
