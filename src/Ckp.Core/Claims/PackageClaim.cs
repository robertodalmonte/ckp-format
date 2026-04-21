namespace Ckp.Core.Claims;

/// <summary>
/// The atomic unit of the CKP format: a single falsifiable assertion made by a book,
/// with a tier assignment, evidence trail, and measurable observables. Content-addressable
/// via <see cref="Hash"/> — if the statement changes, the hash changes, and downstream
/// alignments break explicitly.
/// </summary>
/// <param name="Id">Book-scoped, human-readable identifier (e.g., "delta-14e.ANS.047").</param>
/// <param name="Statement">One falsifiable sentence. Not a paragraph, not a chapter summary.</param>
/// <param name="Tier">Current tier classification (T1–T4). T0 is never assigned by a book.</param>
/// <param name="Domain">Primary knowledge domain.</param>
/// <param name="SubDomain">More specific classification within the domain.</param>
/// <param name="Chapter">Chapter number in the source book.</param>
/// <param name="Section">Section title within the chapter.</param>
/// <param name="PageRange">Page range in the source book (e.g., "225-227").</param>
/// <param name="Keywords">Free-text search keywords.</param>
/// <param name="MeshTerms">MeSH descriptor identifiers for standard vocabulary mapping.</param>
/// <param name="Evidence">Citations, axiom constraints, and internal references.</param>
/// <param name="Observables">Measurable predictions — what you'd test to verify this claim.</param>
/// <param name="SinceEdition">The edition in which this claim first appeared.</param>
/// <param name="TierHistory">Edition-by-edition tier assignment trail.</param>
/// <param name="Hash">SHA-256 content hash of the statement for integrity verification.</param>
public sealed record PackageClaim(
    string Id,
    string Statement,
    Tier Tier,
    string Domain,
    string? SubDomain,
    int? Chapter,
    string? Section,
    string? PageRange,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<string> MeshTerms,
    IReadOnlyList<EvidenceReference> Evidence,
    IReadOnlyList<Observable> Observables,
    int? SinceEdition,
    IReadOnlyList<TierHistoryEntry> TierHistory,
    string Hash);
