namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

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
