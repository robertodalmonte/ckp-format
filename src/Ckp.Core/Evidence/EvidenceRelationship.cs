namespace Ckp.Core.Evidence;

/// <summary>
/// How an evidence reference relates to the claim it is attached to.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public enum EvidenceRelationship
{
    /// <summary>Evidence supports the claim.</summary>
    Supports = 0,

    /// <summary>Evidence contradicts the claim.</summary>
    Contradicts = 1,

    /// <summary>Claim is constrained by a T0 axiom.</summary>
    ConstrainedBy = 2
}
