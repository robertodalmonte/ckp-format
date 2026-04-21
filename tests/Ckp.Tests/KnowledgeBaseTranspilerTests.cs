namespace Ckp.Tests;

using System.Text.RegularExpressions;
using Ckp.Core;
using Ckp.IO;
using Ckp.Transpiler;

/// <summary>
/// Tests the transpiler against the hand-built MiniKb fixture in
/// <c>TestData/MiniKb/</c>. The fixture is intentionally small and covers every
/// transpiler code path (mechanisms, traditions, observations, integrations).
/// </summary>
/// <remarks>
/// Expected MiniKb shape:
/// <list type="bullet">
///   <item>3 mechanism claims (FAS, MCT, ANS) — all tier 1</item>
///   <item>2 tradition claims (TCM) — tier 4, shared evidence, tradition signatures</item>
///   <item>1 observation claim (OBS) — tier 4</item>
///   <item>1 bridge (cl-tcm-001 → cl-fas-001), 1 connection (cl-fas-001 → cl-mct-001), 2 transitions on cl-ans-001</item>
/// </list>
/// </remarks>
public sealed class KnowledgeBaseTranspilerTests
{
    private static readonly string KbPath = Path.Combine(
        AppContext.BaseDirectory, "TestData", "MiniKb");

    private static readonly Lazy<Task<CkpPackage>> CachedPackage = new(
        () => new KnowledgeBaseTranspiler(KbPath).TranspileAsync());

    private static Task<CkpPackage> GetPackageAsync() => CachedPackage.Value;

    // ── Claim counts ──

    [Fact]
    public async Task Transpiler_produces_6_claims()
    {
        var package = await GetPackageAsync();
        package.Claims.Should().HaveCount(6);
    }

    [Fact]
    public async Task Transpiler_produces_3_T1_mechanism_claims()
    {
        var package = await GetPackageAsync();
        package.Claims.Count(c => c.Tier == Tier.T1).Should().Be(3);
    }

    [Fact]
    public async Task Transpiler_produces_3_T4_tradition_or_observation_claims()
    {
        var package = await GetPackageAsync();
        package.Claims.Count(c => c.Tier == Tier.T4).Should().Be(3);
    }

    // ── Claim ID format ──

    [Fact]
    public async Task All_claim_IDs_match_CKP_regex()
    {
        var package = await GetPackageAsync();
        var pattern = new Regex(@"^[a-z0-9]+-[a-z0-9]+\.[A-Z]{2,4}\.\d{3}$");
        foreach (var claim in package.Claims)
            claim.Id.Should().MatchRegex(pattern, $"Claim ID '{claim.Id}' does not match CKP format");
    }

