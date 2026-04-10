namespace Ckp.Core;

/// <summary>
/// A named phenomenon clustering related claims across domains.
/// Stored in enrichment/phenomena.json.
/// </summary>
/// <param name="Name">Phenomenon name (e.g., "trigeminal-autonomic coupling").</param>
/// <param name="Description">Human-readable summary.</param>
/// <param name="ClaimIds">IDs of claims in this cluster.</param>
/// <param name="SharedConcept">The concept that ties the claims together.</param>
public sealed record PhenomenonEntry(
    string Name,
    string Description,
    IReadOnlyList<string> ClaimIds,
    string? SharedConcept);
