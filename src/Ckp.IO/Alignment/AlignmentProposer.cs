namespace Ckp.IO;

using Ckp.Core;
using Ckp.Core.Field;

/// <summary>
/// Proposes claim alignments between two CKP 1.0 packages by scoring multi-signal
/// similarity: MeSH term overlap, observable overlap, keyword Jaccard, and domain match.
/// </summary>
/// <remarks>
/// P4: the proposer is the most allocation-heavy path in the codebase — two nested
/// loops over <c>source.Claims × target.Claims</c> where pre-P4 every
/// <c>ScorePair</c> call built four <see cref="HashSet{T}"/> instances
/// (two Jaccards × 2 sets each) plus a fresh tokenization set per
/// <c>MeasurementsOverlap</c> call. On a 1000×1000 proposal that was ~4M+
/// transient HashSets. The current implementation pre-tokenizes each claim's
/// MeSH, keyword, and per-observable-measurement sets into <see cref="ClaimFeatures"/>
/// once, then scores pairs against cached sets; Jaccard/overlap become
/// probe-the-smaller-iterate-the-larger passes with zero per-pair allocation.
/// The public <see cref="ScorePair(PackageClaim, PackageClaim)"/> overload still
/// materializes features on the fly for callers outside the proposer hot loop.
/// </remarks>
public sealed class AlignmentProposer : IAlignmentProposer
{
    private const double MeshWeight = 0.40;
    private const double ObservableWeight = 0.25;
    private const double KeywordWeight = 0.20;
    private const double DomainWeight = 0.15;
    private const double MinimumScore = 0.3;

    public IReadOnlyList<AlignmentProposal> Propose(CkpPackage source, CkpPackage target)
    {
        // Pre-tokenize each claim's MeSH/keyword/observable feature sets exactly once.
        // Without this, the nested loop below would rebuild four transient HashSets
        // inside every ScorePair invocation (two Jaccards × two sets each) plus one
        // per observable pair checked — quadratic in claim count, linear in token count.
        var sourceFeatures = BuildFeatures(source.Claims);
        var targetFeatures = BuildFeatures(target.Claims);

        // Score every candidate pair above the threshold, then greedily claim
        // the highest-scored pairs first — each source and target used at most
        // once. Unbiased by source/target iteration order.
        var candidates = new List<AlignmentProposal>();
        foreach (var src in sourceFeatures)
        {
            foreach (var tgt in targetFeatures)
            {
                double score = ScorePair(src, tgt);
                if (score < MinimumScore) continue;

                candidates.Add(new AlignmentProposal(
                    src.Claim.Id, tgt.Claim.Id, score,
                    BuildReason(src, tgt, score),
                    ProposeCanonicalId(src.Claim, tgt.Claim),
                    DetectContradiction(src.Claim, tgt.Claim)));
            }
        }

        var usedSources = new HashSet<string>();
        var usedTargets = new HashSet<string>();
        var proposals = new List<AlignmentProposal>();

        foreach (var proposal in candidates.OrderByDescending(p => p.Score))
        {
            if (usedSources.Contains(proposal.SourceClaimId)) continue;
            if (usedTargets.Contains(proposal.TargetClaimId)) continue;

            proposals.Add(proposal);
            usedSources.Add(proposal.SourceClaimId);
            usedTargets.Add(proposal.TargetClaimId);
        }

        return proposals;
    }

    /// <summary>
    /// Convenience overload retained for test/direct callers. Builds feature
    /// caches for both sides and then delegates — O(n+m) up-front cost, which
    /// is fine outside the proposer hot loop.
    /// </summary>
    public static double ScorePair(PackageClaim source, PackageClaim target) =>
        ScorePair(ClaimFeatures.Build(source), ClaimFeatures.Build(target));

    private static double ScorePair(ClaimFeatures source, ClaimFeatures target)
    {
        double meshScore = JaccardSimilarity(source.MeshTerms, target.MeshTerms);
        double observableScore = ObservableSimilarity(source.Observables, target.Observables);
        double keywordScore = JaccardSimilarity(source.Keywords, target.Keywords);
        double domainScore = source.Claim.Domain.Equals(target.Claim.Domain, StringComparison.OrdinalIgnoreCase)
            ? 1.0
            : SubDomainOverlap(source.Claim, target.Claim);

        return MeshWeight * meshScore
             + ObservableWeight * observableScore
             + KeywordWeight * keywordScore
             + DomainWeight * domainScore;
    }

    private static ClaimFeatures[] BuildFeatures(IReadOnlyList<PackageClaim> claims)
    {
        var features = new ClaimFeatures[claims.Count];
        for (int i = 0; i < claims.Count; i++)
            features[i] = ClaimFeatures.Build(claims[i]);
        return features;
    }

    private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 0.0;

        // Probe the smaller set against the larger to minimize hash lookups.
        var (smaller, larger) = a.Count <= b.Count ? (a, b) : (b, a);
        int intersection = 0;
        foreach (var item in smaller)
        {
            if (larger.Contains(item)) intersection++;
        }

