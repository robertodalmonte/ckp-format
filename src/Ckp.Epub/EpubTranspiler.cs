namespace Ckp.Epub;

using Ckp.Core;

/// <summary>
/// Transpiles an ePub file into a CKP skeleton package: book metadata, chapter structure,
/// and supplementary chapter text — but zero claims. Claims require downstream enrichment
/// (human annotation or LLM-assisted extraction).
/// </summary>
internal sealed class EpubTranspiler
{
    private readonly string _epubPath;
    private readonly BookMetadataArgs _metadata;

    public EpubTranspiler(string epubPath, BookMetadataArgs metadata)
    {
        _epubPath = epubPath;
        _metadata = metadata;
    }

    /// <summary>
    /// Extracted chapters, available after <see cref="TranspileAsync"/> completes.
    /// </summary>
    public ChapterText[] Chapters { get; private set; } = [];

    public async Task<CkpPackage> TranspileAsync(CancellationToken ct = default)
    {
        Chapters = await EpubExtractor.ExtractAsync(_epubPath);

        var chapterInfos = Chapters
            .Select(ch => new ChapterInfo(ch.ChapterNumber, ch.Title, ClaimCount: 0, Domains: []))
            .ToList();

        var book = new BookMetadata(
            Key: _metadata.Key,
            Title: _metadata.Title,
            Edition: _metadata.Edition,
            Authors: _metadata.Authors,
            Publisher: _metadata.Publisher,
            Year: _metadata.Year,
            Isbn: null,
            Language: "en",
            Domains: []);

        var fingerprint = new ContentFingerprint(
            Algorithm: "SHA-256",
            ClaimCount: 0,
            DomainCount: 0,
            T1Count: 0,
            T2Count: 0,
            T3Count: 0,
            T4Count: 0,
            CitationCount: 0);

        var manifest = PackageManifest.CreateNew(book, fingerprint);

        var edition = new EditionInfo(
            Edition: _metadata.Edition,
            Year: _metadata.Year,
            Isbn: null,
            Editor: null,
            Note: "Structure extracted from ePub");

        return new CkpPackage(
            Manifest: manifest,
            Claims: [],
            Citations: [],
            AxiomRefs: [],
            Chapters: chapterInfos,
            Domains: [],
            Glossary: [],
            Editions: [edition],
            Alignments: [],
            Mechanisms: [],
            Phenomena: [],
            PublisherCommentary: [],
            CommunityCommentary: []);
    }
}

/// <summary>
/// Book metadata collected from CLI arguments.
/// </summary>
internal sealed record BookMetadataArgs(
    string Key,
    string Title,
    int Edition,
    IReadOnlyList<string> Authors,
    string Publisher,
    int Year);
