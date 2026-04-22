namespace Ckp.Core.Alignment;

/// <summary>
/// Records that two aligned claims have different tier assignments — the richest signal
/// in the system. A claim at T1 in three books and T3 in one is either behind or knows
/// something the others don't.
/// </summary>
/// <param name="SourceTier">Tier in the source book.</param>
/// <param name="TargetTier">Tier in the target book.</param>
/// <param name="Direction">Which book considers the claim more established.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record TierMismatch(
    Tier SourceTier,
    Tier TargetTier,
    TierMismatchDirection Direction);
