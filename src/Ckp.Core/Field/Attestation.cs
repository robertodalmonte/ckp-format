namespace Ckp.Core.Field;

/// <summary>
/// Provenance linkage from a canonical claim back to a specific claim in a CKP 1.0 package.
/// Carries the computed weight from the confidence score formula.
/// </summary>
/// <param name="BookId">Source book key (e.g., "alpha-3e").</param>
/// <param name="ClaimId">Original claim ID in the 1.0 package (e.g., "alpha-3e.BIO.007").</param>
/// <param name="Tier">Tier assigned by this book.</param>
/// <param name="PublicationYear">Publication year of this book edition.</param>
/// <param name="EditionsSurvived">Number of editions this claim has survived in the source book.</param>
/// <param name="Weight">Computed weight: Authority × (1 + α·ln(editions)) × e^(-λ·age).</param>
/// <param name="Note">Optional context for the alignment (e.g., "same pathway in fascial fibroblasts").</param>
public sealed record Attestation(
    string BookId,
    string ClaimId,
    string Tier,
    int PublicationYear,
    int EditionsSurvived,
    double Weight,
    string? Note);
