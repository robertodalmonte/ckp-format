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

    public Task<CkpPackage> ReadAsync(Stream stream, CancellationToken cancellationToken = default) =>
        ReadAsync(stream, CkpReadOptions.Default, cancellationToken);

    public async Task<CkpPackage> ReadAsync(Stream stream, CkpReadOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);

        if (options.VerifySignature && options.SignatureVerifier is null)
        {
            throw new InvalidOperationException(
                "CkpReadOptions.VerifySignature is true but SignatureVerifier is null. " +
                "Wire in a delegate that calls Ckp.Signing.CkpSigner.VerifyManifest.");
        }

        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        var jsonOptions = CkpJsonOptions.Instance;

        var manifest = await ReadRequiredEntryAsync<PackageManifest>(archive, "manifest.json", jsonOptions, cancellationToken);

        // T3: reject unknown formatVersion. Spec §15.4 mandates this; earlier readers silently
        // accepted any string which made forward-compat ambiguous.
        if (!SupportedFormatVersions.Contains(manifest.FormatVersion))
        {
            throw new CkpFormatException(
                $"Unsupported formatVersion '{manifest.FormatVersion}'. Supported: [{string.Join(", ", SupportedFormatVersions)}].",
                entryName: "manifest.json");
        }

        var claims = await ReadEntryAsync<List<PackageClaim>>(archive, "claims/claims.json", jsonOptions, cancellationToken) ?? [];
        var citations = await ReadEntryAsync<List<CitationEntry>>(archive, "evidence/citations.json", jsonOptions, cancellationToken) ?? [];
        var axiomRefs = await ReadEntryAsync<List<EvidenceReference>>(archive, "evidence/axiom-refs.json", jsonOptions, cancellationToken) ?? [];
        var chapters = await ReadEntryAsync<List<ChapterInfo>>(archive, "structure/chapters.json", jsonOptions, cancellationToken) ?? [];
        var domains = await ReadEntryAsync<List<DomainInfo>>(archive, "structure/domains.json", jsonOptions, cancellationToken) ?? [];
        var glossary = await ReadEntryAsync<List<GlossaryEntry>>(archive, "structure/glossary.json", jsonOptions, cancellationToken) ?? [];
        var editions = await ReadEntryAsync<List<EditionInfo>>(archive, "history/editions.json", jsonOptions, cancellationToken) ?? [];

        var mechanisms = await ReadEntryAsync<List<MechanismEntry>>(archive, "enrichment/mechanisms.json", jsonOptions, cancellationToken) ?? [];
        var phenomena = await ReadEntryAsync<List<PhenomenonEntry>>(archive, "enrichment/phenomena.json", jsonOptions, cancellationToken) ?? [];
        var publisherCommentary = await ReadEntryAsync<List<CommentaryEntry>>(archive, "enrichment/commentary/publisher.json", jsonOptions, cancellationToken) ?? [];
        var communityCommentary = await ReadEntryAsync<List<CommentaryEntry>>(archive, "enrichment/commentary/community.json", jsonOptions, cancellationToken) ?? [];

        var alignments = new List<BookAlignment>();
        foreach (var entry in archive.Entries)
        {
            if (!IsAlignmentEntry(entry.FullName)) continue;
            var alignment = await ReadEntryStreamAsync<BookAlignment>(entry, jsonOptions, cancellationToken);
            if (alignment is not null)
                alignments.Add(alignment);
        }

        var package = new CkpPackage
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

        EnforceStrictOptions(package, options, cancellationToken);
        if (options.RequireContentHash || options.VerifySignature)
            await EnforceContentAndSignatureAsync(package, options, cancellationToken);

        return package;
    }

    /// <summary>
    /// Applies the options that can be checked directly against the hydrated manifest,
    /// before any further work (signature verification, hash recomputation). Keeps the
    /// cheap checks up front so a strict reader rejects obviously-wrong input fast.
    /// </summary>
    private static void EnforceStrictOptions(CkpPackage package, CkpReadOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (options.RequireSignature && package.Manifest.Signature is null)
        {
            throw new CkpFormatException(
                "Strict-read mode required a signature but the manifest has none " +
                "(T-DOWNGRADE-UNSIGNED; see SigningThreatModel.md §3).",
                entryName: "manifest.json");
        }

        if (options.ExpectedPublicKey is not null)
        {
            var actual = package.Manifest.Signature?.PublicKey;
            if (!string.Equals(actual, options.ExpectedPublicKey, StringComparison.Ordinal))
            {
                throw new CkpFormatException(
                    "Strict-read mode pinned a public key that does not match the signature's " +
                    "(T-FORGE-KEY; see SigningThreatModel.md §3).",
                    entryName: "manifest.json");
            }
        }
    }

    private static async Task EnforceContentAndSignatureAsync(
        CkpPackage package, CkpReadOptions options, CancellationToken cancellationToken)
    {
        if (options.RequireContentHash)
        {
            var stored = package.Manifest.ContentFingerprint.Hash;
            if (stored is null)
            {
                throw new CkpFormatException(
                    "Strict-read mode required a content hash but ContentFingerprint.Hash is null. " +
                    "Package was written by a pre-S1 writer.",
                    entryName: "manifest.json");
            }

            var computed = await CkpContentHash.ComputeForPackageAsync(package, cancellationToken);
            if (!string.Equals(stored, computed, StringComparison.Ordinal))
            {
                throw new CkpFormatException(
                    $"Strict-read mode content-hash mismatch: manifest says '{stored}', archive body hashes to '{computed}' " +
                    "(T-BYTE / T-ADD; see SigningThreatModel.md §3).",
                    entryName: "manifest.json");
            }
        }

        if (options.VerifySignature)
        {
            // Verifier is guaranteed non-null by the precondition check at entry.
            if (package.Manifest.Signature is null)
            {
                // VerifySignature without RequireSignature on an unsigned package is a caller error.
                throw new CkpFormatException(
                    "Strict-read mode requested signature verification but the manifest has no signature. " +
                    "Set RequireSignature = true to reject unsigned packages, or do not verify unsigned input.",
                    entryName: "manifest.json");
            }

            if (!options.SignatureVerifier!(package.Manifest))
            {
                throw new CkpFormatException(
                    "Strict-read mode signature verification failed " +
                    "(T-BYTE manifest-scope / T-FORGE-KEY / T-DOWNGRADE-ALGORITHM; see SigningThreatModel.md §3).",
                    entryName: "manifest.json");
            }
        }
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
