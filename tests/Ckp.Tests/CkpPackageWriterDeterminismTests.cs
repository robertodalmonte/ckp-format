namespace Ckp.Tests;

using System.IO.Compression;
using Ckp.Core;
using Ckp.IO;

/// <summary>
/// Byte-determinism tests for <see cref="CkpPackageWriter"/>. Each test covers an
/// item in <c>docs/Refactoring/QualityRaisingPlan.md</c> §3.1 (items 10–13) that
/// <see cref="CkpRoundTripTests"/> does not — round-trip is about semantic fidelity,
/// these tests are about the exact output byte stream.
/// <para>
/// Tests that currently fail are skipped with a reference to T5. T5 fills in the
/// missing sort keys (claims by Id, citations by Ref, domains by Name, etc.) and
/// pins <c>JsonSerializerOptions.WriteIndented = false</c>.
/// </para>
/// </summary>
public sealed class CkpPackageWriterDeterminismTests
{
    // Item 10 — two independent writer instances produce identical bytes for the same input.
    [Fact]
    public async Task Two_independent_writers_produce_identical_bytes()
    {
        var package = BuildPackage();
        var ct = TestContext.Current.CancellationToken;

        using var a = new MemoryStream();
        using var b = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(package, a, ct);
        await new CkpPackageWriter().WriteAsync(package, b, ct);

        a.ToArray().Should().Equal(b.ToArray());
    }

    // Item 13 — every ZIP entry's LastWriteTime must be pinned to the deterministic epoch.
    [Fact]
    public async Task Every_entry_last_write_time_is_pinned_to_deterministic_epoch()
    {
        var package = BuildPackage();
        var ct = TestContext.Current.CancellationToken;

        using var ms = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(package, ms, ct);
        ms.Position = 0;

        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        archive.Entries.Should().NotBeEmpty();
        // ZIP's DOS time field is timezone-naive, so on readback .LastWriteTime is reported
        // in the local offset. Compare the wall-clock DateTime components instead — what
        // matters for determinism is that every entry encodes the same instant.
        foreach (var entry in archive.Entries)
        {
            entry.LastWriteTime.DateTime.Should().Be(CkpPackageWriter.DeterministicEpoch.DateTime,
                $"entry {entry.FullName} must carry the deterministic timestamp");
        }
    }

