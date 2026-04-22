namespace Ckp.Core.Evidence;

/// <summary>
/// A reference to evidence supporting, contradicting, or constraining a CKP claim.
/// Can be a citation (PMID/DOI), a T0 axiom reference, or an internal cross-reference.
/// </summary>
/// <param name="Type">Whether this is a citation, axiom constraint, or internal reference.</param>
/// <param name="Ref">The reference identifier (e.g., "PMID:19834602", "T0:BIO.002", claim ID).</param>
/// <param name="Relationship">How this evidence relates to the claim.</param>
/// <param name="Strength">Evidence strength classification (null for axiom refs).</param>
/// <param name="Note">Optional human-readable annotation.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record EvidenceReference(
    EvidenceReferenceType Type,
    string Ref,
    EvidenceRelationship Relationship,
    EvidenceStrength? Strength,
    string? Note);
