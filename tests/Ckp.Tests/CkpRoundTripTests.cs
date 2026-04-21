namespace Ckp.Tests;

using Ckp.Core;
using Ckp.IO;

public sealed class CkpRoundTripTests
{
    private readonly CkpPackageWriter _writer = new();
    private readonly CkpPackageReader _reader = new();

    [Fact]
    public async Task Write_then_read_preserves_manifest()
    {
        var package = CreateTestPackage();

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Manifest.FormatVersion.Should().Be("1.0");
        roundTripped.Manifest.Book.Key.Should().Be("test-1e");
        roundTripped.Manifest.Book.Title.Should().Be("Test Textbook");
        roundTripped.Manifest.Book.Edition.Should().Be(1);
        roundTripped.Manifest.Book.Authors.Should().ContainSingle().Which.Should().Be("Test Author");
        roundTripped.Manifest.ContentFingerprint.ClaimCount.Should().Be(2);
    }

    [Fact]
    public async Task Write_then_read_preserves_claims()
    {
        var package = CreateTestPackage();

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Claims.Should().HaveCount(2);
        var first = roundTripped.Claims.First(c => c.Id == "test-1e.ANS.001");
        first.Statement.Should().Be("Baroreceptor activation reduces heart rate.");
        first.Tier.Should().Be(Tier.T1);
        first.Domain.Should().Be("autonomic-nervous-system");
        first.Hash.Should().StartWith("sha256:");
    }

    [Fact]
    public async Task Write_then_read_preserves_evidence_on_claims()
    {
        var package = CreateTestPackage();

        var roundTripped = await RoundTripAsync(package);

        var claim = roundTripped.Claims.First(c => c.Id == "test-1e.ANS.001");
        claim.Evidence.Should().HaveCount(1);
        claim.Evidence[0].Ref.Should().Be("PMID:19834602");
        claim.Evidence[0].Type.Should().Be(EvidenceReferenceType.Citation);
        claim.Evidence[0].Relationship.Should().Be(EvidenceRelationship.Supports);
        claim.Evidence[0].Strength.Should().Be(EvidenceStrength.Primary);
    }

    [Fact]
    public async Task Write_then_read_preserves_observables()
    {
        var package = CreateTestPackage();

        var roundTripped = await RoundTripAsync(package);

        var claim = roundTripped.Claims.First(c => c.Id == "test-1e.ANS.001");
        claim.Observables.Should().HaveCount(1);
        claim.Observables[0].Measurement.Should().Be("Heart rate decrease");
        claim.Observables[0].Unit.Should().Be("bpm");
        claim.Observables[0].Direction.Should().Be("decrease");
    }

    [Fact]
    public async Task Write_then_read_preserves_citations()
    {
        var package = CreateTestPackage();

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Citations.Should().HaveCount(1);
        roundTripped.Citations[0].Ref.Should().Be("PMID:19834602");
        roundTripped.Citations[0].ReferencedBy.Should().Contain("test-1e.ANS.001");
    }

    [Fact]
    public async Task Write_then_read_preserves_glossary()
    {
        var package = CreateTestPackage();

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Glossary.Should().HaveCount(1);
        roundTripped.Glossary[0].BookTerm.Should().Be("baroreceptor");
        roundTripped.Glossary[0].EquivalentsInOtherBooks.Should().ContainKey("gamma-2e");
    }

    [Fact]
    public async Task Write_then_read_preserves_domains()
    {
        var package = CreateTestPackage();

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Domains.Should().HaveCount(1);
        roundTripped.Domains[0].Name.Should().Be("autonomic-nervous-system");
        roundTripped.Domains[0].ClaimCount.Should().Be(2);
    }

    [Fact]
    public async Task Write_then_read_preserves_alignments()
    {
        var package = CreateTestPackage();

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Alignments.Should().HaveCount(1);
        roundTripped.Alignments[0].SourceBook.Should().Be("test-1e");
        roundTripped.Alignments[0].TargetBook.Should().Be("other-2e");
        roundTripped.Alignments[0].Alignments.Should().HaveCount(1);
        roundTripped.Alignments[0].Alignments[0].Type.Should().Be(AlignmentType.Equivalent);
    }

    [Fact]
    public async Task Write_then_read_preserves_tier_history()
    {
        var package = CreateTestPackage();

        var roundTripped = await RoundTripAsync(package);

        var claim = roundTripped.Claims.First(c => c.Id == "test-1e.ANS.001");
        claim.TierHistory.Should().HaveCount(2);
        claim.TierHistory[0].Edition.Should().Be(8);
        claim.TierHistory[0].Tier.Should().Be(Tier.T2);
        claim.TierHistory[1].Edition.Should().Be(10);
        claim.TierHistory[1].Tier.Should().Be(Tier.T1);
    }

