namespace Ckp.IO;

using Ckp.Core;
using Ckp.Core.Field;

/// <summary>
/// The CKP 2.0 compiler. Takes CKP 1.0 source packages and alignment proposals,
/// produces a compiled <see cref="FieldPackage"/>. Handles:
/// <list type="bullet">
/// <item>Consensus tier computation (weight-adjusted, not majority vote)</item>
/// <item>Confidence scoring (λ=0.058, α=0.1)</item>
/// <item>Turbulence detection (τ_base=0.7, scaled by tier delta)</item>
/// <item>Status classification (Frontier/Converged/Divergent)</item>
/// <item>T0 constraint back-propagation</item>
/// <item>Vocabulary map assembly</item>
/// </list>
/// </summary>
public sealed class FieldPackageCompiler : IFieldPackageCompiler
{
    private const double Lambda = ConfidenceScoreCalculator.DefaultLambda;
    private const double Alpha = ConfidenceScoreCalculator.DefaultAlpha;
    private const double TauBase = TurbulenceDetector.DefaultTauBase;

    public CompilationResult Compile(
        string fieldId,
        string version,
        IReadOnlyList<CkpPackage> packages,
        IReadOnlyList<AlignmentProposal> proposals,
        IReadOnlyDictionary<string, double>? bookAuthorities = null,
        double autoMergeThreshold = 0.7)
    {
        int currentYear = DateTimeOffset.UtcNow.Year;
        var claimIndex = BuildClaimIndex(packages);

        var autoMerged = new List<AlignmentProposal>();
        var reviewNeeded = new List<AlignmentProposal>();

        foreach (var proposal in proposals)
        {
            if (proposal.Score >= autoMergeThreshold)
                autoMerged.Add(proposal);
            else
                reviewNeeded.Add(proposal);
        }

        // Build canonical claims from auto-merged proposals
        var mergedClaimIds = new HashSet<string>();
        var canonicalClaims = new List<CanonicalClaim>();

        foreach (var proposal in autoMerged)
        {
            if (!claimIndex.TryGetValue(proposal.SourceClaimId, out var srcEntry)) continue;
            if (!claimIndex.TryGetValue(proposal.TargetClaimId, out var tgtEntry)) continue;

            var (srcClaim, srcBook) = srcEntry;
            var (tgtClaim, tgtBook) = tgtEntry;

            mergedClaimIds.Add(proposal.SourceClaimId);
            mergedClaimIds.Add(proposal.TargetClaimId);

            var srcAtt = BuildAttestation(srcClaim, srcBook, bookAuthorities, currentYear);
            var tgtAtt = BuildAttestation(tgtClaim, tgtBook, bookAuthorities, currentYear);
            var attestations = new List<Attestation> { srcAtt, tgtAtt };

            var vocabularyMap = BuildVocabularyMap(srcClaim, tgtClaim, srcBook, tgtBook);
            var t0Constraints = CollectT0Constraints(srcClaim, tgtClaim);
            var confidence = ConfidenceScoreCalculator.ComputeScore(
                attestations, bookAuthorities, currentYear: currentYear);

            if (proposal.IsContradiction)
            {
                canonicalClaims.Add(BuildDivergentClaim(
                    proposal, srcClaim, tgtClaim, srcAtt, tgtAtt,
                    confidence, vocabularyMap, t0Constraints));
            }
            else
            {
                string consensusTier = ComputeConsensusTier(attestations);
                var turbulence = DetectTurbulence(attestations, consensusTier);

                canonicalClaims.Add(new CanonicalClaim(
                    CanonicalId: proposal.ProposedCanonicalId,
                    Status: ClaimStatus.Converged,
                    Statement: srcClaim.Statement,
                    ConsensusTier: consensusTier,
                    Confidence: confidence,
                    Attestations: attestations,
                    VocabularyMap: vocabularyMap,
                    T0Constraints: t0Constraints,
                    Turbulence: turbulence,
                    Branches: null));
            }
        }

        // Add frontier claims (unaligned — single attestation)
        int frontierCount = 0;
        foreach (var package in packages)
        {
            var bookMeta = package.Manifest.Book;
            foreach (var claim in package.Claims)
            {
                if (mergedClaimIds.Contains(claim.Id)) continue;

                var att = BuildAttestation(claim, bookMeta, bookAuthorities, currentYear);
                var confidence = ConfidenceScoreCalculator.ComputeScore(
                    [att], bookAuthorities, currentYear: currentYear);

                string canonicalId = $"ckp:{fieldId}:{claim.Domain}:{claim.SubDomain ?? "general"}";

                canonicalClaims.Add(new CanonicalClaim(
                    CanonicalId: canonicalId,
                    Status: ClaimStatus.Frontier,
                    Statement: claim.Statement,
                    ConsensusTier: claim.Tier,
                    Confidence: confidence,
                    Attestations: [att],
                    VocabularyMap: new Dictionary<string, string>
                    {
                        [bookMeta.Key] = ExtractKeyTerms(claim)
                    },
                    T0Constraints: CollectT0Constraints(claim),
                    Turbulence: null,
                    Branches: null));

                frontierCount++;
            }
        }

        var sourcePackages = packages.Select(p => p.Manifest.Book.Key).ToList();

        var fieldPackage = new FieldPackage(
            FieldId: fieldId,
            Version: version,
            CompiledAt: DateTimeOffset.UtcNow,
            SourcePackages: sourcePackages,
            Claims: canonicalClaims,
            DecayLambda: Lambda,
            SurvivalAlpha: Alpha,
            TurbulenceTauBase: TauBase);

        return new CompilationResult(fieldPackage, autoMerged.Count, frontierCount, reviewNeeded);
    }

