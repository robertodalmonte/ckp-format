namespace Ckp.IO;

using System.Text.Json;
using Ckp.Core;

/// <summary>
/// Builds the shared deterministic serialization plan for the non-manifest portion of a
/// <see cref="CkpPackage"/>. Used by <see cref="CkpPackageWriter"/> to drive archive
/// emission and by <see cref="CkpContentHash"/> to fold the content hash. Both callers
/// walk the same <see cref="PackageEntryPlan"/> list, guaranteeing they see the exact
/// same bytes for the same package.
/// </summary>
/// <remarks>
/// <para>
/// P2: pre-P2 this type returned a <c>List&lt;(string Name, byte[] Bytes)&gt;</c> with
/// every non-manifest entry already serialized. The writer needed the bytes to hash
/// them, then to write them into the ZIP — fine for small packages, but at 10 000 claims
/// the peak retained memory was on the order of megabytes per package write, all
/// buffered simultaneously before the first ZIP byte landed. The current design instead
/// returns a sorted list of <see cref="PackageEntryPlan"/> callbacks. The hash pass
/// serializes each entry into a transient reusable buffer, folds the leaf, and discards.
/// The write pass <c>SerializeAsync</c>s each entry straight into the ZIP entry stream.
/// Peak memory drops to one entry's worth of bytes at any moment.
/// </para>
/// </remarks>
internal static class PackageEntrySerializer
{
    /// <summary>
    /// Builds the full ordered list of non-manifest entries for this package. Each
    /// entry carries a <see cref="PackageEntryPlan.WriteToAsync"/> callback the caller
    /// invokes to produce the entry's bytes — either into a transient buffer (hashing)
    /// or straight into a ZIP entry stream (writing). Sorted by <see cref="PackageEntryPlan.Name"/>
    /// using <see cref="StringComparer.Ordinal"/> so the writer and the content-hash
    /// code cannot diverge on order.
    /// </summary>
    /// <remarks>
    /// Per-list natural-key sorting (claims by Id, citations by Ref, etc.) is applied
    /// here — that part has to run eagerly so the resulting in-memory lists are stable
    /// before the callbacks close over them. The closures hold references to the sorted
    /// lists, so nothing re-sorts on serialization.
    /// </remarks>
    public static IReadOnlyList<PackageEntryPlan> PlanEntries(CkpPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

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

        // history/tier-changes.json was removed in spec 1.2 — the data is redundant with
        // PackageClaim.TierHistory (the canonical source of truth) and was never read back
        // by the reference implementation. Tools that want a flat tier-change view should
        // derive it on the consumer side.
        var entries = new List<PackageEntryPlan>
        {
            new("claims/claims.json", (s, ct) => JsonSerializer.SerializeAsync(s, claims, options, ct)),
            new("evidence/citations.json", (s, ct) => JsonSerializer.SerializeAsync(s, citations, options, ct)),
            new("evidence/axiom-refs.json", (s, ct) => JsonSerializer.SerializeAsync(s, axiomRefs, options, ct)),
            new("structure/chapters.json", (s, ct) => JsonSerializer.SerializeAsync(s, chapters, options, ct)),
            new("structure/domains.json", (s, ct) => JsonSerializer.SerializeAsync(s, domains, options, ct)),
            new("structure/glossary.json", (s, ct) => JsonSerializer.SerializeAsync(s, glossary, options, ct)),
            new("history/editions.json", (s, ct) => JsonSerializer.SerializeAsync(s, editions, options, ct)),
        };

        if (mechanisms.Count > 0)
            entries.Add(new("enrichment/mechanisms.json",
                (s, ct) => JsonSerializer.SerializeAsync(s, mechanisms, options, ct)));
        if (phenomena.Count > 0)
            entries.Add(new("enrichment/phenomena.json",
                (s, ct) => JsonSerializer.SerializeAsync(s, phenomena, options, ct)));
        if (publisherCommentary.Count > 0)
            entries.Add(new("enrichment/commentary/publisher.json",
                (s, ct) => JsonSerializer.SerializeAsync(s, publisherCommentary, options, ct)));
        if (communityCommentary.Count > 0)
            entries.Add(new("enrichment/commentary/community.json",
                (s, ct) => JsonSerializer.SerializeAsync(s, communityCommentary, options, ct)));

        foreach (var alignment in package.Alignments.OrderBy(a => a.TargetBook, StringComparer.Ordinal))
        {
            // Capture the alignment in the closure — without this the loop variable would
            // be reused and every callback would serialize the last alignment.
            var captured = alignment;
            entries.Add(new($"alignment/external/{captured.TargetBook}.json",
                (s, ct) => JsonSerializer.SerializeAsync(s, captured, options, ct)));
        }

        entries.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));
        return entries;
    }
}

/// <summary>
/// One entry in the non-manifest serialization plan. <see cref="WriteToAsync"/> is a
/// closure that serializes the entry's payload directly into the caller-supplied stream —
/// no intermediate byte[] on the library side unless the caller explicitly buffers.
/// </summary>
internal readonly record struct PackageEntryPlan(
    string Name,
    Func<Stream, CancellationToken, Task> WriteToAsync);
