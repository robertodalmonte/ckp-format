namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>observations/*.json — same shape as traditions but without tradition signatures.</summary>
internal sealed class ObservationFile
{
    [JsonPropertyName("claims")]
    public List<KbClaim> Claims { get; init; } = [];

    [JsonPropertyName("evidence")]
    public List<KbEvidence> Evidence { get; init; } = [];
}
