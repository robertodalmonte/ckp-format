namespace Ckp.IO;

using System.Text.Json;
using Ckp.Core;

/// <summary>
/// Shared deterministic serialization of the non-manifest portion of a
/// <see cref="CkpPackage"/>. Used by <see cref="CkpPackageWriter"/> to produce archive
/// bytes and by <see cref="CkpContentHash"/> to compute the content hash that lands
/// inside the manifest. Both callers must see the exact same bytes for the same
/// package — otherwise a writer/hash mismatch would be unverifiable by readers.
/// </summary>
internal static class PackageEntrySerializer
{
    /// <summary>
    /// Serializes every non-manifest entry into a stable, unsorted list of
    /// (name, bytes) tuples. Callers are expected to sort ordinally before
    /// writing or hashing. Input ordering inside <paramref name="package"/>
    /// does not affect the output bytes because each list is sorted by its
    /// natural key before serialization (claims by Id, citations by Ref, etc.).
    /// </summary>
    public static async Task<List<(string Name, byte[] Bytes)>> SerializeAsync(
        CkpPackage package, CancellationToken cancellationToken)
    {
        var options = CkpJsonOptions.Instance;

        // T5 — per-list natural-key sort so caller insertion order cannot leak.
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

        var entries = new List<(string Name, byte[] Bytes)>
        {
            ("claims/claims.json", await SerializeToBytesAsync(claims, options, cancellationToken)),
            ("evidence/citations.json", await SerializeToBytesAsync(citations, options, cancellationToken)),
            ("evidence/axiom-refs.json", await SerializeToBytesAsync(axiomRefs, options, cancellationToken)),
            ("structure/chapters.json", await SerializeToBytesAsync(chapters, options, cancellationToken)),
            ("structure/domains.json", await SerializeToBytesAsync(domains, options, cancellationToken)),
            ("structure/glossary.json", await SerializeToBytesAsync(glossary, options, cancellationToken)),
            ("history/editions.json", await SerializeToBytesAsync(editions, options, cancellationToken)),
        };

        var tierChanges = claims
            .Where(c => c.TierHistory.Count > 0)
            .Select(c => new { claimId = c.Id, history = c.TierHistory })
            .ToList();
        entries.Add(("history/tier-changes.json", await SerializeToBytesAsync(tierChanges, options, cancellationToken)));

        if (mechanisms.Count > 0)
            entries.Add(("enrichment/mechanisms.json", await SerializeToBytesAsync(mechanisms, options, cancellationToken)));
        if (phenomena.Count > 0)
            entries.Add(("enrichment/phenomena.json", await SerializeToBytesAsync(phenomena, options, cancellationToken)));
        if (publisherCommentary.Count > 0)
            entries.Add(("enrichment/commentary/publisher.json", await SerializeToBytesAsync(publisherCommentary, options, cancellationToken)));
        if (communityCommentary.Count > 0)
            entries.Add(("enrichment/commentary/community.json", await SerializeToBytesAsync(communityCommentary, options, cancellationToken)));

        foreach (var alignment in package.Alignments.OrderBy(a => a.TargetBook, StringComparer.Ordinal))
        {
            var bytes = await SerializeToBytesAsync(alignment, options, cancellationToken);
            entries.Add(($"alignment/external/{alignment.TargetBook}.json", bytes));
        }

        return entries;
    }

    private static async Task<byte[]> SerializeToBytesAsync<T>(
        T value, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, value, options, cancellationToken);
        return ms.ToArray();
    }
}
