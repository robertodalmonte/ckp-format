namespace Ckp.Tests;

using Ckp.Core;

public sealed class PackageClaimTests
{
    [Fact]
    public void CreateNew_generates_sha256_hash()
    {
        var claim = PackageClaim.CreateNew(
            id: "delta-14e.ANS.047",
            statement: "Baroreceptor activation reduces heart rate.",
            tier: "T1",
            domain: "autonomic-nervous-system");

        claim.Hash.Should().StartWith("sha256:");
        claim.Hash.Length.Should().BeGreaterThan(7);
    }

    [Fact]
    public void CreateNew_sets_all_required_fields()
    {
        var claim = PackageClaim.CreateNew(
            id: "test-1e.PHY.001",
            statement: "Energy is conserved.",
            tier: "T1",
            domain: "physics",
            subDomain: "thermodynamics",
            chapter: 3,
            section: "Conservation Laws",
            pageRange: "42-45",
            sinceEdition: 1);

        claim.Id.Should().Be("test-1e.PHY.001");
        claim.Statement.Should().Be("Energy is conserved.");
        claim.Tier.Should().Be("T1");
        claim.Domain.Should().Be("physics");
        claim.SubDomain.Should().Be("thermodynamics");
        claim.Chapter.Should().Be(3);
        claim.Section.Should().Be("Conservation Laws");
        claim.PageRange.Should().Be("42-45");
        claim.SinceEdition.Should().Be(1);
    }

    [Fact]
    public void CreateNew_defaults_collections_to_empty()
    {
        var claim = PackageClaim.CreateNew(
            id: "test.001",
            statement: "Test.",
            tier: "T2",
            domain: "test");

        claim.Keywords.Should().BeEmpty();
        claim.MeshTerms.Should().BeEmpty();
        claim.Evidence.Should().BeEmpty();
        claim.Observables.Should().BeEmpty();
        claim.TierHistory.Should().BeEmpty();
    }

    [Fact]
    public void CreateNew_with_evidence_and_observables()
    {
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceReferenceType.Citation, "PMID:12345678", EvidenceRelationship.Supports, EvidenceStrength.Primary, null)
        };
        var observables = new List<Observable>
        {
            new("Heart rate", "bpm", "decrease", "<1s", "ECG")
        };

        var claim = PackageClaim.CreateNew(
            id: "test.002",
            statement: "Vagal stimulation slows heart.",
            tier: "T1",
            domain: "autonomic-nervous-system",
            evidence: evidence,
            observables: observables);

        claim.Evidence.Should().HaveCount(1);
        claim.Evidence[0].Ref.Should().Be("PMID:12345678");
        claim.Observables.Should().HaveCount(1);
        claim.Observables[0].Measurement.Should().Be("Heart rate");
    }

    [Fact]
    public void Same_statement_produces_same_hash()
    {
        var claim1 = PackageClaim.CreateNew(id: "a.001", statement: "Test statement.", tier: "T1", domain: "x");
        var claim2 = PackageClaim.CreateNew(id: "b.002", statement: "Test statement.", tier: "T2", domain: "y");

        claim1.Hash.Should().Be(claim2.Hash);
    }

    [Fact]
    public void Different_statement_produces_different_hash()
    {
        var claim1 = PackageClaim.CreateNew(id: "a.001", statement: "First.", tier: "T1", domain: "x");
        var claim2 = PackageClaim.CreateNew(id: "a.001", statement: "Second.", tier: "T1", domain: "x");

        claim1.Hash.Should().NotBe(claim2.Hash);
    }

    [Fact]
    public void Restore_preserves_all_fields_exactly()
    {
        var tierHistory = new List<TierHistoryEntry> { new(8, "T2", "Initial") };
        string hash = "sha256:abc123";

        var claim = PackageClaim.Restore(
            id: "delta-14e.ANS.047",
            statement: "Test.",
            tier: "T1",
            domain: "ans",
            subDomain: "baroreflex",
            chapter: 18,
            section: "Baroreceptors",
            pageRange: "225-227",
            keywords: ["baroreceptor"],
            meshTerms: ["D017704"],
            evidence: [],
            observables: [],
            sinceEdition: 8,
            tierHistory: tierHistory,
            hash: hash);

        claim.Hash.Should().Be(hash, "Restore must not recompute the hash");
        claim.TierHistory.Should().HaveCount(1);
        claim.TierHistory[0].Edition.Should().Be(8);
    }
}
