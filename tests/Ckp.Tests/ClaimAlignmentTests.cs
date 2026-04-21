namespace Ckp.Tests;

using Ckp.Core;

public sealed class ClaimAlignmentTests
{
    [Fact]
    public void Equivalent_alignment_with_tier_mismatch()
    {
        var mismatch = new TierMismatch(Tier.T1, Tier.T2, TierMismatchDirection.SourceAhead);
        var bridge = new VocabularyBridge(
            SourceTerms: ["baroreceptor stretch"],
            TargetTerms: ["fascial mechanoreceptor"],
            SharedConcept: "mechanical deformation → autonomic modulation");

        var alignment = new ClaimAlignment(
            SourceClaim: "delta-14e.ANS.047",
            TargetClaim: "gamma-2e.FAS.112",
            Type: AlignmentType.Equivalent,
            Confidence: 0.85,
            Mismatch: mismatch,
            Bridge: bridge,
            AlignedBy: "consilience-auto",
            ReviewedBy: null,
            Note: null);

        alignment.SourceClaim.Should().Be("delta-14e.ANS.047");
        alignment.TargetClaim.Should().Be("gamma-2e.FAS.112");
        alignment.Type.Should().Be(AlignmentType.Equivalent);
        alignment.Confidence.Should().Be(0.85);
        alignment.Mismatch!.Direction.Should().Be(TierMismatchDirection.SourceAhead);
        alignment.Bridge!.SharedConcept.Should().Contain("autonomic modulation");
    }

    [Fact]
    public void Unmatched_alignment_has_null_target()
    {
        var alignment = new ClaimAlignment(
            SourceClaim: "delta-14e.ANS.112",
            TargetClaim: null,
            Type: AlignmentType.Unmatched,
            Confidence: null,
            Mismatch: null,
            Bridge: null,
            AlignedBy: "consilience-auto",
            ReviewedBy: null,
            Note: "No equivalent in Gamma.");

        alignment.TargetClaim.Should().BeNull();
        alignment.Type.Should().Be(AlignmentType.Unmatched);
        alignment.Note.Should().Contain("Gamma");
    }

    [Fact]
    public void BookAlignment_groups_alignments_between_two_books()
    {
        var alignments = new List<ClaimAlignment>
        {
            new("a.001", "b.001", AlignmentType.Equivalent, 0.9, null, null, "auto", null, null),
            new("a.002", null, AlignmentType.Unmatched, null, null, null, "auto", null, "No match")
        };

        var bookAlignment = new BookAlignment("book-a", "book-b", alignments);

        bookAlignment.SourceBook.Should().Be("book-a");
        bookAlignment.TargetBook.Should().Be("book-b");
        bookAlignment.Alignments.Should().HaveCount(2);
    }

    [Fact]
    public void Contradictory_alignment_captures_disagreement()
    {
        var alignment = new ClaimAlignment(
            SourceClaim: "a.001",
            TargetClaim: "b.001",
            Type: AlignmentType.Contradictory,
            Confidence: 0.72,
            Mismatch: new TierMismatch(Tier.T1, Tier.T3, TierMismatchDirection.SourceAhead),
            Bridge: null,
            AlignedBy: "manual",
            ReviewedBy: "expert-1",
            Note: "Source accepts mechanism; target disputes pathway.");

        alignment.Type.Should().Be(AlignmentType.Contradictory);
        alignment.ReviewedBy.Should().Be("expert-1");
    }
}
