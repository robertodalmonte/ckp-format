namespace Ckp.Core.Claims;

/// <summary>
/// Epistemic classification assigned by a book to a claim.
/// Numeric values are the canonical ordering: T1 (consensus) → T4 (ancient observation).
/// <para>
/// Serialized as the exact string form <c>"T1"</c>..<c>"T4"</c> via a
/// <see cref="System.Text.Json.Serialization.JsonStringEnumConverter{Tier}"/> configured
/// with no naming policy — the spec form is preserved byte-for-byte.
/// </para>
/// </summary>
public enum Tier
{
    T1 = 1,
    T2 = 2,
    T3 = 3,
    T4 = 4
}
