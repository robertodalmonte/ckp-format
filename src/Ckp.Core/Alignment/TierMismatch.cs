namespace Ckp.Core;

/// <summary>
/// Records that two aligned claims have different tier assignments — the richest signal
/// in the system. A claim at T1 in three books and T3 in one is either behind or knows
/// something the others don't.
/// </summary>
/// <param name="SourceTier">Tier in the source book.</param>
/// <param name="TargetTier">Tier in the target book.</param>
/// <param name="Direction">Which book considers the claim more established.</param>
public sealed record TierMismatch(
    string SourceTier,
    string TargetTier,
    TierMismatchDirection Direction);
