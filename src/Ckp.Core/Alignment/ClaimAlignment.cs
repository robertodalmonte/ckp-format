namespace Ckp.Core.Alignment;

/// <summary>
/// Maps a claim in one book to a claim in another — same phenomenon, different vocabulary,
/// possibly different tier. Parallel to ALiveBook's SentenceAlignment but for knowledge claims.
/// </summary>
/// <param name="SourceClaim">Claim ID in the source book (e.g., "delta-14e.ANS.047").</param>
/// <param name="TargetClaim">Claim ID in the target book, or null if unmatched.</param>
/// <param name="Type">How the two claims relate.</param>
/// <param name="Confidence">Alignment confidence score (0.0–1.0).</param>
/// <param name="Mismatch">Tier mismatch details, or null if tiers agree or claim is unmatched.</param>
/// <param name="Bridge">Vocabulary mapping between the two books' terminology.</param>
/// <param name="AlignedBy">Who or what produced this alignment (e.g., "consilience-auto").</param>
/// <param name="ReviewedBy">Human reviewer, or null if unreviewed.</param>
/// <param name="Note">Optional annotation, especially for unmatched alignments.</param>
public sealed record ClaimAlignment(
    string SourceClaim,
    string? TargetClaim,
    AlignmentType Type,
    double? Confidence,
    TierMismatch? Mismatch,
    VocabularyBridge? Bridge,
    string? AlignedBy,
    string? ReviewedBy,
    string? Note);
