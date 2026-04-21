namespace Ckp.Tests;

using Ckp.Core;
using Ckp.IO;

public sealed class EnrichmentRoundTripTests
{
    private readonly CkpPackageWriter _writer = new();
    private readonly CkpPackageReader _reader = new();

    [Fact]
    public async Task Mechanisms_round_trip()
    {
        var mechanisms = new List<MechanismEntry>
        {
            new("FAK-osteoclast cascade",
                "FAK-mediated osteoclast activation under orthodontic force.",
                ["test-1e.BIO.001", "test-1e.BIO.002"],
                ["integrin-FAK", "Rho-ROCK signaling"],
                ["Cell morphology changes under mechanical loading"])
        };
        var package = CreatePackage(mechanisms: mechanisms);

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Mechanisms.Should().ContainSingle();
        roundTripped.Mechanisms[0].Name.Should().Be("FAK-osteoclast cascade");
        roundTripped.Mechanisms[0].ClaimIds.Should().HaveCount(2);
        roundTripped.Mechanisms[0].PathwayTerms.Should().Contain("integrin-FAK");
        roundTripped.Mechanisms[0].PredictedMeasurements.Should().ContainSingle();
    }

    [Fact]
    public async Task Phenomena_round_trip()
    {
        var phenomena = new List<PhenomenonEntry>
        {
            new("trigeminal-autonomic coupling",
                "Trigeminal afferents modulate autonomic tone.",
                ["test-1e.ANS.001", "test-1e.OCC.003"],
                "mechanical deformation -> autonomic modulation")
        };
        var package = CreatePackage(phenomena: phenomena);

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Phenomena.Should().ContainSingle();
        roundTripped.Phenomena[0].Name.Should().Be("trigeminal-autonomic coupling");
        roundTripped.Phenomena[0].SharedConcept.Should().Be("mechanical deformation -> autonomic modulation");
    }

    [Fact]
    public async Task Publisher_commentary_round_trips()
    {
        var commentary = new List<CommentaryEntry>
        {
            new("test-1e.ANS.001", "Dr. Smith", "This claim was recently confirmed by a 2025 meta-analysis.",
                new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero))
        };
        var package = CreatePackage(publisherCommentary: commentary);

        var roundTripped = await RoundTripAsync(package);

        roundTripped.PublisherCommentary.Should().ContainSingle();
        roundTripped.PublisherCommentary[0].ClaimId.Should().Be("test-1e.ANS.001");
        roundTripped.PublisherCommentary[0].Author.Should().Be("Dr. Smith");
    }

    [Fact]
    public async Task Community_commentary_round_trips()
    {
        var commentary = new List<CommentaryEntry>
        {
            new("test-1e.BIO.001", "reviewer42", "The latency measurement needs refinement.",
                new DateTimeOffset(2026, 4, 10, 0, 0, 0, TimeSpan.Zero))
        };
        var package = CreatePackage(communityCommentary: commentary);

        var roundTripped = await RoundTripAsync(package);

        roundTripped.CommunityCommentary.Should().ContainSingle();
        roundTripped.CommunityCommentary[0].Author.Should().Be("reviewer42");
    }

    [Fact]
    public async Task Empty_enrichment_round_trips()
    {
        var package = CreatePackage();

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Mechanisms.Should().BeEmpty();
        roundTripped.Phenomena.Should().BeEmpty();
        roundTripped.PublisherCommentary.Should().BeEmpty();
        roundTripped.CommunityCommentary.Should().BeEmpty();
    }

    [Fact]
    public async Task Enrichment_entries_do_not_appear_in_zip_when_empty()
    {
        var package = CreatePackage();
        var ct = TestContext.Current.CancellationToken;

        using var ms = new MemoryStream();
        await _writer.WriteAsync(package, ms, ct);
        ms.Position = 0;

        using var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        archive.GetEntry("enrichment/mechanisms.json").Should().BeNull();
        archive.GetEntry("enrichment/phenomena.json").Should().BeNull();
        archive.GetEntry("enrichment/commentary/publisher.json").Should().BeNull();
        archive.GetEntry("enrichment/commentary/community.json").Should().BeNull();
    }

    private async Task<CkpPackage> RoundTripAsync(CkpPackage package)
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = new MemoryStream();
        await _writer.WriteAsync(package, ms, ct);
        ms.Position = 0;
        return await _reader.ReadAsync(ms, ct);
    }

    private static CkpPackage CreatePackage(
        IReadOnlyList<MechanismEntry>? mechanisms = null,
        IReadOnlyList<PhenomenonEntry>? phenomena = null,
        IReadOnlyList<CommentaryEntry>? publisherCommentary = null,
        IReadOnlyList<CommentaryEntry>? communityCommentary = null)
    {
        var book = new BookMetadata("test-1e", "Test Book", 1, ["Author"], "Publisher", 2026, null, "en", []);
        var fp = new ContentFingerprint("SHA-256", 0, 0, 0, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        return new CkpPackage
        {
            Manifest = manifest,
            Mechanisms = mechanisms ?? [],
            Phenomena = phenomena ?? [],
            PublisherCommentary = publisherCommentary ?? [],
            CommunityCommentary = communityCommentary ?? [],
        };
    }
}
