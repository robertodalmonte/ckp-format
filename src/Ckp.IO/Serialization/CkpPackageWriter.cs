namespace Ckp.IO;

using System.IO.Compression;
using System.Text.Json;
using Ckp.Core;

/// <summary>
/// Writes a <see cref="CkpPackage"/> to a .ckp ZIP archive following the directory layout
/// defined in the CKP format specification.
/// </summary>
public sealed class CkpPackageWriter : ICkpPackageWriter
{
    public async Task WriteAsync(CkpPackage package, Stream stream, CancellationToken cancellationToken = default)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        var options = CkpJsonOptions.Instance;

        // manifest.json
        await WriteEntryAsync(archive, "manifest.json", package.Manifest, options, cancellationToken);

        // claims/claims.json
        await WriteEntryAsync(archive, "claims/claims.json", package.Claims, options, cancellationToken);

        // claims/by-tier/*.json
        await WriteTierSlicesAsync(archive, package.Claims, options, cancellationToken);

        // claims/by-domain/*.json
        await WriteDomainSlicesAsync(archive, package.Claims, options, cancellationToken);

        // evidence/citations.json
        await WriteEntryAsync(archive, "evidence/citations.json", package.Citations, options, cancellationToken);

        // evidence/axiom-refs.json
        await WriteEntryAsync(archive, "evidence/axiom-refs.json", package.AxiomRefs, options, cancellationToken);

        // structure/chapters.json
        await WriteEntryAsync(archive, "structure/chapters.json", package.Chapters, options, cancellationToken);

        // structure/domains.json
        await WriteEntryAsync(archive, "structure/domains.json", package.Domains, options, cancellationToken);

        // structure/glossary.json
        await WriteEntryAsync(archive, "structure/glossary.json", package.Glossary, options, cancellationToken);

        // history/editions.json
        await WriteEntryAsync(archive, "history/editions.json", package.Editions, options, cancellationToken);

        // history/tier-changes.json — extract all tier history across claims
        var tierChanges = package.Claims
            .Where(c => c.TierHistory.Count > 0)
            .Select(c => new { claimId = c.Id, history = c.TierHistory })
            .ToList();
        await WriteEntryAsync(archive, "history/tier-changes.json", tierChanges, options, cancellationToken);

        // enrichment/mechanisms.json
        if (package.Mechanisms.Count > 0)
            await WriteEntryAsync(archive, "enrichment/mechanisms.json", package.Mechanisms, options, cancellationToken);

        // enrichment/phenomena.json
        if (package.Phenomena.Count > 0)
            await WriteEntryAsync(archive, "enrichment/phenomena.json", package.Phenomena, options, cancellationToken);

        // enrichment/commentary/publisher.json
        if (package.PublisherCommentary.Count > 0)
            await WriteEntryAsync(archive, "enrichment/commentary/publisher.json", package.PublisherCommentary, options, cancellationToken);

        // enrichment/commentary/community.json
        if (package.CommunityCommentary.Count > 0)
            await WriteEntryAsync(archive, "enrichment/commentary/community.json", package.CommunityCommentary, options, cancellationToken);

        // alignment/external/*.json
        foreach (var alignment in package.Alignments)
        {
            string entryName = $"alignment/external/{alignment.TargetBook}.json";
            await WriteEntryAsync(archive, entryName, alignment, options, cancellationToken);
        }
    }

    private static async Task WriteTierSlicesAsync(
        ZipArchive archive,
        IReadOnlyList<PackageClaim> claims,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var tiers = new[] { "T1", "T2", "T3", "T4" };
        foreach (string tier in tiers)
        {
            var slice = claims.Where(c => c.Tier.Equals(tier, StringComparison.OrdinalIgnoreCase)).ToList();
            string entryName = $"claims/by-tier/{tier.ToLowerInvariant()}.json";
            await WriteEntryAsync(archive, entryName, slice, options, cancellationToken);
        }
    }

    private static async Task WriteDomainSlicesAsync(
        ZipArchive archive,
        IReadOnlyList<PackageClaim> claims,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var groups = claims.GroupBy(c => c.Domain);
        foreach (var group in groups)
        {
            string entryName = $"claims/by-domain/{group.Key}.json";
            await WriteEntryAsync(archive, entryName, group.ToList(), options, cancellationToken);
        }
    }

    private static async Task WriteEntryAsync<T>(
        ZipArchive archive,
        string entryName,
        T content,
        JsonSerializerOptions options,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await JsonSerializer.SerializeAsync(entryStream, content, options, cancellationToken);
    }
}
