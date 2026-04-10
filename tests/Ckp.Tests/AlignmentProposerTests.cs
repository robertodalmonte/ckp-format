namespace Ckp.Tests;

using Ckp.Core;
using Ckp.IO;

public sealed class AlignmentProposerTests
{
    private readonly AlignmentProposer _proposer = new();

    [Fact]
    public void Claims_with_shared_mesh_terms_align()
    {
        var src = MakeClaim("src-1e.BIO.001", "FAK triggers osteoclast formation.",
            domain: "mechanotransduction", meshTerms: ["D051076", "D010513", "D040542"],
            keywords: ["FAK", "osteoclast", "mechanotransduction"]);
        var tgt = MakeClaim("tgt-1e.MEC.001", "Integrin-FAK signaling in fibroblasts.",
            domain: "mechanotransduction", meshTerms: ["D051076", "D040542"],
            keywords: ["FAK", "integrin", "mechanotransduction"]);

        var srcPkg = MakePackage("src-1e", src);
        var tgtPkg = MakePackage("tgt-1e", tgt);

        var proposals = _proposer.Propose(srcPkg, tgtPkg);

        proposals.Should().ContainSingle();
        proposals[0].Score.Should().BeGreaterThan(0.3);
        proposals[0].SourceClaimId.Should().Be("src-1e.BIO.001");
        proposals[0].TargetClaimId.Should().Be("tgt-1e.MEC.001");
    }

    [Fact]
    public void Claims_with_no_overlap_do_not_align()
    {
        var src = MakeClaim("src-1e.EPI.001", "Class II prevalence is 15%.",
            domain: "malocclusion-epidemiology", meshTerms: ["D008310"]);
        var tgt = MakeClaim("tgt-1e.PHY.001", "Relaxin inhibits fibrosis.",
            domain: "fascial-physiology", meshTerms: ["D012065"]);

        var srcPkg = MakePackage("src-1e", src);
        var tgtPkg = MakePackage("tgt-1e", tgt);

        var proposals = _proposer.Propose(srcPkg, tgtPkg);

        proposals.Should().BeEmpty();
    }

    [Fact]
    public void Shared_keywords_contribute_to_score()
    {
        var src = MakeClaim("src-1e.BIO.001", "Collagen remodeling under load.",
            domain: "orthodontic-biology",
            keywords: ["collagen", "mechanical loading", "remodeling", "extracellular matrix"],
            meshTerms: ["D003094", "D040542"]);
        var tgt = MakeClaim("tgt-1e.ECM.001", "Tensional homeostasis of collagen.",
            domain: "extracellular-matrix",
            keywords: ["collagen", "tensional homeostasis", "remodeling", "extracellular matrix"],
            meshTerms: ["D003094"]);

        var srcPkg = MakePackage("src-1e", src);
        var tgtPkg = MakePackage("tgt-1e", tgt);

        var proposals = _proposer.Propose(srcPkg, tgtPkg);

        proposals.Should().ContainSingle();
        proposals[0].Reason.Should().Contain("collagen");
    }

    [Fact]
    public void Same_domain_boosts_alignment_score()
    {
        var src = MakeClaim("src-1e.MEC.001", "Mechanical load induces collagen.",
            domain: "mechanotransduction");
        var tgt = MakeClaim("tgt-1e.MEC.001", "Fibroblasts respond to strain.",
            domain: "mechanotransduction");

        double score = AlignmentProposer.ScorePair(src, tgt);

        score.Should().BeGreaterThan(0, "same domain should contribute positively");
    }

    [Fact]
    public void Large_tier_gap_flags_contradiction()
    {
        var src = MakeClaim("src-1e.BIO.001", "Mechanism X is established.",
            domain: "mechanotransduction", tier: "T1");
        var tgt = MakeClaim("tgt-1e.BIO.001", "Mechanism X is speculative.",
            domain: "mechanotransduction", tier: "T3",
            meshTerms: ["D040542"]);
        // Give src a shared MeSH to ensure alignment triggers
        var srcWithMesh = MakeClaim("src-1e.BIO.001", "Mechanism X is established.",
            domain: "mechanotransduction", tier: "T1", meshTerms: ["D040542"]);

        var srcPkg = MakePackage("src-1e", srcWithMesh);
        var tgtPkg = MakePackage("tgt-1e", tgt);

        var proposals = _proposer.Propose(srcPkg, tgtPkg);

        proposals.Should().ContainSingle();
        proposals[0].IsContradiction.Should().BeTrue();
    }

    [Fact]
    public void Each_target_claim_used_at_most_once()
    {
        var src1 = MakeClaim("src-1e.BIO.001", "Claim A.",
            domain: "mechanotransduction", meshTerms: ["D040542"]);
        var src2 = MakeClaim("src-1e.BIO.002", "Claim B.",
            domain: "mechanotransduction", meshTerms: ["D040542"]);
        var tgt = MakeClaim("tgt-1e.MEC.001", "Target claim.",
            domain: "mechanotransduction", meshTerms: ["D040542"]);

        var srcPkg = MakePackage("src-1e", src1, src2);
        var tgtPkg = MakePackage("tgt-1e", tgt);

        var proposals = _proposer.Propose(srcPkg, tgtPkg);

        // Only one of src1/src2 should align to the single target
        proposals.Should().HaveCount(1);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static PackageClaim MakeClaim(
        string id, string statement,
        string domain = "mechanotransduction",
        string? tier = "T1",
        IReadOnlyList<string>? meshTerms = null,
        IReadOnlyList<string>? keywords = null,
        IReadOnlyList<Observable>? observables = null) =>
        PackageClaim.CreateNew(
            id: id,
            statement: statement,
            tier: tier!,
            domain: domain,
            meshTerms: meshTerms,
            keywords: keywords,
            observables: observables);

    private static CkpPackage MakePackage(string bookKey, params PackageClaim[] claims)
    {
        int t1 = claims.Count(c => c.Tier == "T1");
        int t2 = claims.Count(c => c.Tier == "T2");
        int t3 = claims.Count(c => c.Tier == "T3");
        int t4 = claims.Count(c => c.Tier == "T4");
        var book = new BookMetadata(bookKey, "Test", 1, ["Author"], "Pub", 2020, null, "en-US", []);
        var fp = new ContentFingerprint("SHA-256", claims.Length, 1, t1, t2, t3, t4, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        return new CkpPackage(manifest, claims, [], [], [], [], [], [], [], [], [], [], []);
    }
}
