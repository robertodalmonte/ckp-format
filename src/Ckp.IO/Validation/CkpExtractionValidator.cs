namespace Ckp.IO;

using System.Text.RegularExpressions;
using Ckp.Core;
using Ckp.IO.Rules;

/// <summary>
/// Validates a hydrated <see cref="CkpPackage"/> against the CKP 1.1 extraction criteria.
/// Structural (S*) and set-level (SET*) rules are built-in. Per-claim semantic rules (SEM*)
/// are composed via the <see cref="IExtractionRule"/> strategy pattern — drop a new rule
/// into the collection without changing pipeline logic.
/// </summary>
/// <remarks>Spec: <c>Docs/CKP_Extraction_Validator_Spec.md</c></remarks>
public sealed class CkpExtractionValidator : ICkpExtractionValidator
{
    private static readonly Regex HashPattern = new(
        @"^sha256:[a-f0-9]{64}$", RegexOptions.Compiled);

    private static readonly Regex IdPattern = new(
        @"^[a-z0-9]+-[a-z0-9]+\.[A-Z]{2,4}\.\d{3}$", RegexOptions.Compiled);

    private readonly ExtractionVocabulary _vocabulary;

    public CkpExtractionValidator(ExtractionVocabulary? vocabulary = null)
    {
        _vocabulary = vocabulary ?? ExtractionVocabulary.Empty;
    }

    public CkpValidationReport Validate(
        CkpPackage package,
        IReadOnlyDictionary<string, ExtractionPriority>? priorities = null)
    {
        var diagnostics = new List<ClaimValidationDiagnostic>();

        ValidateStructural(package, diagnostics);
        ValidateSetLevel(package, diagnostics);
        ValidatePriorityConditional(package, priorities, diagnostics);
        RunExtractionRules(package, priorities, diagnostics);

        return diagnostics.Count == 0
            ? CkpValidationReport.Valid()
            : CkpValidationReport.WithDiagnostics(diagnostics);
    }

    private static void ValidateStructural(
        CkpPackage package, List<ClaimValidationDiagnostic> diagnostics)
    {
        foreach (var claim in package.Claims)
        {
            // S1: Hash format
            if (!HashPattern.IsMatch(claim.Hash))
            {
                diagnostics.Add(new("S1", ClaimValidationSeverity.Error, claim.Id,
                    $"Hash '{claim.Hash}' does not match required format sha256:<64 hex chars>."));
            }

            // S2: Hash integrity
            if (HashPattern.IsMatch(claim.Hash))
            {
                string expected = CkpHash.OfStatement(claim.Statement);
                if (claim.Hash != expected)
                {
                    diagnostics.Add(new("S2", ClaimValidationSeverity.Error, claim.Id,
                        $"Hash mismatch: stored '{claim.Hash}' but statement hashes to '{expected}'."));
                }
            }

            // S4: ID format
            if (!IdPattern.IsMatch(claim.Id))
            {
                diagnostics.Add(new("S4", ClaimValidationSeverity.Error, claim.Id,
                    $"ID '{claim.Id}' does not match required format (e.g., 'alpha-3e.BIO.007')."));
            }

            // S5: Statement present
            if (string.IsNullOrWhiteSpace(claim.Statement))
            {
                diagnostics.Add(new("S5", ClaimValidationSeverity.Error, claim.Id,
                    "Statement is empty or whitespace."));
            }

            // S6: Domain present
            if (string.IsNullOrWhiteSpace(claim.Domain))
            {
                diagnostics.Add(new("S6", ClaimValidationSeverity.Error, claim.Id,
                    "Domain is empty or whitespace."));
            }
        }
    }

