namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

internal sealed class KbEvidence
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("authors")]
    public string? Authors { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("year")]
    public int? Year { get; init; }

    [JsonPropertyName("journal")]
    public string? Journal { get; init; }

    [JsonPropertyName("pubMedId")]
    public string? PubMedId { get; init; }

    [JsonPropertyName("keyFindings")]
    public string? KeyFindings { get; init; }
}