    [Fact]
    public async Task Writing_same_package_twice_produces_byte_identical_archives()
    {
        var package = CreateTestPackage();
        var ct = TestContext.Current.CancellationToken;

        using var a = new MemoryStream();
        using var b = new MemoryStream();
        await _writer.WriteAsync(package, a, ct);
        await _writer.WriteAsync(package, b, ct);

        a.ToArray().Should().BeEquivalentTo(b.ToArray());
    }

    [Fact]
    public async Task Empty_package_round_trips()
    {
        var book = new BookMetadata("empty-1e", "Empty Book", 1, ["Nobody"], "None", 2026, null, "en-US", []);
        var fp = new ContentFingerprint("SHA-256", 0, 0, 0, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        var package = new CkpPackage { Manifest = manifest };

        var roundTripped = await RoundTripAsync(package);

        roundTripped.Claims.Should().BeEmpty();
        roundTripped.Citations.Should().BeEmpty();
        roundTripped.Alignments.Should().BeEmpty();
    }

    private async Task<CkpPackage> RoundTripAsync(CkpPackage package)
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = new MemoryStream();
        await _writer.WriteAsync(package, ms, ct);
        ms.Position = 0;
        return await _reader.ReadAsync(ms, ct);
    }

    private static CkpPackage CreateTestPackage()
    {
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceReferenceType.Citation, "PMID:19834602", EvidenceRelationship.Supports, EvidenceStrength.Primary, null)
        };
        var observables = new List<Observable>
        {
            new("Heart rate decrease", "bpm", "decrease", "<1 cardiac cycle", "ECG")
        };
        var tierHistory = new List<TierHistoryEntry>
        {
            new(8, Tier.T2, "Introduced as supported hypothesis"),
            new(10, Tier.T1, "Promoted after consensus review")
        };

        var claim1 = PackageClaim.CreateNew(
            id: "test-1e.ANS.001",
            statement: "Baroreceptor activation reduces heart rate.",
            tier: Tier.T1,
            domain: "autonomic-nervous-system",
            subDomain: "baroreceptor-reflex",
            chapter: 18,
            section: "Arterial Baroreceptor Reflex",
            pageRange: "225-227",
            keywords: ["baroreceptor", "heart rate"],
            meshTerms: ["D017704"],
            evidence: evidence,
            observables: observables,
            sinceEdition: 8,
            tierHistory: tierHistory);

        var claim2 = PackageClaim.CreateNew(
            id: "test-1e.ANS.002",
            statement: "Vagal tone modulates resting heart rate.",
            tier: Tier.T1,
            domain: "autonomic-nervous-system");

        var citations = new List<CitationEntry>
        {
            new("PMID:19834602", "Baroreflex sensitivity study", "Smith J et al.", 2009, "Circulation", ["test-1e.ANS.001"])
        };

        var glossary = new List<GlossaryEntry>
        {
            new("baroreceptor", "arterial baroreceptor", "D017704",
                new Dictionary<string, string> { ["gamma-2e"] = "fascial stretch receptor" },
                "Cross-vocabulary mapping")
        };

        var domains = new List<DomainInfo>
        {
            new("autonomic-nervous-system", 2, 2, 0, 0, 0)
        };

        var alignment = new BookAlignment("test-1e", "other-2e", [
            new ClaimAlignment("test-1e.ANS.001", "other-2e.MEC.005", AlignmentType.Equivalent,
                0.88, new TierMismatch(Tier.T1, Tier.T2, TierMismatchDirection.SourceAhead),
                null, "consilience-auto", null, null)
        ]);

        var book = new BookMetadata("test-1e", "Test Textbook", 1, ["Test Author"], "Publisher", 2026, "978-0000000000", "en-US",
            ["autonomic-nervous-system"]);
        var fp = new ContentFingerprint("SHA-256", 2, 1, 2, 0, 0, 0, 1);
        var manifest = PackageManifest.CreateNew(book, fp,
            alignments: [new AlignmentSummary("other-2e", null, 1, 1)]);

        return new CkpPackage
        {
            Manifest = manifest,
            Claims = [claim1, claim2],
            Citations = citations,
            Domains = domains,
            Glossary = glossary,
            Alignments = [alignment],
        };
    }
}
