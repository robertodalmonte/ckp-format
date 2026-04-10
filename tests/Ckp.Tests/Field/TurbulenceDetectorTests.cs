namespace Ckp.Tests.Field;

using Ckp.Core.Field;

public sealed class TurbulenceDetectorTests
{
    // ── Core threshold logic ────────────────────────────────────────────

    [Fact]
    public void Recent_authoritative_source_one_tier_gap_triggers_turbulence()
    {
        // Recent 2024 book (weight=0.87) vs consensus mean 0.62 → ratio 1.40 > 0.70
        var consensus = new Attestation[]
        {
            new("alpha-3e", "p.001", "T1", 2019, 6, 0.79, null),
            new("nanda-3e",   "n.001", "T1", 2010, 3, 0.49, null),
            new("burstone",   "b.001", "T1", 2015, 2, 0.58, null)
        };
        var dissent = new Attestation("recent-2024", "r.001", "T2", 2024, 1, 0.87, null);

        var flag = TurbulenceDetector.Evaluate(dissent, consensus, "T1");

        flag.Should().NotBeNull();
        flag!.Direction.Should().Be(TurbulenceDirection.Demotion);
        flag.TierDelta.Should().Be(1);
        flag.TriggeredByBookId.Should().Be("recent-2024");
    }

    [Fact]
    public void Old_low_weight_source_one_tier_gap_does_not_trigger()
    {
        // Old 2005 book (weight=0.28) vs consensus mean 0.62 → ratio 0.45 < 0.70
        var consensus = new Attestation[]
        {
            new("alpha-3e", "p.001", "T1", 2019, 6, 0.79, null),
            new("nanda-3e",   "n.001", "T1", 2010, 3, 0.49, null),
            new("burstone",   "b.001", "T1", 2015, 2, 0.58, null)
        };
        var dissent = new Attestation("old-2005", "o.001", "T2", 2005, 1, 0.28, null);

        var flag = TurbulenceDetector.Evaluate(dissent, consensus, "T1");

        flag.Should().BeNull();
    }

    [Fact]
    public void Old_source_two_tier_gap_triggers_due_to_lower_threshold()
    {
        // Old 2005 book (weight=0.28) vs consensus mean 0.62 → ratio 0.45 > 0.35 (τ/2)
        var consensus = new Attestation[]
        {
            new("alpha-3e", "p.001", "T1", 2019, 6, 0.79, null),
            new("nanda-3e",   "n.001", "T1", 2010, 3, 0.49, null),
            new("burstone",   "b.001", "T1", 2015, 2, 0.58, null)
        };
        var dissent = new Attestation("old-2005", "o.001", "T3", 2005, 1, 0.28, null);

        var flag = TurbulenceDetector.Evaluate(dissent, consensus, "T1");

        flag.Should().NotBeNull();
        flag!.TierDelta.Should().Be(2);
        flag.Direction.Should().Be(TurbulenceDirection.Demotion);
    }

    // ── Direction detection ─────────────────────────────────────────────

    [Fact]
    public void Promotion_detected_when_dissent_is_higher_tier()
    {
        var consensus = new Attestation[]
        {
            new("book-a", "a.001", "T3", 2015, 1, 0.50, null)
        };
        var dissent = new Attestation("book-b", "b.001", "T1", 2024, 1, 0.87, null);

        var flag = TurbulenceDetector.Evaluate(dissent, consensus, "T3");

        flag.Should().NotBeNull();
        flag!.Direction.Should().Be(TurbulenceDirection.Promotion);
        flag.TierDelta.Should().Be(2);
    }

    // ── Edge cases ──────────────────────────────────────────────────────

    [Fact]
    public void Same_tier_never_triggers()
    {
        var consensus = new Attestation[]
        {
            new("book-a", "a.001", "T1", 2019, 1, 0.67, null)
        };
        var dissent = new Attestation("book-b", "b.001", "T1", 2024, 1, 0.87, null);

        var flag = TurbulenceDetector.Evaluate(dissent, consensus, "T1");

        flag.Should().BeNull();
    }

    [Fact]
    public void Empty_consensus_returns_null()
    {
        var dissent = new Attestation("book-a", "a.001", "T2", 2024, 1, 0.87, null);

        var flag = TurbulenceDetector.Evaluate(dissent, [], "T1");

        flag.Should().BeNull();
    }

    [Fact]
    public void Custom_tau_base_changes_threshold()
    {
        var consensus = new Attestation[]
        {
            new("book-a", "a.001", "T1", 2019, 1, 0.67, null)
        };
        // Weight 0.40, ratio = 0.40/0.67 ≈ 0.60
        var dissent = new Attestation("book-b", "b.001", "T2", 2015, 1, 0.40, null);

        // Default τ=0.7: ratio 0.60 < 0.70 → no turbulence
        var flag1 = TurbulenceDetector.Evaluate(dissent, consensus, "T1", tauBase: 0.7);
        flag1.Should().BeNull();

        // Lower τ=0.5: ratio 0.60 > 0.50 → turbulence
        var flag2 = TurbulenceDetector.Evaluate(dissent, consensus, "T1", tauBase: 0.5);
        flag2.Should().NotBeNull();
    }

    // ── Three-tier gap ──────────────────────────────────────────────────

    [Fact]
    public void Three_tier_gap_has_very_low_threshold()
    {
        var consensus = new Attestation[]
        {
            new("book-a", "a.001", "T1", 2019, 1, 0.67, null)
        };
        // Weight 0.20, ratio = 0.20/0.67 ≈ 0.30 > 0.23 (τ/3)
        var dissent = new Attestation("book-b", "b.001", "T4", 2010, 1, 0.20, null);

        var flag = TurbulenceDetector.Evaluate(dissent, consensus, "T1");

        flag.Should().NotBeNull();
        flag!.TierDelta.Should().Be(3);
    }
}