    // Item 13b — entries emit in lexicographic order so the central directory is stable.
    [Fact]
    public async Task Entries_emit_in_lexicographic_order()
    {
        var package = BuildPackage();
        var ct = TestContext.Current.CancellationToken;

        using var ms = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(package, ms, ct);
        ms.Position = 0;

        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var names = archive.Entries.Select(e => e.FullName).ToList();
        names.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    // Item 11 — claims supplied in a shuffled order must produce the same output bytes.
    [Fact(Skip = "Awaiting T5 — writer must sort claims by Id before serializing.")]
    public async Task Claims_in_shuffled_order_produce_identical_bytes()
    {
        var ordered = BuildPackage();
        var shuffled = ordered with
        {
            Claims = ordered.Claims.Reverse().ToList(),
        };
        var ct = TestContext.Current.CancellationToken;

        using var a = new MemoryStream();
        using var b = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(ordered, a, ct);
        await new CkpPackageWriter().WriteAsync(shuffled, b, ct);

        a.ToArray().Should().Equal(b.ToArray());
    }

    // Item 11b — citations, domains, chapters, editions likewise.
    [Fact(Skip = "Awaiting T5 — writer must sort citations/domains/chapters/editions by their natural key.")]
    public async Task Auxiliary_lists_in_shuffled_order_produce_identical_bytes()
    {
        var ordered = BuildPackage();
        var shuffled = ordered with
        {
            Citations = ordered.Citations.Reverse().ToList(),
            Domains = ordered.Domains.Reverse().ToList(),
            Chapters = ordered.Chapters.Reverse().ToList(),
            Editions = ordered.Editions.Reverse().ToList(),
        };
        var ct = TestContext.Current.CancellationToken;

        using var a = new MemoryStream();
        using var b = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(ordered, a, ct);
        await new CkpPackageWriter().WriteAsync(shuffled, b, ct);

        a.ToArray().Should().Equal(b.ToArray());
    }

    // Item 11c — alignments are already sorted by TargetBook. Pin that behaviour today.
    [Fact]
    public async Task Alignments_in_shuffled_order_produce_identical_bytes()
    {
        var ordered = BuildPackage();
        var shuffled = ordered with
        {
            Alignments = ordered.Alignments.Reverse().ToList(),
        };
        var ct = TestContext.Current.CancellationToken;

        using var a = new MemoryStream();
        using var b = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(ordered, a, ct);
        await new CkpPackageWriter().WriteAsync(shuffled, b, ct);

        a.ToArray().Should().Equal(b.ToArray());
    }

    // Item 12 — compression level is Optimal. Indirect check: compressed stream is strictly
    // smaller than the uncompressed manifest bytes when the input is highly compressible.
    [Fact]
    public async Task Writer_compresses_entries()
    {
        // Build a package whose manifest contains highly repetitive author strings.
        var authors = Enumerable.Repeat("Dr. Repeated Author", 500).ToList();
        var book = new BookMetadata("redundant-1e", "Redundant", 1, authors, "Pub", 2026, null, "en-US", []);
        var fp = new ContentFingerprint("SHA-256", 0, 0, 0, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        var package = new CkpPackage { Manifest = manifest };

        var ct = TestContext.Current.CancellationToken;
        using var ms = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(package, ms, ct);
        ms.Position = 0;

        using var archive = new ZipArchive(ms, ZipArchiveMode.Read);
        var manifestEntry = archive.GetEntry("manifest.json")!;
        manifestEntry.CompressedLength.Should().BeLessThan(manifestEntry.Length,
            "Optimal compression must shrink a highly-redundant manifest");
    }

    private static CkpPackage BuildPackage()
    {
        var claim1 = PackageClaim.CreateNew(
            id: "det-1e.ANS.001",
            statement: "Claim one statement.",
            tier: Tier.T1,
            domain: "autonomic-nervous-system");
        var claim2 = PackageClaim.CreateNew(
            id: "det-1e.ANS.002",
            statement: "Claim two statement.",
            tier: Tier.T2,
            domain: "autonomic-nervous-system");
        var claim3 = PackageClaim.CreateNew(
            id: "det-1e.MUS.001",
            statement: "Muscle claim statement.",
            tier: Tier.T1,
            domain: "musculoskeletal");

        var citations = new List<CitationEntry>
        {
            new("PMID:10000001", "First", "A", 2020, "J1", ["det-1e.ANS.001"]),
            new("PMID:10000002", "Second", "B", 2021, "J2", ["det-1e.ANS.002"]),
        };
        var domains = new List<DomainInfo>
        {
            new("autonomic-nervous-system", 2, 1, 1, 0, 0),
            new("musculoskeletal", 1, 1, 0, 0, 0),
        };
        var chapters = new List<ChapterInfo>
        {
            new(1, "Intro", 2, ["autonomic-nervous-system"]),
            new(2, "Muscle", 1, ["musculoskeletal"]),
        };
        var editions = new List<EditionInfo>
        {
            new(1, 2024, null, null, null),
            new(2, 2026, null, null, null),
        };
        var alignments = new List<BookAlignment>
        {
            new("det-1e", "other-1e", []),
            new("det-1e", "third-1e", []),
        };

        var book = new BookMetadata("det-1e", "Determinism Book", 1, ["Author"], "Pub", 2026, null, "en-US",
            ["autonomic-nervous-system", "musculoskeletal"]);
        var fp = new ContentFingerprint("SHA-256", 3, 2, 2, 1, 0, 0, 2);
        var manifest = PackageManifest.CreateNew(book, fp);

        return new CkpPackage
        {
            Manifest = manifest,
            Claims = [claim1, claim2, claim3],
            Citations = citations,
            Domains = domains,
            Chapters = chapters,
            Editions = editions,
            Alignments = alignments,
        };
    }
}
