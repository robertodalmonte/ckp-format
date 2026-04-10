namespace Ckp.Core;

/// <summary>
/// How an evidence reference relates to the claim it is attached to.
/// </summary>
public enum EvidenceRelationship
{
    /// <summary>Evidence supports the claim.</summary>
    Supports = 0,

    /// <summary>Evidence contradicts the claim.</summary>
    Contradicts = 1,

    /// <summary>Claim is constrained by a T0 axiom.</summary>
    ConstrainedBy = 2
}
