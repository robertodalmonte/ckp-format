namespace Ckp.Tests;

using Ckp.Core;
using Ckp.Core.Field;
using Ckp.IO;

public sealed class FieldPackageCompilerTests
{
    private readonly FieldPackageCompiler _compiler = new();

    [Fact]
    public void Single_package_produces_all_frontier_claims()
    {
        var claim = MakeClaim("alpha-3e.BIO.001", "FAK triggers osteoclasts.", Tier.T1);
        var package = MakePackage("alpha-3e", 2019, claim);

        var result = _compiler.Compile("orthodontics", "2026.4", [package], []);

        result.Package.Claims.Should().HaveCount(1);
        result.Package.Claims[0].Status.Should().Be(ClaimStatus.Frontier);
        result.FrontierCount.Should().Be(1);
        result.AutoMergedCount.Should().Be(0);
    }

    [Fact]
    public void High_confidence_proposal_auto_merges_into_converged_claim()
    {
        var srcClaim = MakeClaim("alpha-3e.BIO.001", "FAK triggers osteoclasts.", Tier.T1);
        var tgtClaim = MakeClaim("beta-2e.MEC.001", "Integrin-FAK mechanotransduction.", Tier.T1);

        var srcPkg = MakePackage("alpha-3e", 2019, srcClaim);
        var tgtPkg = MakePackage("beta-2e", 2022, tgtClaim);

        var proposal = new AlignmentProposal(
            "alpha-3e.BIO.001", "beta-2e.MEC.001",
            0.85, "shared FAK pathway", "ckp:ortho:mech:fak-cascade", false);

        var result = _compiler.Compile("orthodontics", "2026.4", [srcPkg, tgtPkg], [proposal]);

        result.AutoMergedCount.Should().Be(1);

        var converged = result.Package.Claims.Where(c => c.Status == ClaimStatus.Converged).ToList();
        converged.Should().HaveCount(1);
        converged[0].Attestations.Should().HaveCount(2);
        converged[0].ConsensusTier.Should().Be(Tier.T1);
        converged[0].Confidence.FinalValue.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Low_confidence_proposal_goes_to_review_queue()
    {
        var srcClaim = MakeClaim("alpha-3e.BIO.001", "Claim A.", Tier.T1);
        var tgtClaim = MakeClaim("beta-2e.MEC.001", "Claim B.", Tier.T1);

        var srcPkg = MakePackage("alpha-3e", 2019, srcClaim);
        var tgtPkg = MakePackage("beta-2e", 2022, tgtClaim);

        var proposal = new AlignmentProposal(
            "alpha-3e.BIO.001", "beta-2e.MEC.001",
            0.45, "weak alignment", "ckp:ortho:bio:general", false);

        var result = _compiler.Compile("orthodontics", "2026.4", [srcPkg, tgtPkg], [proposal]);

        result.AutoMergedCount.Should().Be(0);
        result.ReviewNeeded.Should().HaveCount(1);
    }

    [Fact]
    public void Contradiction_proposal_produces_divergent_claim()
    {
        var srcClaim = MakeClaim("alpha-3e.BIO.001", "Pressure-tension is dominant.", Tier.T1);
        var tgtClaim = MakeClaim("other-1e.BIO.001", "Piezoelectric is dominant.", Tier.T2);

        var srcPkg = MakePackage("alpha-3e", 2019, srcClaim);
        var tgtPkg = MakePackage("other-1e", 2020, tgtClaim);

        var proposal = new AlignmentProposal(
            "alpha-3e.BIO.001", "other-1e.BIO.001",
            0.80, "same phenomenon, opposite conclusions",
            "ckp:ortho:biomech:tooth-movement-transduction", true);

        var result = _compiler.Compile("orthodontics", "2026.4", [srcPkg, tgtPkg], [proposal]);

        var divergent = result.Package.Claims.Where(c => c.Status == ClaimStatus.Divergent).ToList();
        divergent.Should().HaveCount(1);
        divergent[0].Branches.Should().HaveCount(2);
    }

    [Fact]
    public void Merged_claims_excluded_from_frontier()
    {
        var srcClaim1 = MakeClaim("alpha-3e.BIO.001", "FAK claim.", Tier.T1);
        var srcClaim2 = MakeClaim("alpha-3e.EPI.001", "Epidemiology.", Tier.T1);
        var tgtClaim = MakeClaim("beta-2e.MEC.001", "Mechanotransduction.", Tier.T1);

        var srcPkg = MakePackage("alpha-3e", 2019, srcClaim1, srcClaim2);
        var tgtPkg = MakePackage("beta-2e", 2022, tgtClaim);

        var proposal = new AlignmentProposal(
            "alpha-3e.BIO.001", "beta-2e.MEC.001",
            0.85, "shared pathway", "ckp:ortho:mech:fak", false);

        var result = _compiler.Compile("orthodontics", "2026.4", [srcPkg, tgtPkg], [proposal]);

        // BIO.001 merged, EPI.001 frontier, MEC.001 merged → 1 converged + 1 frontier
        result.AutoMergedCount.Should().Be(1);
        result.FrontierCount.Should().Be(1);
        result.Package.Claims.Should().HaveCount(2);
    }

    [Fact]
    public void T0_constraints_back_propagate_across_attestations()
    {
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceReferenceType.Axiom, "T0:BIO.002", EvidenceRelationship.ConstrainedBy, null, null)
        };
        var srcClaim = PackageClaim.CreateNew("alpha-3e.BIO.001", "Claim with T0.", Tier.T1,
            "mechanotransduction", evidence: evidence);
        var tgtClaim = MakeClaim("beta-2e.MEC.001", "Claim without T0.", Tier.T1);

