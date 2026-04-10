namespace Ckp.Core;

/// <summary>
/// The fully hydrated in-memory representation of a .ckp (Consilience Knowledge Package).
/// Aggregates all sections that live inside the ZIP archive: manifest, claims, evidence,
/// structure, history, glossary, and alignments.
/// </summary>
/// <param name="Manifest">Package manifest with book metadata and fingerprint.</param>
/// <param name="Claims">All claims in the package.</param>
/// <param name="Citations">All bibliographic citations referenced by claims.</param>
/// <param name="AxiomRefs">T0 axiom references used as constraints by claims.</param>
/// <param name="Chapters">Chapter index.</param>
/// <param name="Domains">Domain taxonomy.</param>
/// <param name="Glossary">Vocabulary entries with cross-book mappings.</param>
/// <param name="Editions">Edition history metadata.</param>
/// <param name="Alignments">Cross-book claim alignments.</param>
/// <param name="Mechanisms">Named mechanisms linking related claims.</param>
/// <param name="Phenomena">Named phenomena clustering claims across domains.</param>
/// <param name="PublisherCommentary">Publisher annotations on claims.</param>
/// <param name="CommunityCommentary">Community annotations on claims.</param>
public sealed record CkpPackage(
    PackageManifest Manifest,
    IReadOnlyList<PackageClaim> Claims,
    IReadOnlyList<CitationEntry> Citations,
    IReadOnlyList<EvidenceReference> AxiomRefs,
    IReadOnlyList<ChapterInfo> Chapters,
    IReadOnlyList<DomainInfo> Domains,
    IReadOnlyList<GlossaryEntry> Glossary,
    IReadOnlyList<EditionInfo> Editions,
    IReadOnlyList<BookAlignment> Alignments,
    IReadOnlyList<MechanismEntry> Mechanisms,
    IReadOnlyList<PhenomenonEntry> Phenomena,
    IReadOnlyList<CommentaryEntry> PublisherCommentary,
    IReadOnlyList<CommentaryEntry> CommunityCommentary);
