namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>
/// Signatures block for a T4 tradition file: the tradition's domain, name, and
/// the search/physiological/practice terms used by the alignment proposer.
/// </summary>
/// <remarks>
/// JSON deserialization DTO; not part of the CKP wire format. Internal by B2;
/// exposed to <c>Ckp.Tests</c> via <c>InternalsVisibleTo</c>.
/// </remarks>
internal sealed class KbTraditionSignatures
{
    [JsonPropertyName("domain")]
    public int Domain { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("searchKeywords")]
    public List<string>? SearchKeywords { get; init; }

    [JsonPropertyName("physiologicalTerms")]
    public List<string>? PhysiologicalTerms { get; init; }

    [JsonPropertyName("practiceTerms")]
    public List<string>? PracticeTerms { get; init; }
}
