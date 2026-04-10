namespace Ckp.Core.Field;

/// <summary>
/// A proposed alignment between two claims from different CKP 1.0 packages.
/// Produced by the Alignment Proposer; consumed by the Field Package Compiler.
/// High-confidence proposals auto-merge; low-confidence ones go to human review.
/// </summary>
/// <param name="SourceClaimId">Claim ID in the source package.</param>
/// <param name="TargetClaimId">Claim ID in the target package.</param>
/// <param name="Score">Alignment confidence (0.0–1.0).</param>
/// <param name="Reason">Human-readable explanation of why these claims align.</param>
/// <param name="ProposedCanonicalId">Suggested canonical URN for the merged claim.</param>
/// <param name="IsContradiction">True if the claims reach opposite conclusions.</param>
public sealed record AlignmentProposal(
    string SourceClaimId,
    string TargetClaimId,
    double Score,
    string Reason,
    string ProposedCanonicalId,
    bool IsContradiction);
