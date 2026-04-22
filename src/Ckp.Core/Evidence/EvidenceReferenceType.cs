namespace Ckp.Core.Evidence;

/// <summary>
/// Type of evidence reference in a CKP claim's evidence array.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public enum EvidenceReferenceType
{
    /// <summary>A bibliographic citation (PMID, DOI).</summary>
    Citation = 0,

    /// <summary>A T0 axiom constraint from the shared registry.</summary>
    Axiom = 1,

    /// <summary>An internal cross-reference to another claim in the same package.</summary>
    InternalRef = 2
}
