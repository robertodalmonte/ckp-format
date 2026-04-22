namespace Ckp.Core.Enrichment;

/// <summary>
/// A named mechanism linking related claims in the package.
/// Stored in enrichment/mechanisms.json.
/// </summary>
/// <param name="Name">Short mechanism name (e.g., "FAK-osteoclast cascade").</param>
/// <param name="Description">Human-readable summary of the mechanism.</param>
/// <param name="ClaimIds">IDs of claims that participate in this mechanism.</param>
/// <param name="PathwayTerms">Biological pathway keywords (e.g., "integrin-FAK", "Rho-ROCK signaling").</param>
/// <param name="PredictedMeasurements">Free-text descriptions of expected measurable outcomes.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record MechanismEntry(
    string Name,
    string Description,
    IReadOnlyList<string> ClaimIds,
    IReadOnlyList<string> PathwayTerms,
    IReadOnlyList<string> PredictedMeasurements);
