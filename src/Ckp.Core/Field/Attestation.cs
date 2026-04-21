namespace Ckp.Core.Field;

/// <summary>
/// Provenance linkage from a canonical claim back to a specific claim in a CKP 1.0 package.
/// Carries all inputs to the weight formula so the audit trail is self-contained — any
/// reviewer can recompute the weight and its decomposition from this record alone.
/// </summary>
/// <param name="BookId">Source book key (e.g., "alpha-3e").</param>
/// <param name="ClaimId">Original claim ID in the 1.0 package (e.g., "alpha-3e.BIO.007").</param>
/// <param name="Tier">Tier assigned by this book.</param>
/// <param name="PublicationYear">Publication year of this book edition.</param>
/// <param name="EditionsSurvived">Number of editions this claim has survived in the source book.</param>
/// <param name="BaseAuthority">Base authority assigned to the source book (0.0–1.0).</param>
/// <param name="Weight">Computed weight: BaseAuthority × (1 + α·ln(editions)) × e^(-λ·age).
/// This is the authoritative value used by both the confidence score and turbulence
/// detector — they never recompute it from the other fields.</param>
/// <param name="Note">Optional context for the alignment (e.g., "same pathway in fascial fibroblasts").</param>
public sealed record Attestation(
    string BookId,
    string ClaimId,
    Tier Tier,
    int PublicationYear,
    int EditionsSurvived,
    double BaseAuthority,
    double Weight,
    string? Note);
