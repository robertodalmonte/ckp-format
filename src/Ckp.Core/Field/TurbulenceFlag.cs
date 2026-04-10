namespace Ckp.Core.Field;

/// <summary>
/// Epistemic signal indicating a recent authoritative source diverges from the
/// historical consensus on a canonical claim. Not a silent override, not a buried
/// dissent — an explicit flag that the field may be shifting.
/// </summary>
/// <param name="TriggeredByBookId">The book whose attestation caused the turbulence.</param>
/// <param name="Direction">Whether the divergence is a promotion, demotion, or contradiction.</param>
/// <param name="TierDelta">Absolute tier gap (e.g., T1→T3 = 2).</param>
/// <param name="Note">Human-readable explanation of the divergence.</param>
public sealed record TurbulenceFlag(
    string TriggeredByBookId,
    TurbulenceDirection Direction,
    int TierDelta,
    string Note);
