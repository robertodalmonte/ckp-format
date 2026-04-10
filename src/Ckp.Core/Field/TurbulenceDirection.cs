namespace Ckp.Core.Field;

/// <summary>
/// Direction of an epistemic turbulence event triggered by a recent authoritative
/// source diverging from historical consensus.
/// </summary>
public enum TurbulenceDirection
{
    /// <summary>New source promotes the claim to a higher tier than consensus.</summary>
    Promotion = 0,

    /// <summary>New source demotes the claim to a lower tier than consensus.</summary>
    Demotion = 1,

    /// <summary>New source directly contradicts the claim's conclusion.</summary>
    Contradiction = 2
}
