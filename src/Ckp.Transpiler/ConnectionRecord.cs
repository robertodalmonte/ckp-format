namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>integrations/connections.json entries.</summary>
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
