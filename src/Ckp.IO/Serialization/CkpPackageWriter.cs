namespace Ckp.IO;

using System.IO.Compression;
using System.Text.Json;
using Ckp.Core;

/// <summary>
/// Writes a <see cref="CkpPackage"/> to a .ckp ZIP archive following the directory layout
/// defined in the CKP format specification.
/// <para>
/// Output is byte-deterministic: entries are added in a fixed sorted order, each entry's
/// <see cref="ZipArchiveEntry.LastWriteTime"/> is pinned to <see cref="DeterministicEpoch"/>,
/// and the manifest is canonicalized via <see cref="CkpCanonicalJson"/>. Identical input
/// packages therefore produce byte-identical archives, which is required for content
/// addressing and Ed25519-signature stability.
/// </para>
/// </summary>
public sealed class CkpPackageWriter : ICkpPackageWriter
{
    /// <summary>
    /// Fixed timestamp stamped onto every ZIP entry. Matches the epoch used by Nuke,
    /// Gradle, and the reproducible-builds project for deterministic ZIP output.
    /// </summary>
    internal static readonly DateTimeOffset DeterministicEpoch =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public async Task WriteAsync(CkpPackage package, Stream stream, CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var options = CkpJsonOptions.Instance;

        // T5 — sort every top-level list by its natural key before serializing, so input
        // insertion order does not leak into the output bytes. Alignments were already
        // sorted; claims, citations, axiom-refs, chapters, domains, glossary, editions,
        // and the four enrichment lists were not.
        var claims = package.Claims.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        var citations = package.Citations.OrderBy(c => c.Ref, StringComparer.Ordinal).ToList();
        var axiomRefs = package.AxiomRefs.OrderBy(r => r.Ref, StringComparer.Ordinal).ToList();
        var chapters = package.Chapters.OrderBy(c => c.Number).ThenBy(c => c.Title, StringComparer.Ordinal).ToList();
        var domains = package.Domains.OrderBy(d => d.Name, StringComparer.Ordinal).ToList();
        var glossary = package.Glossary.OrderBy(g => g.BookTerm, StringComparer.Ordinal).ToList();
        var editions = package.Editions.OrderBy(e => e.Edition).ToList();
        var mechanisms = package.Mechanisms.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        var phenomena = package.Phenomena.OrderBy(p => p.Name, StringComparer.Ordinal).ToList();
        var publisherCommentary = package.PublisherCommentary
            .OrderBy(c => c.ClaimId, StringComparer.Ordinal)
            .ThenBy(c => c.CreatedAt)
            .ThenBy(c => c.Author, StringComparer.Ordinal)
            .ToList();
        var communityCommentary = package.CommunityCommentary
            .OrderBy(c => c.ClaimId, StringComparer.Ordinal)
            .ThenBy(c => c.CreatedAt)
            .ThenBy(c => c.Author, StringComparer.Ordinal)
            .ToList();

        // Collect every entry first, emit in sorted order so ZIP central directory is stable.
        var entries = new List<(string Name, Func<Task<byte[]>> Bytes)>
        {
            ("manifest.json", () => Task.FromResult(CkpCanonicalJson.Serialize(package.Manifest))),
            ("claims/claims.json", () => SerializeAsync(claims, options, cancellationToken)),
            ("evidence/citations.json", () => SerializeAsync(citations, options, cancellationToken)),
            ("evidence/axiom-refs.json", () => SerializeAsync(axiomRefs, options, cancellationToken)),
            ("structure/chapters.json", () => SerializeAsync(chapters, options, cancellationToken)),
            ("structure/domains.json", () => SerializeAsync(domains, options, cancellationToken)),
            ("structure/glossary.json", () => SerializeAsync(glossary, options, cancellationToken)),
            ("history/editions.json", () => SerializeAsync(editions, options, cancellationToken)),
            ("history/tier-changes.json", () =>
            {
                var tierChanges = claims
                    .Where(c => c.TierHistory.Count > 0)
                    .Select(c => new { claimId = c.Id, history = c.TierHistory })
                    .ToList();
                return SerializeAsync(tierChanges, options, cancellationToken);
            })
        };

        if (mechanisms.Count > 0)
            entries.Add(("enrichment/mechanisms.json", () => SerializeAsync(mechanisms, options, cancellationToken)));
        if (phenomena.Count > 0)
            entries.Add(("enrichment/phenomena.json", () => SerializeAsync(phenomena, options, cancellationToken)));
        if (publisherCommentary.Count > 0)
            entries.Add(("enrichment/commentary/publisher.json", () => SerializeAsync(publisherCommentary, options, cancellationToken)));
        if (communityCommentary.Count > 0)
            entries.Add(("enrichment/commentary/community.json", () => SerializeAsync(communityCommentary, options, cancellationToken)));

        foreach (var alignment in package.Alignments.OrderBy(a => a.TargetBook, StringComparer.Ordinal))
        {
            var captured = alignment;
            entries.Add(($"alignment/external/{captured.TargetBook}.json",
                () => SerializeAsync(captured, options, cancellationToken)));
        }

        foreach (var (name, bytesFactory) in entries.OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            byte[] bytes = await bytesFactory();
            var zipEntry = archive.CreateEntry(name, CompressionLevel.Optimal);
            zipEntry.LastWriteTime = DeterministicEpoch;
            await using var entryStream = zipEntry.Open();
            await entryStream.WriteAsync(bytes, cancellationToken);
        }
    }

    private static async Task<byte[]> SerializeAsync<T>(
        T value, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, value, options, cancellationToken);
        return ms.ToArray();
    }

}
