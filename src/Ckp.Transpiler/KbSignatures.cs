namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>
/// Optional signatures block attached to a KnowledgeBase mechanism file: search
/// keywords, pathway/MeSH-like terms, predicted measurements, functional
/// observables. Used by the alignment proposer after transpile.
/// </summary>
/// <remarks>
/// JSON deserialization DTO; not part of the CKP wire format (the contents are
/// flattened into <see cref="Ckp.Core.PackageClaim"/> fields). Internal by B2;
/// exposed to <c>Ckp.Tests</c> via <c>InternalsVisibleTo</c>.
/// </remarks>
internal sealed class KbSignatures
{
    [JsonPropertyName("observables")]
    public List<string>? Observables { get; init; }

    [JsonPropertyName("pathwayTerms")]
    public List<string>? PathwayTerms { get; init; }

    [JsonPropertyName("predictedMeasurements")]
    public List<string>? PredictedMeasurements { get; init; }

    [JsonPropertyName("functionalObservables")]
    public List<string>? FunctionalObservables { get; init; }
}
