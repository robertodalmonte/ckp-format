namespace Ckp.IO;

using System.IO.Compression;
using System.Text.Json;
using Ckp.Core;

/// <summary>
/// Reads a .ckp ZIP archive and hydrates the full <see cref="CkpPackage"/> domain aggregate.
/// </summary>
public sealed class CkpPackageReader : ICkpPackageReader
{
    public async Task<CkpPackage> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var options = CkpJsonOptions.Instance;

        var manifest = await ReadRequiredEntryAsync<PackageManifest>(archive, "manifest.json", options, cancellationToken);
        var claims = await ReadEntryAsync<List<PackageClaim>>(archive, "claims/claims.json", options, cancellationToken) ?? [];
        var citations = await ReadEntryAsync<List<CitationEntry>>(archive, "evidence/citations.json", options, cancellationToken) ?? [];
        var axiomRefs = await ReadEntryAsync<List<EvidenceReference>>(archive, "evidence/axiom-refs.json", options, cancellationToken) ?? [];
        var chapters = await ReadEntryAsync<List<ChapterInfo>>(archive, "structure/chapters.json", options, cancellationToken) ?? [];
        var domains = await ReadEntryAsync<List<DomainInfo>>(archive, "structure/domains.json", options, cancellationToken) ?? [];
        var glossary = await ReadEntryAsync<List<GlossaryEntry>>(archive, "structure/glossary.json", options, cancellationToken) ?? [];
        var editions = await ReadEntryAsync<List<EditionInfo>>(archive, "history/editions.json", options, cancellationToken) ?? [];

        var mechanisms = await ReadEntryAsync<List<MechanismEntry>>(archive, "enrichment/mechanisms.json", options, cancellationToken) ?? [];
        var phenomena = await ReadEntryAsync<List<PhenomenonEntry>>(archive, "enrichment/phenomena.json", options, cancellationToken) ?? [];
        var publisherCommentary = await ReadEntryAsync<List<CommentaryEntry>>(archive, "enrichment/commentary/publisher.json", options, cancellationToken) ?? [];
        var communityCommentary = await ReadEntryAsync<List<CommentaryEntry>>(archive, "enrichment/commentary/community.json", options, cancellationToken) ?? [];

        var alignments = new List<BookAlignment>();
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.StartsWith("alignment/external/", StringComparison.Ordinal)
                && entry.FullName.EndsWith(".json", StringComparison.Ordinal))
            {
                var alignment = await ReadEntryStreamAsync<BookAlignment>(entry, options, cancellationToken);
                if (alignment is not null)
                    alignments.Add(alignment);
            }
        }

        return new CkpPackage
        {
            Manifest = manifest,
            Claims = claims,
            Citations = citations,
            AxiomRefs = axiomRefs,
            Chapters = chapters,
            Domains = domains,
            Glossary = glossary,
            Editions = editions,
            Alignments = alignments,
            Mechanisms = mechanisms,
            Phenomena = phenomena,
            PublisherCommentary = publisherCommentary,
            CommunityCommentary = communityCommentary,
        };
    }

    private static async Task<T> ReadRequiredEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new InvalidOperationException($"Required entry '{entryName}' not found in .ckp archive.");
        return await ReadEntryStreamAsync<T>(entry, options, cancellationToken)
            ?? throw new InvalidOperationException($"Required entry '{entryName}' deserialized to null.");
    }

    private static async Task<T?> ReadEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
            return default;
        return await ReadEntryStreamAsync<T>(entry, options, cancellationToken);
    }

    private static async Task<T?> ReadEntryStreamAsync<T>(
        ZipArchiveEntry entry,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        await using var entryStream = entry.Open();
        return await JsonSerializer.DeserializeAsync<T>(entryStream, options, cancellationToken);
    }
}
