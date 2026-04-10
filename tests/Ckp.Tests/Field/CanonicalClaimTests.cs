namespace Ckp.Tests.Field;

using Ckp.Core.Field;

public sealed class CanonicalClaimTests
{
    [Fact]
    public void Frontier_claim_has_single_attestation()
    {
        var claim = CreateFrontierClaim();

        claim.Status.Should().Be(ClaimStatus.Frontier);
        claim.Attestations.Should().HaveCount(1);
        claim.Branches.Should().BeNull();
        claim.Turbulence.Should().BeNull();
    }

    [Fact]
    public void Converged_claim_has_multiple_attestations()
    {
        var claim = CreateConvergedClaim();

        claim.Status.Should().Be(ClaimStatus.Converged);
        claim.Attestations.Should().HaveCountGreaterThan(1);
        claim.Branches.Should().BeNull();
    }

    [Fact]
    public void Divergent_claim_has_branches()
    {
        var claim = CreateDivergentClaim();

        claim.Status.Should().Be(ClaimStatus.Divergent);
        claim.Branches.Should().NotBeNull();
        claim.Branches.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Canonical_id_follows_urn_format()
    {
        var claim = CreateConvergedClaim();

        claim.CanonicalId.Should().StartWith("ckp:");
        claim.CanonicalId.Split(':').Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void T0_constraints_back_propagate_from_any_attestation()
    {
        var claim = CreateConvergedClaim();

        // Only one attestation linked T0, but the canonical claim inherits it
        claim.T0Constraints.Should().Contain("T0:BIO.002");
    }

    [Fact]
    public void Vocabulary_map_captures_cross_book_terminology()
    {
        var claim = CreateConvergedClaim();

        claim.VocabularyMap.Should().ContainKey("alpha-3e");
        claim.VocabularyMap.Should().ContainKey("beta-2e");
        claim.VocabularyMap["alpha-3e"].Should().Contain("FAK");
        claim.VocabularyMap["beta-2e"].Should().Contain("integrin");
    }

    [Fact]
    public void Confidence_score_decomposes_into_audit_trail()
    {
        var claim = CreateConvergedClaim();

        claim.Confidence.FinalValue.Should().BeGreaterThan(0);
        claim.Confidence.BaseAuthoritySum.Should().BeGreaterThan(0);
        claim.Confidence.DecayPenalty.Should().BeGreaterThanOrEqualTo(0);
        claim.Confidence.SurvivalBonus.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Field_package_records_compilation_parameters()
    {
        var package = CreateFieldPackage();

        package.FieldId.Should().Be("orthodontics");
        package.DecayLambda.Should().BeApproximately(0.058, 0.001);
        package.SurvivalAlpha.Should().BeApproximately(0.1, 0.001);
        package.TurbulenceTauBase.Should().BeApproximately(0.7, 0.001);
        package.SourcePackages.Should().Contain("alpha-3e");
        package.SourcePackages.Should().Contain("beta-2e");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static CanonicalClaim CreateFrontierClaim() => new(
        CanonicalId: "ckp:ortho:epidemiology:class-ii-prevalence",
        Status: ClaimStatus.Frontier,
        Statement: "Class II malocclusion affects approximately 15% of the U.S. population.",
        ConsensusTier: "T1",
        Confidence: new ConfidenceScore(0.67, 1.0, 0.33, 0.0),
        Attestations: [new Attestation("alpha-3e", "alpha-3e.EPI.001", "T1", 2019, 6, 0.785, null)],
        VocabularyMap: new Dictionary<string, string> { ["alpha-3e"] = "Class II malocclusion" },
        T0Constraints: [],
        Turbulence: null,
        Branches: null);

    private static CanonicalClaim CreateConvergedClaim() => new(
        CanonicalId: "ckp:ortho:biomech:fak-osteoclast-cascade",
        Status: ClaimStatus.Converged,
        Statement: "Focal adhesion kinase compression in PDL cells triggers PgE2 release and RANKL-mediated osteoclast differentiation.",
        ConsensusTier: "T2",
        Confidence: new ConfidenceScore(0.82, 2.0, 0.52, 0.18),
        Attestations:
        [
            new Attestation("alpha-3e", "alpha-3e.BIO.007", "T2", 2019, 6, 0.785, null),
            new Attestation("beta-2e", "beta-2e.MEC.001", "T1", 2022, 2, 0.848,
                "same pathway in fascial fibroblasts")
        ],
        VocabularyMap: new Dictionary<string, string>
        {
            ["alpha-3e"] = "focal adhesion kinase (FAK)",
            ["beta-2e"] = "mechanotransduction via integrin-FAK signaling"
        },
        T0Constraints: ["T0:BIO.002"],
        Turbulence: null,
        Branches: null);

    private static CanonicalClaim CreateDivergentClaim() => new(
        CanonicalId: "ckp:ortho:biomech:tooth-movement-transduction",
        Status: ClaimStatus.Divergent,
        Statement: "The primary biological control mechanism for orthodontic tooth movement.",
        ConsensusTier: "T1",
        Confidence: new ConfidenceScore(0.71, 2.0, 0.58, 0.0),
        Attestations:
        [
            new Attestation("alpha-3e", "alpha-3e.BIO.003", "T1", 2019, 6, 0.785, null),
            new Attestation("alpha-3e", "alpha-3e.BIO.004", "T2", 2019, 5, 0.74, null)
        ],
        VocabularyMap: new Dictionary<string, string>
        {
            ["alpha-3e"] = "pressure-tension / piezoelectric theories"
        },
        T0Constraints: [],
        Turbulence: null,
        Branches:
        [
            new DivergentBranch(
                "Chemical messengers from PDL compression are the dominant control mechanism.",
                "T1",
                [new Attestation("alpha-3e", "alpha-3e.BIO.003", "T1", 2019, 6, 0.785, null)]),
            new DivergentBranch(
                "Piezoelectric signals from bone bending are the primary control mechanism.",
                "T2",
                [new Attestation("alpha-3e", "alpha-3e.BIO.004", "T2", 2019, 5, 0.74, null)])
        ]);

    private static FieldPackage CreateFieldPackage() => new(
        FieldId: "orthodontics",
        Version: "2026.4",
        CompiledAt: new DateTimeOffset(2026, 4, 9, 0, 0, 0, TimeSpan.Zero),
        SourcePackages: ["alpha-3e", "beta-2e"],
        Claims: [CreateConvergedClaim()],
        DecayLambda: 0.058,
        SurvivalAlpha: 0.1,
        TurbulenceTauBase: 0.7);
}
