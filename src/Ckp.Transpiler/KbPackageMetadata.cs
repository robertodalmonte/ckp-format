namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>
/// Metadata read from package.json in the KnowledgeBase directory.
/// Provides the book identity for the output .ckp package.
/// </summary>
internal sealed class KbPackageMetadata
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = "";

    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("edition")]
    public int Edition { get; init; }

    [JsonPropertyName("authors")]
    public List<string> Authors { get; init; } = [];

    [JsonPropertyName("publisher")]
    public string Publisher { get; init; } = "";

    [JsonPropertyName("year")]
    public int Year { get; init; }

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";
}
