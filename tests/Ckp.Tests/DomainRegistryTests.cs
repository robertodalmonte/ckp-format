namespace Ckp.Tests;

using Ckp.Transpiler;

public sealed class DomainRegistryTests
{
    [Theory]
    [InlineData("cl-ans-001", "ANS")]
    [InlineData("cl-fas-001", "FAS")]
    [InlineData("cl-tcm-004", "TCM")]
    [InlineData("cl-mct-002", "MCT")]
    [InlineData("cl-yog-008", "YOG")]
    [InlineData("cl-hom-005", "HOM")]
    public void ExtractDomainCode_parses_correctly(string claimId, string expectedCode)
    {
        DomainRegistry.ExtractDomainCode(claimId).Should().Be(expectedCode);
    }

    [Theory]
    [InlineData("cl-ans-001", 1)]
    [InlineData("cl-fas-003", 3)]
    [InlineData("cl-tcm-006", 6)]
    public void ExtractSequence_parses_correctly(string claimId, int expectedSeq)
    {
        DomainRegistry.ExtractSequence(claimId).Should().Be(expectedSeq);
    }

    [Theory]
    [InlineData("cl-ans-001", "consilience-v1", "consilience-v1.ANS.001")]
    [InlineData("cl-fas-003", "consilience-v1", "consilience-v1.FAS.003")]
    [InlineData("cl-tcm-006", "test-1e", "test-1e.TCM.006")]
    public void ToCkpClaimId_produces_correct_format(string kbId, string bookKey, string expected)
    {
        DomainRegistry.ToCkpClaimId(kbId, bookKey).Should().Be(expected);
    }

    [Theory]
    [InlineData("ANS", "ans")]
    [InlineData("FAS", "fas")]
    [InlineData("TCM", "tcm")]
    [InlineData("xyz", "xyz")]
    public void ToDomainName_returns_lowercase_code(string code, string expectedName)
    {
        DomainRegistry.ToDomainName(code).Should().Be(expectedName);
    }

    [Fact]
    public void ExtractDomainCode_throws_on_invalid_id()
    {
        var act = () => DomainRegistry.ExtractDomainCode("invalid-id");
        act.Should().Throw<InvalidOperationException>();
    }
}
