namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>
/// JSON deserialization DTO for a KnowledgeBase evidence entry.
/// </summary>
/// <remarks>
/// Not part of the CKP wire format — transpiled into
/// <see cref="Ckp.Core.CitationEntry"/> (and friends). Internal by B2; exposed
/// to <c>Ckp.Tests</c> via <c>InternalsVisibleTo</c>.
/// </remarks>
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
