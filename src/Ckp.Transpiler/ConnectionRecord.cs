namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>integrations/connections.json entries.</summary>
/// <remarks>
/// JSON deserialization DTO, shape-locked to the Consilience KnowledgeBase file
/// layout. Internal by B2 — not part of the CKP wire format. Exposed to
/// <c>Ckp.Tests</c> via <c>InternalsVisibleTo</c> for unit-test construction.
/// </remarks>
internal sealed class ConnectionRecord
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("sourceClaimId")]
    public string SourceClaimId { get; init; } = "";

    [JsonPropertyName("targetClaimId")]
    public string TargetClaimId { get; init; } = "";

    [JsonPropertyName("relationship")]
    public string? Relationship { get; init; }
}