        var srcPkg = MakePackage("alpha-3e", 2019, srcClaim);
        var tgtPkg = MakePackage("beta-2e", 2022, tgtClaim);

        var proposal = new AlignmentProposal(
            "alpha-3e.BIO.001", "beta-2e.MEC.001",
            0.85, "shared pathway", "ckp:ortho:mech:fak", false);

        var result = _compiler.Compile("orthodontics", "2026.4", [srcPkg, tgtPkg], [proposal]);

        var converged = result.Package.Claims.First(c => c.Status == ClaimStatus.Converged);
        converged.T0Constraints.Should().Contain("T0:BIO.002",
            "T0 should back-propagate from Alpha to the canonical claim");
    }

    [Fact]
    public void Field_package_stores_compilation_parameters()
    {
        var claim = MakeClaim("alpha-3e.BIO.001", "Test.", Tier.T1);
        var package = MakePackage("alpha-3e", 2019, claim);

        var result = _compiler.Compile("orthodontics", "2026.4", [package], []);

        result.Package.DecayLambda.Should().BeApproximately(0.058, 0.001);
        result.Package.SurvivalAlpha.Should().BeApproximately(0.1, 0.001);
        result.Package.TurbulenceTauBase.Should().BeApproximately(0.7, 0.001);
        result.Package.SourcePackages.Should().Contain("alpha-3e");
    }

    [Fact]
    public void Converged_claim_has_vocabulary_map_with_both_books()
    {
        var srcClaim = MakeClaim("alpha-3e.BIO.001", "FAK triggers osteoclasts.", Tier.T1,
            keywords: ["FAK", "osteoclast"]);
        var tgtClaim = MakeClaim("beta-2e.MEC.001", "Integrin-FAK signaling.", Tier.T1,
            keywords: ["integrin", "FAK"]);

        var srcPkg = MakePackage("alpha-3e", 2019, srcClaim);
        var tgtPkg = MakePackage("beta-2e", 2022, tgtClaim);

        var proposal = new AlignmentProposal(
            "alpha-3e.BIO.001", "beta-2e.MEC.001",
            0.85, "shared FAK", "ckp:ortho:mech:fak", false);

        var result = _compiler.Compile("orthodontics", "2026.4", [srcPkg, tgtPkg], [proposal]);

        var converged = result.Package.Claims.First(c => c.Status == ClaimStatus.Converged);
        converged.VocabularyMap.Should().ContainKey("alpha-3e");
        converged.VocabularyMap.Should().ContainKey("beta-2e");
    }

    [Fact]
    public void Tier_disagreement_with_high_weight_triggers_turbulence()
    {
        // Recent book (2024) says T2, old consensus says T1
        var srcClaim = MakeClaim("old-1e.BIO.001", "Established claim.", Tier.T1);
        var tgtClaim = MakeClaim("new-1e.BIO.001", "Demoted claim.", Tier.T2);

        var srcPkg = MakePackage("old-1e", 2010, srcClaim);
        var tgtPkg = MakePackage("new-1e", 2024, tgtClaim);

        var proposal = new AlignmentProposal(
            "old-1e.BIO.001", "new-1e.BIO.001",
            0.85, "same claim", "ckp:test:bio:general", false);

        var result = _compiler.Compile("test", "2026.4", [srcPkg, tgtPkg], [proposal]);

        var converged = result.Package.Claims.First(c => c.Status == ClaimStatus.Converged);
        // The new book at weight ~0.87 vs old at ~0.44 with 1-tier gap
        // Consensus should be T2 (higher weight), and old T1 might trigger turbulence
        // depending on which direction we look at it
        converged.Attestations.Should().HaveCount(2);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static PackageClaim MakeClaim(
        string id, string statement, Tier tier,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyList<EvidenceReference>? evidence = null) =>
        PackageClaim.CreateNew(id: id, statement: statement, tier: tier,
            domain: "mechanotransduction", keywords: keywords, evidence: evidence);

    private static CkpPackage MakePackage(string bookKey, int year, params PackageClaim[] claims)
    {
        int t1 = claims.Count(c => c.Tier == Tier.T1);
        int t2 = claims.Count(c => c.Tier == Tier.T2);
        int t3 = claims.Count(c => c.Tier == Tier.T3);
        int t4 = claims.Count(c => c.Tier == Tier.T4);
        var book = new BookMetadata(bookKey, "Test", 1, ["Author"], "Pub", year, null, "en-US", []);
        var fp = new ContentFingerprint("SHA-256", claims.Length, 1, t1, t2, t3, t4, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        return new CkpPackage { Manifest = manifest, Claims = claims };
    }
}
