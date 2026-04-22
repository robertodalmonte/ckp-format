namespace Ckp.Transpiler;

using System.Text.Json.Serialization;

/// <summary>observations/*.json — same shape as traditions but without tradition signatures.</summary>
/// <remarks>
/// JSON deserialization DTO, shape-locked to the KnowledgeBase layout. Internal
/// by B2; exposed to <c>Ckp.Tests</c> via <c>InternalsVisibleTo</c>.
/// </remarks>
internal sealed class ObservationFile
{
    [JsonPropertyName("claims")]
    public List<KbClaim> Claims { get; init; } = [];

    [JsonPropertyName("evidence")]
    public List<KbEvidence> Evidence { get; init; } = [];
}
