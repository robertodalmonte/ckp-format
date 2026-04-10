namespace Ckp.IO;

using Ckp.Core;
using Ckp.Core.Field;

/// <summary>
/// Proposes claim alignments between two CKP 1.0 packages by scoring multi-signal
/// similarity: MeSH term overlap, observable overlap, keyword Jaccard, and domain match.
/// </summary>
public sealed class AlignmentProposer : IAlignmentProposer
{
    private const double MeshWeight = 0.40;
    private const double ObservableWeight = 0.25;
    private const double KeywordWeight = 0.20;
    private const double DomainWeight = 0.15;
    private const double MinimumScore = 0.3;

    public IReadOnlyList<AlignmentProposal> Propose(CkpPackage source, CkpPackage target)
    {
        var proposals = new List<AlignmentProposal>();
        var usedTargets = new HashSet<string>();

        foreach (var srcClaim in source.Claims)
        {
            AlignmentProposal? best = null;

            foreach (var tgtClaim in target.Claims)
            {
                double score = ScorePair(srcClaim, tgtClaim);
                if (score < MinimumScore) continue;

                bool isContradiction = DetectContradiction(srcClaim, tgtClaim);
                string canonicalId = ProposeCanonicalId(srcClaim, tgtClaim);
                string reason = BuildReason(srcClaim, tgtClaim, score);

                var proposal = new AlignmentProposal(
                    srcClaim.Id, tgtClaim.Id, score, reason, canonicalId, isContradiction);

                if (best is null || proposal.Score > best.Score)
                    best = proposal;
            }

            if (best is not null && !usedTargets.Contains(best.TargetClaimId))
            {
                proposals.Add(best);
                usedTargets.Add(best.TargetClaimId);
            }
        }

        return proposals.OrderByDescending(p => p.Score).ToList();
    }

    public static double ScorePair(PackageClaim source, PackageClaim target)
    {
        double meshScore = JaccardSimilarity(source.MeshTerms, target.MeshTerms);
        double observableScore = ObservableSimilarity(source.Observables, target.Observables);
        double keywordScore = JaccardSimilarity(source.Keywords, target.Keywords);
        double domainScore = source.Domain.Equals(target.Domain, StringComparison.OrdinalIgnoreCase)
            ? 1.0
            : SubDomainOverlap(source, target);

        return MeshWeight * meshScore
             + ObservableWeight * observableScore
             + KeywordWeight * keywordScore
             + DomainWeight * domainScore;
    }

    private static double JaccardSimilarity(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 0.0;

        var setA = new HashSet<string>(a, StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(b, StringComparer.OrdinalIgnoreCase);

        int intersection = setA.Count(x => setB.Contains(x));
        int union = setA.Count + setB.Count - intersection;

        return union == 0 ? 0.0 : (double)intersection / union;
    }

    private static double ObservableSimilarity(
        IReadOnlyList<Observable> source, IReadOnlyList<Observable> target)
    {
        if (source.Count == 0 || target.Count == 0) return 0.0;

        int matches = 0;
        foreach (var s in source)
        {
            foreach (var t in target)
            {
                if (MeasurementsOverlap(s.Measurement, t.Measurement) &&
                    DirectionsCompatible(s.Direction, t.Direction))
                {
                    matches++;
                    break;
                }
            }
        }

        int total = Math.Max(source.Count, target.Count);
        return (double)matches / total;
    }

    private static bool MeasurementsOverlap(string a, string b)
    {
        // Tokenize and check overlap — measurements like "PgE2 concentration" and
        // "prostaglandin E2 level" should partially match
        var tokensA = Tokenize(a);
        var tokensB = Tokenize(b);
        int shared = tokensA.Count(t => tokensB.Contains(t));
        int total = Math.Max(tokensA.Count, tokensB.Count);
        return total > 0 && (double)shared / total > 0.3;
    }

    private static bool DirectionsCompatible(string a, string b) =>
        a.Equals(b, StringComparison.OrdinalIgnoreCase);

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

        int srcTier = TierToInt(source.Tier);
        int tgtTier = TierToInt(target.Tier);
        return Math.Abs(srcTier - tgtTier) >= 2;
    }

    private static string ProposeCanonicalId(PackageClaim source, PackageClaim target)
    {
        string domain = source.Domain.ToLowerInvariant();
        string subDomain = source.SubDomain?.ToLowerInvariant() ?? target.SubDomain?.ToLowerInvariant() ?? "general";
        return $"ckp:field:{domain}:{subDomain}";
    }

    private static string BuildReason(PackageClaim source, PackageClaim target, double score)
    {
        var parts = new List<string>();

        var sharedMesh = source.MeshTerms.Intersect(target.MeshTerms, StringComparer.OrdinalIgnoreCase).ToList();
        if (sharedMesh.Count > 0)
            parts.Add($"shared MeSH: {string.Join(", ", sharedMesh)}");

        var sharedKw = source.Keywords.Intersect(target.Keywords, StringComparer.OrdinalIgnoreCase).ToList();
        if (sharedKw.Count > 0)
            parts.Add($"shared keywords: {string.Join(", ", sharedKw)}");

        if (source.Domain.Equals(target.Domain, StringComparison.OrdinalIgnoreCase))
            parts.Add($"same domain: {source.Domain}");

        return parts.Count > 0
            ? string.Join("; ", parts)
            : $"alignment score {score:F2}";
    }

    private static HashSet<string> Tokenize(string text) =>
        new(text.Split([' ', '-', '_', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries),
            StringComparer.OrdinalIgnoreCase);

    private static int TierToInt(string tier) => tier switch
    {
        "T1" => 1, "T2" => 2, "T3" => 3, "T4" => 4, _ => 0
    };
}
