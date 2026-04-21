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

        // Collect every entry first, emit in sorted order so ZIP central directory is stable.
        var entries = new List<(string Name, Func<Task<byte[]>> Bytes)>
        {
            ("manifest.json", () => Task.FromResult(CkpCanonicalJson.Serialize(package.Manifest))),
            ("claims/claims.json", () => SerializeAsync(package.Claims, options, cancellationToken)),
            ("evidence/citations.json", () => SerializeAsync(package.Citations, options, cancellationToken)),
            ("evidence/axiom-refs.json", () => SerializeAsync(package.AxiomRefs, options, cancellationToken)),
            ("structure/chapters.json", () => SerializeAsync(package.Chapters, options, cancellationToken)),
            ("structure/domains.json", () => SerializeAsync(package.Domains, options, cancellationToken)),
            ("structure/glossary.json", () => SerializeAsync(package.Glossary, options, cancellationToken)),
            ("history/editions.json", () => SerializeAsync(package.Editions, options, cancellationToken)),
            ("history/tier-changes.json", () =>
            {
                var tierChanges = package.Claims
                    .Where(c => c.TierHistory.Count > 0)
                    .OrderBy(c => c.Id, StringComparer.Ordinal)
                    .Select(c => new { claimId = c.Id, history = c.TierHistory })
                    .ToList();
                return SerializeAsync(tierChanges, options, cancellationToken);
            })
        };

        if (package.Mechanisms.Count > 0)
            entries.Add(("enrichment/mechanisms.json", () => SerializeAsync(package.Mechanisms, options, cancellationToken)));
        if (package.Phenomena.Count > 0)
            entries.Add(("enrichment/phenomena.json", () => SerializeAsync(package.Phenomena, options, cancellationToken)));
        if (package.PublisherCommentary.Count > 0)
            entries.Add(("enrichment/commentary/publisher.json", () => SerializeAsync(package.PublisherCommentary, options, cancellationToken)));
        if (package.CommunityCommentary.Count > 0)
            entries.Add(("enrichment/commentary/community.json", () => SerializeAsync(package.CommunityCommentary, options, cancellationToken)));

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
