namespace Ckp.Core.Field;

/// <summary>
/// Epistemic lifecycle state of a canonical claim in a CKP 2.0 field package.
/// </summary>
public enum ClaimStatus
{
    /// <summary>Single attestation. No cross-book corroboration yet.</summary>
    Frontier = 0,

    /// <summary>Multiple attestations with general tier agreement.</summary>
    Converged = 1,

    /// <summary>Multiple attestations with explicit contradiction. Branches populated.</summary>
    Divergent = 2
}
