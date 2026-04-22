namespace Ckp.Core.Field;

/// <summary>
/// Direction of an epistemic turbulence event triggered by a recent authoritative
/// source diverging from historical consensus.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public enum TurbulenceDirection
{
    /// <summary>New source promotes the claim to a higher tier than consensus.</summary>
    Promotion = 0,

    /// <summary>New source demotes the claim to a lower tier than consensus.</summary>
    Demotion = 1,

    /// <summary>New source directly contradicts the claim's conclusion.</summary>
    Contradiction = 2
}