    private static void ValidateSetLevel(
        CkpPackage package, List<ClaimValidationDiagnostic> diagnostics)
    {
        // SET1: Unique IDs
        var idCounts = package.Claims
            .GroupBy(c => c.Id)
            .Where(g => g.Count() > 1);

        foreach (var group in idCounts)
        {
            diagnostics.Add(new("SET1", ClaimValidationSeverity.Error, group.Key,
                $"Duplicate claim ID '{group.Key}' appears {group.Count()} times."));
        }

        // SET2: Unique hashes
        var hashCounts = package.Claims
            .GroupBy(c => c.Hash)
            .Where(g => g.Count() > 1);

        foreach (var group in hashCounts)
        {
            var ids = string.Join(", ", group.Select(c => c.Id));
            diagnostics.Add(new("SET2", ClaimValidationSeverity.Error, null,
                $"Duplicate hash '{group.Key[..20]}...' shared by claims: {ids}. Identical statements detected."));
        }

        // SET3: Internal references resolve
        var allIds = new HashSet<string>(package.Claims.Select(c => c.Id));
        foreach (var claim in package.Claims)
        {
            foreach (var evidence in claim.Evidence)
            {
                if (evidence.Type == EvidenceReferenceType.InternalRef && !allIds.Contains(evidence.Ref))
                {
                    diagnostics.Add(new("SET3", ClaimValidationSeverity.Error, claim.Id,
                        $"Internal reference '{evidence.Ref}' does not resolve to any claim in the package."));
                }
            }
        }

        // SET4: Citation consistency
        var citationRefs = new HashSet<string>(package.Citations.Select(c => c.Ref));
        foreach (var claim in package.Claims)
        {
            foreach (var evidence in claim.Evidence)
            {
                if (evidence.Type == EvidenceReferenceType.Citation && !citationRefs.Contains(evidence.Ref))
                {
                    diagnostics.Add(new("SET4", ClaimValidationSeverity.Warning, claim.Id,
                        $"Citation '{evidence.Ref}' referenced in claim but not found in package citations array."));
                }
            }
        }

        // SET5: Manifest counts
        var fingerprint = package.Manifest.ContentFingerprint;
        if (fingerprint.ClaimCount != package.Claims.Count)
        {
            diagnostics.Add(new("SET5", ClaimValidationSeverity.Error, null,
                $"Manifest claims {fingerprint.ClaimCount} claims but package contains {package.Claims.Count}."));
        }

        int actualT1 = package.Claims.Count(c => c.Tier == Tier.T1);
        int actualT2 = package.Claims.Count(c => c.Tier == Tier.T2);
        int actualT3 = package.Claims.Count(c => c.Tier == Tier.T3);
        int actualT4 = package.Claims.Count(c => c.Tier == Tier.T4);

        if (fingerprint.T1Count != actualT1)
            diagnostics.Add(new("SET5", ClaimValidationSeverity.Error, null,
                $"Manifest T1 count ({fingerprint.T1Count}) != actual ({actualT1})."));
        if (fingerprint.T2Count != actualT2)
            diagnostics.Add(new("SET5", ClaimValidationSeverity.Error, null,
                $"Manifest T2 count ({fingerprint.T2Count}) != actual ({actualT2})."));
        if (fingerprint.T3Count != actualT3)
            diagnostics.Add(new("SET5", ClaimValidationSeverity.Error, null,
                $"Manifest T3 count ({fingerprint.T3Count}) != actual ({actualT3})."));
        if (fingerprint.T4Count != actualT4)
            diagnostics.Add(new("SET5", ClaimValidationSeverity.Error, null,
                $"Manifest T4 count ({fingerprint.T4Count}) != actual ({actualT4})."));
    }

    private static void ValidatePriorityConditional(
        CkpPackage package,
        IReadOnlyDictionary<string, ExtractionPriority>? priorities,
        List<ClaimValidationDiagnostic> diagnostics)
    {
        if (priorities is null) return;

        foreach (var claim in package.Claims)
        {
            if (!priorities.TryGetValue(claim.Id, out var priority)) continue;

            // PC1: Citation required for T1/T2
            if (claim.Tier is Tier.T1 or Tier.T2)
            {
                bool hasCitation = claim.Evidence.Any(e => e.Type == EvidenceReferenceType.Citation);
                if (!hasCitation)
                {
                    diagnostics.Add(new("PC1", ClaimValidationSeverity.Error, claim.Id,
                        $"Tier {claim.Tier} claim requires at least one citation in evidence."));
                }
            }

            // PC2: Observable required for P0/P1
            if (priority is ExtractionPriority.Mechanistic or ExtractionPriority.Quantitative)
            {
                if (claim.Observables.Count == 0)
                {
                    diagnostics.Add(new("PC2", ClaimValidationSeverity.Error, claim.Id,
                        $"Priority {priority} claim requires at least one observable."));
                }
            }
        }
    }

    /// <summary>
    /// Runs all per-claim <see cref="IExtractionRule"/> instances in the pipeline.
    /// New rules are added here — the rest of the validator doesn't change.
    /// </summary>
    private void RunExtractionRules(
        CkpPackage package,
        IReadOnlyDictionary<string, ExtractionPriority>? priorities,
        List<ClaimValidationDiagnostic> diagnostics)
    {
        IExtractionRule[] rules =
        [
            new EpistemicTierMismatchRule(_vocabulary.HedgingMarkers),   // SEM1 + SEM6
            new CompoundStatementRule(),                                 // SEM2
            new UnknownDomainRule(_vocabulary.KnownDomains),             // SEM3
            new MechanisticObservableRule(_vocabulary.MechanisticKeywords), // SEM4
            new StaleTierHistoryRule(package.Manifest.Book.Edition)      // SEM5
        ];

        foreach (var claim in package.Claims)
        {
            ExtractionPriority? p = null;
            if (priorities is not null && priorities.TryGetValue(claim.Id, out var priority))
                p = priority;

            foreach (var rule in rules)
            {
                diagnostics.AddRange(rule.Validate(claim, p));
            }
        }
    }

}
