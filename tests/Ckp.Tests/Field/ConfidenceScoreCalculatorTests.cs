namespace Ckp.Tests.Field;

using Ckp.Core.Field;

public sealed class ConfidenceScoreCalculatorTests
{
    private const int CurrentYear = 2026;

    // ── Single attestation weight computation ───────────────────────────

    [Fact]
    public void Brand_new_book_with_one_edition_has_full_weight()
    {
        double weight = ConfidenceScoreCalculator.ComputeWeight(
            baseAuthority: 1.0, publicationYear: 2026, currentYear: 2026, editionsSurvived: 1);

        weight.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void Alpha_3e_weight_matches_reference_table()
    {
        // Alpha 3e: 2019, 6 editions survived, Authority=1.0
        // Expected: 1.0 × (1 + 0.1·ln(6)) × e^(-0.058·7) ≈ 1.0 × 1.179 × 0.666 ≈ 0.785
        double weight = ConfidenceScoreCalculator.ComputeWeight(
            baseAuthority: 1.0, publicationYear: 2019, currentYear: 2026, editionsSurvived: 6);

        weight.Should().BeApproximately(0.785, 0.01);
    }

    [Fact]
    public void Beta_2e_weight_matches_reference_table()
    {
        // Beta 2e: 2022, 2 editions survived, Authority=1.0
        // Expected: 1.0 × (1 + 0.1·ln(2)) × e^(-0.058·4) ≈ 1.0 × 1.069 × 0.793 ≈ 0.848
        double weight = ConfidenceScoreCalculator.ComputeWeight(
            baseAuthority: 1.0, publicationYear: 2022, currentYear: 2026, editionsSurvived: 2);

        weight.Should().BeApproximately(0.848, 0.01);
    }

    [Fact]
    public void Half_life_at_12_years()
    {
        // At exactly the half-life (12 years), weight ≈ 0.5 (with 1 edition, no survival bonus)
        double weight = ConfidenceScoreCalculator.ComputeWeight(
            baseAuthority: 1.0, publicationYear: 2014, currentYear: 2026, editionsSurvived: 1);

        weight.Should().BeApproximately(0.50, 0.02);
    }

    [Fact]
    public void Old_book_25_years_has_low_weight()
    {
        double weight = ConfidenceScoreCalculator.ComputeWeight(
            baseAuthority: 1.0, publicationYear: 2001, currentYear: 2026, editionsSurvived: 1);

        weight.Should().BeApproximately(0.234, 0.02);
    }

    [Fact]
    public void Survival_bonus_increases_with_editions()
    {
        double w1 = ConfidenceScoreCalculator.ComputeWeight(1.0, 2019, 2026, editionsSurvived: 1);
        double w3 = ConfidenceScoreCalculator.ComputeWeight(1.0, 2019, 2026, editionsSurvived: 3);
        double w6 = ConfidenceScoreCalculator.ComputeWeight(1.0, 2019, 2026, editionsSurvived: 6);

        w3.Should().BeGreaterThan(w1);
        w6.Should().BeGreaterThan(w3);
    }

    [Fact]
    public void Zero_age_produces_no_decay()
    {
        double weight = ConfidenceScoreCalculator.ComputeWeight(
            baseAuthority: 0.8, publicationYear: 2026, currentYear: 2026, editionsSurvived: 1);

        weight.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public void Custom_lambda_changes_decay_rate()
    {
        // Faster decay (λ=0.1): weight should be lower than default
        double fast = ConfidenceScoreCalculator.ComputeWeight(1.0, 2019, 2026, 1, lambda: 0.1);
        double def = ConfidenceScoreCalculator.ComputeWeight(1.0, 2019, 2026, 1);

        fast.Should().BeLessThan(def);
    }

    // ── Aggregate confidence score ──────────────────────────────────────

    [Fact]
    public void Empty_attestations_produce_zero_score()
    {
        var score = ConfidenceScoreCalculator.ComputeScore([]);

        score.FinalValue.Should().Be(0.0);
    }

    [Fact]
    public void Single_attestation_score_equals_its_weight()
    {
        var att = new Attestation("alpha-3e", "alpha-3e.BIO.007", "T1", 2019, 6, 0.785, null);

        var score = ConfidenceScoreCalculator.ComputeScore([att], currentYear: 2026);

        score.FinalValue.Should().BeApproximately(0.785, 0.02);
    }

    [Fact]
    public void Multiple_attestations_produce_averaged_score()
    {
        var att1 = new Attestation("alpha-3e", "p.001", "T1", 2019, 6, 0, null);
        var att2 = new Attestation("beta-2e", "s.001", "T1", 2022, 2, 0, null);

        var score = ConfidenceScoreCalculator.ComputeScore([att1, att2], currentYear: 2026);

        // Average of two weights, both should be reasonable
        score.FinalValue.Should().BeGreaterThan(0.5);
        score.FinalValue.Should().BeLessThan(1.0);
    }

    [Fact]
    public void Score_decomposes_base_decay_and_bonus()
    {
        var att = new Attestation("alpha-3e", "p.001", "T1", 2019, 6, 0, null);

        var score = ConfidenceScoreCalculator.ComputeScore([att], currentYear: 2026);

        score.BaseAuthoritySum.Should().BeApproximately(1.0, 0.001);
        score.DecayPenalty.Should().BeGreaterThan(0, "7-year-old book should have decay penalty");
        score.SurvivalBonus.Should().BeGreaterThan(0, "6-edition claim should have survival bonus");
    }
}
