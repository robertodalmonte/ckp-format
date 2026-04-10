namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

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
