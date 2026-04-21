namespace Ckp.Core.Alignment;

/// <summary>
/// Summary of a cross-book alignment included in the package manifest.
/// </summary>
/// <param name="TargetBook">Book key for the aligned target (e.g., "gamma-2e").</param>
/// <param name="TargetPackageId">Package UUID of the target book.</param>
/// <param name="AlignedClaims">Number of claims with alignments to the target.</param>
/// <param name="TierMismatches">Number of aligned claims where tiers disagree.</param>
public sealed record AlignmentSummary(
    string TargetBook,
    string? TargetPackageId,
    int AlignedClaims,
    int TierMismatches);