    [Fact]
    public async Task All_claim_IDs_are_unique()
    {
        var package = await GetPackageAsync();
        var ids = package.Claims.Select(c => c.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    // ── Hashes ──

    [Fact]
    public async Task All_hashes_start_with_sha256_prefix()
    {
        var package = await GetPackageAsync();
        foreach (var claim in package.Claims)
            claim.Hash.Should().StartWith("sha256:");
    }

    [Fact]
    public async Task All_hashes_are_unique()
    {
        var package = await GetPackageAsync();
        var hashes = package.Claims.Select(c => c.Hash).ToList();
        hashes.Should().OnlyHaveUniqueItems();
    }

    // ── Tiers ──

    [Fact]
    public async Task All_tiers_are_valid()
    {
        var package = await GetPackageAsync();
        foreach (var claim in package.Claims)
            claim.Tier.Should().BeOneOf(Tier.T1, Tier.T2, Tier.T3, Tier.T4);
    }

    // ── Domains ──

    [Fact]
    public async Task All_claims_have_non_empty_domain()
    {
        var package = await GetPackageAsync();
        foreach (var claim in package.Claims)
            claim.Domain.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Domain_index_matches_claims()
    {
        var package = await GetPackageAsync();
        var domainCounts = package.Claims.GroupBy(c => c.Domain)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var domain in package.Domains)
        {
            domainCounts.Should().ContainKey(domain.Name);
            domain.ClaimCount.Should().Be(domainCounts[domain.Name]);
        }
    }

    [Fact]
    public async Task Domain_tier_counts_are_consistent()
    {
        var package = await GetPackageAsync();
        foreach (var domain in package.Domains)
        {
            var domainClaims = package.Claims.Where(c => c.Domain == domain.Name).ToList();
            domain.T1Count.Should().Be(domainClaims.Count(c => c.Tier == Tier.T1));
            domain.T2Count.Should().Be(domainClaims.Count(c => c.Tier == Tier.T2));
            domain.T3Count.Should().Be(domainClaims.Count(c => c.Tier == Tier.T3));
            domain.T4Count.Should().Be(domainClaims.Count(c => c.Tier == Tier.T4));
        }
    }

    // ── Manifest / fingerprint ──

    [Fact]
    public async Task Manifest_fingerprint_matches_actual_counts()
    {
        var package = await GetPackageAsync();
        var fp = package.Manifest.ContentFingerprint;
        fp.ClaimCount.Should().Be(package.Claims.Count);
        fp.DomainCount.Should().Be(package.Domains.Count);
        fp.CitationCount.Should().Be(package.Citations.Count);
        fp.T1Count.Should().Be(package.Claims.Count(c => c.Tier == Tier.T1));
        fp.T4Count.Should().Be(package.Claims.Count(c => c.Tier == Tier.T4));
    }

    [Fact]
    public async Task Manifest_book_metadata_is_correct()
    {
        var package = await GetPackageAsync();
        package.Manifest.Book.Key.Should().Be("minikb-1e");
        package.Manifest.Book.Title.Should().Be("Mini Test Knowledge Base");
        package.Manifest.Book.Edition.Should().Be(1);
        package.Manifest.Book.Language.Should().Be("en");
        package.Manifest.FormatVersion.Should().Be("1.0");
    }

    // ── Citations ──

    [Fact]
    public async Task Citations_are_present_and_have_valid_refs()
    {
        var package = await GetPackageAsync();
        package.Citations.Should().NotBeEmpty();
        package.Citations.Where(c => c.Ref.StartsWith("PMID:")).Should().NotBeEmpty();
        foreach (var citation in package.Citations)
            citation.Ref.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Citations_reference_existing_claims()
    {
        var package = await GetPackageAsync();
        var claimIds = package.Claims.Select(c => c.Id).ToHashSet();
        foreach (var citation in package.Citations)
        foreach (var refBy in citation.ReferencedBy)
            claimIds.Should().Contain(refBy);
    }

    // ── Evidence references ──

    [Fact]
    public async Task Mechanism_claims_have_citation_evidence()
    {
        var package = await GetPackageAsync();
        var mechanismClaims = package.Claims.Where(c => c.Tier == Tier.T1);
        foreach (var claim in mechanismClaims)
        {
            claim.Evidence.Where(e => e.Type == EvidenceReferenceType.Citation)
                .Should().NotBeEmpty($"T1 claim {claim.Id} should have citation evidence");
        }
    }

    // ── Tier history from transitions ──

    [Fact]
    public async Task Claims_with_transitions_have_tier_history()
    {
        var package = await GetPackageAsync();
        var ansClaim = package.Claims.First(c => c.Id == "minikb-1e.ANS.001");
        ansClaim.TierHistory.Should().HaveCount(2);
    }

    // ── Internal references (bridges + connections) ──

    [Fact]
    public async Task Bridge_claims_have_internal_refs()
    {
        var package = await GetPackageAsync();
        // cl-tcm-001 → minikb-1e.TCM.001 is bridged to cl-fas-001 (minikb-1e.FAS.001)
        var tcmClaim = package.Claims.First(c => c.Id == "minikb-1e.TCM.001");
        tcmClaim.Evidence.Should().Contain(e =>
            e.Type == EvidenceReferenceType.InternalRef &&
            e.Ref == "minikb-1e.FAS.001");
    }

    [Fact]
    public async Task Connection_claims_have_internal_refs()
    {
        var package = await GetPackageAsync();
        // cl-fas-001 has a connection to cl-mct-001
        var fasClaim = package.Claims.First(c => c.Id == "minikb-1e.FAS.001");
        fasClaim.Evidence.Should().Contain(e =>
            e.Type == EvidenceReferenceType.InternalRef &&
            e.Ref == "minikb-1e.MCT.001");
    }

    // ── Observables ──

    [Fact]
    public async Task Claims_with_falsification_criteria_have_observables()
    {
        var package = await GetPackageAsync();
        foreach (var claim in package.Claims)
            claim.Observables.Should().NotBeEmpty($"Claim {claim.Id} should have at least one observable");
    }

    // ── Round-trip: transpile → write → read ──

    [Fact]
    public async Task Package_survives_write_then_read_round_trip()
    {
        var ct = TestContext.Current.CancellationToken;
        var package = await GetPackageAsync();
        var writer = new CkpPackageWriter();
        var reader = new CkpPackageReader();

        using var ms = new MemoryStream();
        await writer.WriteAsync(package, ms, ct);
        ms.Position = 0;
        var roundTripped = await reader.ReadAsync(ms, ct);

        roundTripped.Claims.Should().HaveCount(package.Claims.Count);
        roundTripped.Citations.Should().HaveCount(package.Citations.Count);
        roundTripped.Domains.Should().HaveCount(package.Domains.Count);
        roundTripped.Manifest.Book.Key.Should().Be("minikb-1e");
    }

    [Fact]
    public async Task Round_trip_preserves_claim_hashes()
    {
        var ct = TestContext.Current.CancellationToken;
        var package = await GetPackageAsync();
        var writer = new CkpPackageWriter();
        var reader = new CkpPackageReader();

        using var ms = new MemoryStream();
        await writer.WriteAsync(package, ms, ct);
        ms.Position = 0;
        var roundTripped = await reader.ReadAsync(ms, ct);

        foreach (var original in package.Claims)
        {
            var restored = roundTripped.Claims.First(c => c.Id == original.Id);
            restored.Hash.Should().Be(original.Hash);
            restored.Statement.Should().Be(original.Statement);
        }
    }

    [Fact]
    public async Task Round_trip_preserves_tier_history()
    {
        var ct = TestContext.Current.CancellationToken;
        var package = await GetPackageAsync();
        var writer = new CkpPackageWriter();
        var reader = new CkpPackageReader();

        using var ms = new MemoryStream();
        await writer.WriteAsync(package, ms, ct);
        ms.Position = 0;
        var roundTripped = await reader.ReadAsync(ms, ct);

        var original = package.Claims.First(c => c.Id == "minikb-1e.ANS.001");
        var restored = roundTripped.Claims.First(c => c.Id == "minikb-1e.ANS.001");
        restored.TierHistory.Should().HaveCount(original.TierHistory.Count);
    }
}
