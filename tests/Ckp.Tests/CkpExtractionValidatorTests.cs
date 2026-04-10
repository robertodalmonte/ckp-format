namespace Ckp.Tests;

using Ckp.Core;
using Ckp.IO;

public sealed class CkpExtractionValidatorTests
{
    private readonly CkpExtractionValidator _validator = new(TestExtractionVocabulary.Build());

    // ── S1: Hash format ─────────────────────────────────────────────────

    [Fact]
    public void S1_placeholder_hash_is_error()
    {
        var package = PackageWith(ClaimWith(hash: "sha256:placeholder-001"));

        var report = _validator.Validate(package);

        report.IsValid.Should().BeFalse();
        report.Diagnostics.Should().Contain(d => d.RuleId == "S1" && d.Severity == ClaimValidationSeverity.Error);
    }

    [Fact]
    public void S1_valid_hash_passes()
    {
        var package = PackageWith(ValidClaim());

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "S1");
    }

    // ── S2: Hash integrity ──────────────────────────────────────────────

    [Fact]
    public void S2_mismatched_hash_is_error()
    {
        var claim = PackageClaim.Restore(
            id: "test-1e.BIO.001",
            statement: "Original statement.",
            tier: "T1",
            domain: "mechanotransduction",
            subDomain: null, chapter: 1, section: null, pageRange: null,
            keywords: [], meshTerms: [], evidence: [], observables: [],
            sinceEdition: 1,
            tierHistory: [new TierHistoryEntry(1, "T1", null)],
            hash: "sha256:0000000000000000000000000000000000000000000000000000000000000000");

        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.IsValid.Should().BeFalse();
        report.Diagnostics.Should().Contain(d => d.RuleId == "S2");
    }

    // ── S3: Valid tier ───────────────────────────────────────────────────

    [Fact]
    public void S3_invalid_tier_is_error()
    {
        var package = PackageWith(ClaimWith(tier: "T0"));

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d => d.RuleId == "S3");
    }

    [Theory]
    [InlineData("T1")]
    [InlineData("T2")]
    [InlineData("T3")]
    [InlineData("T4")]
    public void S3_valid_tiers_pass(string tier)
    {
        var package = PackageWith(ClaimWith(tier: tier));

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "S3");
    }

    // ── S4: ID format ───────────────────────────────────────────────────

    [Fact]
    public void S4_invalid_id_format_is_error()
    {
        var package = PackageWith(ClaimWith(id: "bad_id"));

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d => d.RuleId == "S4");
    }

    [Theory]
    [InlineData("alpha-3e.BIO.007")]
    [InlineData("beta-2e.FAS.001")]
    [InlineData("delta-14e.ANS.047")]
    public void S4_valid_ids_pass(string id)
    {
        var package = PackageWith(ClaimWith(id: id));

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "S4");
    }

    // ── SET1: Unique IDs ────────────────────────────────────────────────

    [Fact]
    public void SET1_duplicate_ids_is_error()
    {
        var claim1 = ClaimWith(id: "test-1e.BIO.001", statement: "First claim.");
        var claim2 = ClaimWith(id: "test-1e.BIO.001", statement: "Different claim.");
        var package = PackageWith(claim1, claim2);

        var report = _validator.Validate(package);

        report.IsValid.Should().BeFalse();
        report.Diagnostics.Should().Contain(d => d.RuleId == "SET1");
    }

    // ── SET2: Unique hashes ─────────────────────────────────────────────

    [Fact]
    public void SET2_duplicate_statements_is_error()
    {
        var claim1 = ClaimWith(id: "test-1e.BIO.001", statement: "Same statement.");
        var claim2 = ClaimWith(id: "test-1e.BIO.002", statement: "Same statement.");
        var package = PackageWith(claim1, claim2);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d => d.RuleId == "SET2");
    }

    // ── SET3: Internal references resolve ────────────────────────────────

    [Fact]
    public void SET3_dangling_internal_ref_is_error()
    {
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceReferenceType.InternalRef, "test-1e.BIO.999", EvidenceRelationship.Supports, null, null)
        };
        var claim = ClaimWith(evidence: evidence);
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.IsValid.Should().BeFalse();
        report.Diagnostics.Should().Contain(d => d.RuleId == "SET3");
    }

    [Fact]
    public void SET3_valid_internal_ref_passes()
    {
        var claim1 = ClaimWith(id: "test-1e.BIO.001", statement: "Base claim.");
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceReferenceType.InternalRef, "test-1e.BIO.001", EvidenceRelationship.Supports, null, null)
        };
        var claim2 = ClaimWith(id: "test-1e.BIO.002", statement: "Dependent claim.", evidence: evidence);
        var package = PackageWith(claim1, claim2);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "SET3");
    }

    // ── SET5: Manifest counts ───────────────────────────────────────────

    [Fact]
    public void SET5_mismatched_claim_count_is_error()
    {
        var claim = ValidClaim();
        // Manifest says 5 claims, package has 1
        var book = TestBook();
        var fp = new ContentFingerprint("SHA-256", 5, 1, 1, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        var package = new CkpPackage(manifest, [claim], [], [], [], [], [], [], [], [], [], [], []);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d => d.RuleId == "SET5");
    }

    // ── PC1: Citation required for T1/T2 ────────────────────────────────

    [Fact]
    public void PC1_t1_without_citation_is_error_when_priorities_present()
    {
        var claim = ClaimWith(tier: "T1", evidence: []);
        var package = PackageWith(claim);
        var priorities = new Dictionary<string, ExtractionPriority>
        {
            [claim.Id] = ExtractionPriority.Mechanistic
        };

        var report = _validator.Validate(package, priorities);

        report.Diagnostics.Should().Contain(d => d.RuleId == "PC1");
    }

    [Fact]
    public void PC1_t1_with_citation_passes()
    {
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceReferenceType.Citation, "PMID:12345678", EvidenceRelationship.Supports, EvidenceStrength.Primary, null)
        };
        var claim = ClaimWith(tier: "T1", evidence: evidence);
        var package = PackageWith(claim);
        var priorities = new Dictionary<string, ExtractionPriority>
        {
            [claim.Id] = ExtractionPriority.Mechanistic
        };

        var report = _validator.Validate(package, priorities);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "PC1");
    }

    [Fact]
    public void PC1_not_checked_when_no_priorities()
    {
        var claim = ClaimWith(tier: "T1", evidence: []);
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "PC1");
    }

    // ── PC2: Observable required for P0/P1 ──────────────────────────────

    [Fact]
    public void PC2_mechanistic_without_observable_is_error()
    {
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceReferenceType.Citation, "PMID:12345678", EvidenceRelationship.Supports, EvidenceStrength.Primary, null)
        };
        var claim = ClaimWith(evidence: evidence, observables: []);
        var package = PackageWith(claim);
        var priorities = new Dictionary<string, ExtractionPriority>
        {
            [claim.Id] = ExtractionPriority.Mechanistic
        };

        var report = _validator.Validate(package, priorities);

        report.Diagnostics.Should().Contain(d => d.RuleId == "PC2");
    }

    [Fact]
    public void PC2_epidemiological_without_observable_passes()
    {
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceReferenceType.Citation, "PMID:12345678", EvidenceRelationship.Supports, EvidenceStrength.Primary, null)
        };
        var claim = ClaimWith(evidence: evidence, observables: []);
        var package = PackageWith(claim);
        var priorities = new Dictionary<string, ExtractionPriority>
        {
            [claim.Id] = ExtractionPriority.Epidemiological
        };

        var report = _validator.Validate(package, priorities);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "PC2");
    }

    // ── SEM1: Tier-language mismatch ────────────────────────────────────

    [Theory]
    [InlineData("FAK appears to be the mechanoreceptor in PDL cells.")]
    [InlineData("This mechanism may play a role in bone remodeling.")]
    [InlineData("Preliminary evidence suggests that RANKL is upregulated.")]
    [InlineData("The pathway is not yet established in clinical settings.")]
    public void SEM1_hedging_in_t1_claim_is_warning(string statement)
    {
        var claim = ClaimWith(tier: "T1", statement: statement);
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d =>
            d.RuleId == "SEM1" && d.Severity == ClaimValidationSeverity.Warning);
    }

    [Fact]
    public void SEM1_hedging_in_t2_claim_does_not_fire()
    {
        var claim = ClaimWith(tier: "T2", statement: "FAK appears to be the mechanoreceptor.");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "SEM1");
    }

    [Fact]
    public void SEM1_definitive_t1_claim_does_not_fire()
    {
        var claim = ClaimWith(tier: "T1", statement: "Baroreceptor activation reduces heart rate within one cardiac cycle.");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "SEM1");
    }

    // ── SEM2: Compound statement ────────────────────────────────────────

    [Fact]
    public void SEM2_semicolon_separated_clauses_is_warning()
    {
        var claim = ClaimWith(statement: "FAK triggers PgE2 release; RANKL upregulation induces osteoclasts.");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d => d.RuleId == "SEM2");
    }

    // ── SEM3: Unknown domain ────────────────────────────────────────────

    [Fact]
    public void SEM3_unknown_domain_is_warning()
    {
        var claim = ClaimWith(domain: "underwater-basket-weaving");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d =>
            d.RuleId == "SEM3" && d.Severity == ClaimValidationSeverity.Warning);
    }

    [Fact]
    public void SEM3_known_domain_does_not_fire()
    {
        var claim = ClaimWith(domain: "mechanotransduction");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "SEM3");
    }

    // ── SEM4: Mechanistic claim without observables ─────────────────────

    [Fact]
    public void SEM4_mechanistic_keywords_without_observables_is_warning()
    {
        var claim = ClaimWith(
            statement: "FAK compression triggers RANKL-mediated osteoclast differentiation.",
            observables: []);
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d => d.RuleId == "SEM4");
    }

    [Fact]
    public void SEM4_mechanistic_keywords_with_observables_does_not_fire()
    {
        var observables = new List<Observable>
        {
            new("RANKL expression", "fold-change", "increase", "12-24 hours", "Western blot")
        };
        var claim = ClaimWith(
            statement: "FAK compression triggers RANKL-mediated osteoclast differentiation.",
            observables: observables);
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "SEM4");
    }

    // ── SEM5: Stale tier history ────────────────────────────────────────

    [Fact]
    public void SEM5_stale_tier_history_is_warning()
    {
        var tierHistory = new List<TierHistoryEntry> { new(3, "T1", "Old edition") };
        var claim = ClaimWith(tierHistory: tierHistory);
        // Book is edition 6 but tier history only goes to edition 3
        var book = new BookMetadata("test-1e", "Test", 6, ["Author"], "Pub", 2026, null, "en-US", []);
        var fp = new ContentFingerprint("SHA-256", 1, 1, 1, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        var package = new CkpPackage(manifest, [claim], [], [], [], [], [], [], [], [], [], [], []);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d => d.RuleId == "SEM5");
    }

    // ── SEM6: Epistemic rigidity ──────────────────────────────────────────

    [Fact]
    public void SEM6_t3_without_hedging_is_notice()
    {
        var claim = ClaimWith(tier: "T3",
            statement: "Cranial rhythmic impulse drives cerebrospinal fluid circulation.");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d =>
            d.RuleId == "SEM6" && d.Severity == ClaimValidationSeverity.Notice);
    }

    [Fact]
    public void SEM6_t4_without_hedging_is_notice()
    {
        var claim = ClaimWith(tier: "T4",
            statement: "Fascia was identified as connective tissue in the Sushruta Samhita.");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().Contain(d =>
            d.RuleId == "SEM6" && d.Severity == ClaimValidationSeverity.Notice);
    }

    [Fact]
    public void SEM6_t3_with_hedging_does_not_fire()
    {
        var claim = ClaimWith(tier: "T3",
            statement: "The sponge model appears to explain tissue hydration changes during manual therapy.");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "SEM6");
    }

    [Fact]
    public void SEM6_t1_without_hedging_does_not_fire()
    {
        var claim = ClaimWith(tier: "T1",
            statement: "Baroreceptor activation reduces heart rate within one cardiac cycle.");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.Diagnostics.Should().NotContain(d => d.RuleId == "SEM6");
    }

    // ── Notice severity in reports ──────────────────────────────────────

    [Fact]
    public void Report_with_only_notices_is_valid()
    {
        var claim = ClaimWith(tier: "T4",
            statement: "Fascia was identified as connective tissue in the Sushruta Samhita.");
        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.IsValid.Should().BeTrue();
        report.NoticeCount.Should().BeGreaterThan(0);
        report.ErrorCount.Should().Be(0);
    }

    // ── Valid package passes all checks ──────────────────────────────────

    [Fact]
    public void Valid_package_produces_clean_report()
    {
        var evidence = new List<EvidenceReference>
        {
            new(EvidenceReferenceType.Citation, "PMID:12345678", EvidenceRelationship.Supports, EvidenceStrength.Primary, null)
        };
        var observables = new List<Observable>
        {
            new("Heart rate decrease", "bpm", "decrease", "<1s", "ECG")
        };
        var tierHistory = new List<TierHistoryEntry> { new(1, "T1", "Established") };

        var claim = PackageClaim.CreateNew(
            id: "test-1e.BIO.001",
            statement: "Baroreceptor activation reduces heart rate within one cardiac cycle.",
            tier: "T1",
            domain: "autonomic-nervous-system",
            evidence: evidence,
            observables: observables,
            sinceEdition: 1,
            tierHistory: tierHistory);

        var book = new BookMetadata("test-1e", "Test", 1, ["Author"], "Pub", 2026, null, "en-US", []);
        var fp = new ContentFingerprint("SHA-256", 1, 1, 1, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        var citations = new List<CitationEntry>
        {
            new("PMID:12345678", "Study", "Author", 2020, "Journal", ["test-1e.BIO.001"])
        };
        var package = new CkpPackage(manifest, [claim], citations, [], [], [], [], [], [], [], [], [], []);

        var priorities = new Dictionary<string, ExtractionPriority>
        {
            [claim.Id] = ExtractionPriority.Mechanistic
        };

        var report = _validator.Validate(package, priorities);

        report.IsValid.Should().BeTrue();
        report.ErrorCount.Should().Be(0);
    }

    // ── Report aggregation ──────────────────────────────────────────────

    [Fact]
    public void Report_counts_errors_and_warnings_separately()
    {
        // S1 error (placeholder hash) + SEM3 warning (unknown domain)
        var claim = PackageClaim.Restore(
            id: "test-1e.BIO.001",
            statement: "Test claim.",
            tier: "T1",
            domain: "unknown-domain-xyz",
            subDomain: null, chapter: null, section: null, pageRange: null,
            keywords: [], meshTerms: [], evidence: [], observables: [],
            sinceEdition: null,
            tierHistory: [],
            hash: "sha256:placeholder");

        var package = PackageWith(claim);

        var report = _validator.Validate(package);

        report.IsValid.Should().BeFalse();
        report.ErrorCount.Should().BeGreaterThan(0);
        report.WarningCount.Should().BeGreaterThan(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static PackageClaim ValidClaim(string? id = null, string? statement = null) =>
        PackageClaim.CreateNew(
            id: id ?? "test-1e.BIO.001",
            statement: statement ?? "Baroreceptor activation reduces heart rate.",
            tier: "T1",
            domain: "autonomic-nervous-system",
            sinceEdition: 1,
            tierHistory: [new TierHistoryEntry(1, "T1", null)]);

    private static PackageClaim ClaimWith(
        string? id = null,
        string? statement = null,
        string? tier = null,
        string? domain = null,
        string? hash = null,
        IReadOnlyList<EvidenceReference>? evidence = null,
        IReadOnlyList<Observable>? observables = null,
        IReadOnlyList<TierHistoryEntry>? tierHistory = null)
    {
        string stmt = statement ?? "Test claim statement.";
        string computedHash = hash ?? ComputeHash(stmt);

        return PackageClaim.Restore(
            id: id ?? "test-1e.BIO.001",
            statement: stmt,
            tier: tier ?? "T1",
            domain: domain ?? "mechanotransduction",
            subDomain: null,
            chapter: 1,
            section: null,
            pageRange: null,
            keywords: [],
            meshTerms: [],
            evidence: evidence ?? [],
            observables: observables ?? [],
            sinceEdition: 1,
            tierHistory: tierHistory ?? [new TierHistoryEntry(1, tier ?? "T1", null)],
            hash: computedHash);
    }

    private static CkpPackage PackageWith(params PackageClaim[] claims)
    {
        int t1 = claims.Count(c => c.Tier == "T1");
        int t2 = claims.Count(c => c.Tier == "T2");
        int t3 = claims.Count(c => c.Tier == "T3");
        int t4 = claims.Count(c => c.Tier == "T4");

        var book = TestBook();
        var fp = new ContentFingerprint("SHA-256", claims.Length, 1, t1, t2, t3, t4, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        return new CkpPackage(manifest, claims, [], [], [], [], [], [], [], [], [], [], []);
    }

    private static BookMetadata TestBook() =>
        new("test-1e", "Test", 1, ["Author"], "Pub", 2026, null, "en-US", []);

    private static string ComputeHash(string statement)
    {
        byte[] bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(statement));
        return $"sha256:{Convert.ToHexStringLower(bytes)}";
    }
}