        int union = a.Count + b.Count - intersection;
        return union == 0 ? 0.0 : (double)intersection / union;
    }

    private static double ObservableSimilarity(
        ObservableFeatures[] source, ObservableFeatures[] target)
    {
        if (source.Length == 0 || target.Length == 0) return 0.0;

        int matches = 0;
        foreach (var s in source)
        {
            foreach (var t in target)
            {
                if (MeasurementsOverlap(s.MeasurementTokens, t.MeasurementTokens) &&
                    string.Equals(s.Direction, t.Direction, StringComparison.OrdinalIgnoreCase))
                {
                    matches++;
                    break;
                }
            }
        }

        int total = Math.Max(source.Length, target.Length);
        return (double)matches / total;
    }

    private static bool MeasurementsOverlap(HashSet<string> tokensA, HashSet<string> tokensB)
    {
        // Measurements like "PgE2 concentration" and "prostaglandin E2 level" should
        // partially match. Tokens were computed once when the feature cache was built.
        var (smaller, larger) = tokensA.Count <= tokensB.Count ? (tokensA, tokensB) : (tokensB, tokensA);
        int shared = 0;
        foreach (var token in smaller)
        {
            if (larger.Contains(token)) shared++;
        }

        int total = Math.Max(tokensA.Count, tokensB.Count);
        return total > 0 && (double)shared / total > 0.3;
    }

    private static double SubDomainOverlap(PackageClaim source, PackageClaim target)
    {
        if (source.SubDomain is not null && target.SubDomain is not null &&
            source.SubDomain.Equals(target.SubDomain, StringComparison.OrdinalIgnoreCase))
            return 0.7;

        return 0.0;
    }

    private static bool DetectContradiction(PackageClaim source, PackageClaim target)
    {
        // Basic heuristic: same domain but tier delta >= 2 suggests possible contradiction
        // Real contradiction detection would require LLM analysis
        if (!source.Domain.Equals(target.Domain, StringComparison.OrdinalIgnoreCase))
            return false;

        return Math.Abs((int)source.Tier - (int)target.Tier) >= 2;
    }

    private static string ProposeCanonicalId(PackageClaim source, PackageClaim target)
    {
        string domain = source.Domain.ToLowerInvariant();
        string subDomain = source.SubDomain?.ToLowerInvariant() ?? target.SubDomain?.ToLowerInvariant() ?? "general";
        return $"ckp:field:{domain}:{subDomain}";
    }

    private static string BuildReason(ClaimFeatures source, ClaimFeatures target, double score)
    {
        // Reuses the pre-built feature HashSets — no fresh tokenization — but we do
        // need the shared elements in original-casing order for the human-readable
        // reason. Iterating source lists preserves the author's original casing
        // and insertion order (the HashSets are case-insensitive probes only).
        var parts = new List<string>();

        var sharedMesh = CollectShared(source.Claim.MeshTerms, target.MeshTerms);
        if (sharedMesh.Count > 0)
            parts.Add($"shared MeSH: {string.Join(", ", sharedMesh)}");

        var sharedKw = CollectShared(source.Claim.Keywords, target.Keywords);
        if (sharedKw.Count > 0)
            parts.Add($"shared keywords: {string.Join(", ", sharedKw)}");

        if (source.Claim.Domain.Equals(target.Claim.Domain, StringComparison.OrdinalIgnoreCase))
            parts.Add($"same domain: {source.Claim.Domain}");

        return parts.Count > 0
            ? string.Join("; ", parts)
            : $"alignment score {score:F2}";
    }

    private static List<string> CollectShared(IReadOnlyList<string> sourceItems, HashSet<string> targetSet)
    {
        var shared = new List<string>();
        foreach (var item in sourceItems)
        {
            if (targetSet.Contains(item)) shared.Add(item);
        }
        return shared;
    }

    /// <summary>
    /// Per-claim pre-tokenized feature cache. Built once per claim at the top of
    /// <see cref="Propose"/> and reused across every pair comparison.
    /// </summary>
    private readonly struct ClaimFeatures
    {
        public PackageClaim Claim { get; init; }
        public HashSet<string> MeshTerms { get; init; }
        public HashSet<string> Keywords { get; init; }
        public ObservableFeatures[] Observables { get; init; }

        public static ClaimFeatures Build(PackageClaim claim)
        {
            var observables = claim.Observables.Count == 0
                ? []
                : new ObservableFeatures[claim.Observables.Count];

            for (int i = 0; i < claim.Observables.Count; i++)
            {
                var obs = claim.Observables[i];
                observables[i] = new ObservableFeatures
                {
                    MeasurementTokens = Tokenize(obs.Measurement),
                    Direction = obs.Direction,
                };
            }

            return new ClaimFeatures
            {
                Claim = claim,
                MeshTerms = new HashSet<string>(claim.MeshTerms, StringComparer.OrdinalIgnoreCase),
                Keywords = new HashSet<string>(claim.Keywords, StringComparer.OrdinalIgnoreCase),
                Observables = observables,
            };
        }
    }

    private readonly struct ObservableFeatures
    {
        public HashSet<string> MeasurementTokens { get; init; }
        public string Direction { get; init; }
    }

    private static HashSet<string> Tokenize(string text) =>
        new(text.Split([' ', '-', '_', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);
}
