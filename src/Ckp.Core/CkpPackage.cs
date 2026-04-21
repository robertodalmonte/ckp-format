namespace Ckp.Core;

/// <summary>
/// The fully hydrated in-memory representation of a .ckp (Consilience Knowledge Package).
/// Aggregates all sections that live inside the ZIP archive: manifest, claims, evidence,
/// structure, history, glossary, and alignments.
/// </summary>
/// <remarks>
/// Construct with object-initializer syntax. Only <see cref="Manifest"/> is required; all
/// collections default to empty, so tests and minimal writers can do
/// <c>new CkpPackage { Manifest = m }</c> without supplying thirteen empty lists.
/// </remarks>
public sealed record CkpPackage
{
    /// <summary>Package manifest with book metadata and fingerprint.</summary>
    public required PackageManifest Manifest { get; init; }

    /// <summary>All claims in the package.</summary>
    public IReadOnlyList<PackageClaim> Claims { get; init; } = [];

    /// <summary>All bibliographic citations referenced by claims.</summary>
    public IReadOnlyList<CitationEntry> Citations { get; init; } = [];

    /// <summary>T0 axiom references used as constraints by claims.</summary>
    public IReadOnlyList<EvidenceReference> AxiomRefs { get; init; } = [];

    /// <summary>Chapter index.</summary>
    public IReadOnlyList<ChapterInfo> Chapters { get; init; } = [];

    /// <summary>Domain taxonomy.</summary>
    public IReadOnlyList<DomainInfo> Domains { get; init; } = [];

    /// <summary>Vocabulary entries with cross-book mappings.</summary>
    public IReadOnlyList<GlossaryEntry> Glossary { get; init; } = [];

    /// <summary>Edition history metadata.</summary>
    public IReadOnlyList<EditionInfo> Editions { get; init; } = [];

    /// <summary>Cross-book claim alignments.</summary>
    public IReadOnlyList<BookAlignment> Alignments { get; init; } = [];

    /// <summary>Named mechanisms linking related claims.</summary>
    public IReadOnlyList<MechanismEntry> Mechanisms { get; init; } = [];

    /// <summary>Named phenomena clustering claims across domains.</summary>
    public IReadOnlyList<PhenomenonEntry> Phenomena { get; init; } = [];

    /// <summary>Publisher annotations on claims.</summary>
    public IReadOnlyList<CommentaryEntry> PublisherCommentary { get; init; } = [];

    /// <summary>Community annotations on claims.</summary>
    public IReadOnlyList<CommentaryEntry> CommunityCommentary { get; init; } = [];

    /// <summary>Creates an empty package containing only the manifest.</summary>
    public static CkpPackage Empty(PackageManifest manifest) => new() { Manifest = manifest };
}
