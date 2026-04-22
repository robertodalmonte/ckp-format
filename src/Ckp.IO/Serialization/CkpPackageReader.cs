namespace Ckp.IO;

using System.IO.Compression;
using System.Text.Json;
using Ckp.Core;

/// <summary>
/// Reads a .ckp ZIP archive and hydrates the full <see cref="CkpPackage"/> domain aggregate.
/// </summary>
public sealed class CkpPackageReader : ICkpPackageReader
{
    /// <summary>
    /// Versions of the CKP format this reader can hydrate. Readers reject any manifest
    /// whose <c>formatVersion</c> is outside this set per spec §15.4.
    /// </summary>
    public static readonly IReadOnlySet<string> SupportedFormatVersions =
        new HashSet<string>(StringComparer.Ordinal) { "1.0" };

    private const string AlignmentExternalPrefix = "alignment/external/";

    public async Task<CkpPackage> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var options = CkpJsonOptions.Instance;

        var manifest = await ReadRequiredEntryAsync<PackageManifest>(archive, "manifest.json", options, cancellationToken);

        // T3: reject unknown formatVersion. Spec §15.4 mandates this; earlier readers silently
        // accepted any string which made forward-compat ambiguous.
        if (!SupportedFormatVersions.Contains(manifest.FormatVersion))
        {
            throw new CkpFormatException(
                $"Unsupported formatVersion '{manifest.FormatVersion}'. Supported: [{string.Join(", ", SupportedFormatVersions)}].",
                entryName: "manifest.json");
        }

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
            if (!IsAlignmentEntry(entry.FullName)) continue;
            var alignment = await ReadEntryStreamAsync<BookAlignment>(entry, options, cancellationToken);
            if (alignment is not null)
                alignments.Add(alignment);
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

    /// <summary>
    /// Returns true iff the given entry full-name is a valid alignment/external/{book}.json
    /// entry. Normalizes <c>..</c> segments and rejects anything that escapes the prefix.
    /// <para>
    /// T3 — without normalization, an entry literally named
    /// <c>alignment/external/../../evil.json</c> passed the previous
    /// <c>StartsWith/EndsWith</c> filter. The reader never touched the filesystem so the
    /// exposure was bounded, but a crafted path could still smuggle data in and the spec
    /// forbids it. This guard is belt-and-braces.
    /// </para>
    /// </summary>
    internal static bool IsAlignmentEntry(string fullName)
    {
        if (!fullName.StartsWith(AlignmentExternalPrefix, StringComparison.Ordinal)) return false;
        if (!fullName.EndsWith(".json", StringComparison.Ordinal)) return false;

        // Normalize by splitting on '/' and folding '..'/'.'. If any segment backs out past
        // the alignment/external root, reject.
        var segments = fullName.Split('/');
        int depth = 0;
        int prefixDepth = AlignmentExternalPrefix.TrimEnd('/').Split('/').Length; // 2
        foreach (var seg in segments)
        {
            if (seg.Length == 0 || seg == ".") continue;
            if (seg == "..")
            {
                depth--;
                if (depth < 0) return false;
                continue;
            }
            depth++;
        }
        // Must still be strictly under alignment/external/ after normalization
        // (depth counts both prefix segments and the filename).
        return depth > prefixDepth;
    }

    private static async Task<T> ReadRequiredEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(entryName)
            ?? throw new CkpFormatException($"Required entry '{entryName}' not found in .ckp archive.", entryName);
        var value = await ReadEntryStreamAsync<T>(entry, options, cancellationToken, isRequired: true);
        return value ?? throw new CkpFormatException($"Required entry '{entryName}' deserialized to null.", entryName);
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
        return await ReadEntryStreamAsync<T>(entry, options, cancellationToken, isRequired: false);
    }

    private static async Task<T?> ReadEntryStreamAsync<T>(
        ZipArchiveEntry entry,
        JsonSerializerOptions options,
        CancellationToken cancellationToken,
        bool isRequired = false)
    {
        await using var entryStream = entry.Open();
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(entryStream, options, cancellationToken);
        }
        catch (JsonException ex) when (isRequired)
        {
            // T3: wrap System.Text.Json's raw error so callers can distinguish "the archive
            // structure is broken" from "some unrelated JsonException deep in a stack".
            throw new CkpFormatException(
                $"Required entry '{entry.FullName}' contains malformed JSON: {ex.Message}",
                entry.FullName,
                ex);
        }
    }
}