    private static Attestation BuildAttestation(
        PackageClaim claim,
        BookMetadata book,
        IReadOnlyDictionary<string, double>? authorities,
        int currentYear)
    {
        double baseAuth = authorities is not null && authorities.TryGetValue(book.Key, out var ba) ? ba : 1.0;
        int editionsSurvived = claim.TierHistory.Count > 0
            ? claim.TierHistory.Select(t => t.Edition).Distinct().Count()
            : 1;

        double weight = ConfidenceScoreCalculator.ComputeWeight(
            baseAuth, book.Year, currentYear, editionsSurvived);

        return new Attestation(
            BookId: book.Key,
            ClaimId: claim.Id,
            Tier: claim.Tier,
            PublicationYear: book.Year,
            EditionsSurvived: editionsSurvived,
            Weight: weight,
            Note: null);
    }

    private static string ComputeConsensusTier(IReadOnlyList<Attestation> attestations)
    {
        // Weight-adjusted consensus: tier with highest total weight wins
        var tierWeights = attestations
            .GroupBy(a => a.Tier)
            .Select(g => (Tier: g.Key, TotalWeight: g.Sum(a => a.Weight)))
            .OrderByDescending(x => x.TotalWeight)
            .ToList();

        return tierWeights[0].Tier;
    }

    private static TurbulenceFlag? DetectTurbulence(
        IReadOnlyList<Attestation> attestations, string consensusTier)
    {
        if (attestations.Count < 2) return null;

        var consensus = attestations.Where(a => a.Tier == consensusTier).ToList();
        if (consensus.Count == 0) return null;

        foreach (var att in attestations)
        {
            if (att.Tier == consensusTier) continue;

            var flag = TurbulenceDetector.Evaluate(att, consensus, consensusTier);
            if (flag is not null) return flag;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> BuildVocabularyMap(
        PackageClaim source, PackageClaim target,
        BookMetadata srcBook, BookMetadata tgtBook)
    {
        return new Dictionary<string, string>
        {
            [srcBook.Key] = ExtractKeyTerms(source),
            [tgtBook.Key] = ExtractKeyTerms(target)
        };
    }

    private static string ExtractKeyTerms(PackageClaim claim)
    {
        if (claim.Keywords.Count > 0)
            return string.Join(", ", claim.Keywords.Take(5));
        return claim.SubDomain ?? claim.Domain;
    }

    private static IReadOnlyList<string> CollectT0Constraints(params PackageClaim[] claims)
    {
        return claims
            .SelectMany(c => c.Evidence)
            .Where(e => e.Type == EvidenceReferenceType.Axiom)
            .Select(e => e.Ref)
            .Distinct()
            .ToList();
    }

    private static CanonicalClaim BuildDivergentClaim(
        AlignmentProposal proposal,
        PackageClaim srcClaim, PackageClaim tgtClaim,
        Attestation srcAtt, Attestation tgtAtt,
        ConfidenceScore confidence,
        IReadOnlyDictionary<string, string> vocabularyMap,
        IReadOnlyList<string> t0Constraints)
    {
        var branches = new List<DivergentBranch>
        {
            new(srcClaim.Statement, srcClaim.Tier, [srcAtt]),
            new(tgtClaim.Statement, tgtClaim.Tier, [tgtAtt])
        };

        return new CanonicalClaim(
            CanonicalId: proposal.ProposedCanonicalId,
            Status: ClaimStatus.Divergent,
            Statement: $"Divergent positions on {srcClaim.Domain}:{srcClaim.SubDomain ?? "general"}",
            ConsensusTier: srcAtt.Weight >= tgtAtt.Weight ? srcClaim.Tier : tgtClaim.Tier,
            Confidence: confidence,
            Attestations: [srcAtt, tgtAtt],
            VocabularyMap: vocabularyMap,
            T0Constraints: t0Constraints,
            Turbulence: null,
            Branches: branches);
    }

    private static Dictionary<string, (PackageClaim Claim, BookMetadata Book)> BuildClaimIndex(
        IReadOnlyList<CkpPackage> packages)
    {
        var index = new Dictionary<string, (PackageClaim, BookMetadata)>();
        foreach (var pkg in packages)
            foreach (var claim in pkg.Claims)
                index[claim.Id] = (claim, pkg.Manifest.Book);
        return index;
    }

}
