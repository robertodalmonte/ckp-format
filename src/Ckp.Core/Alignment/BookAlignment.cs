namespace Ckp.Core.Alignment;

/// <summary>
/// The full alignment file between two books: a source book, a target book,
/// and the list of individual claim alignments.
/// </summary>
/// <param name="SourceBook">Book key for the source (e.g., "delta-14e").</param>
/// <param name="TargetBook">Book key for the target (e.g., "gamma-2e").</param>
/// <param name="Alignments">Individual claim-level alignments.</param>
public sealed record BookAlignment(
    string SourceBook,
    string TargetBook,
    IReadOnlyList<ClaimAlignment> Alignments);
