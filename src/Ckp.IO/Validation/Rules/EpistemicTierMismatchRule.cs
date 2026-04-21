namespace Ckp.IO.Rules;

using System.Text.RegularExpressions;
using Ckp.Core;

/// <summary>
/// Symmetric epistemic consistency check. Detects two failure modes:
/// <list type="bullet">
/// <item><b>SEM1 — Over-promotion:</b> T1 claim with hedging language -> Warning.
/// A T1 built on "appears to" is sand for the alignment engine.</item>
/// <item><b>SEM6 — Epistemic rigidity:</b> T3/T4 claim without hedging -> Notice.
/// A T3 stripped of nuance forces the 2.0 compiler to reconcile an absolute
/// statement that doesn't exist in the literature.</item>
/// </list>
/// Hedging markers are loaded from external JSON — no hardcoded vocabulary.
/// </summary>
public sealed class EpistemicTierMismatchRule : IExtractionRule
{
    private readonly Regex? _hedgingPattern;

    public EpistemicTierMismatchRule(IReadOnlyList<string> hedgingMarkers)
    {
        if (hedgingMarkers.Count > 0)
        {
            string pattern = @"\b(" + string.Join("|", hedgingMarkers.Select(Regex.Escape)) + @")\b";
            _hedgingPattern = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }

    public IEnumerable<ClaimValidationDiagnostic> Validate(
        PackageClaim claim, ExtractionPriority? priority)
    {
        if (string.IsNullOrEmpty(claim.Statement) || _hedgingPattern is null)
            yield break;

        bool hasHedge = _hedgingPattern.IsMatch(claim.Statement);

        // SEM1: Over-promotion — T1 claim with epistemic hedging
        if (claim.Tier == Tier.T1 && hasHedge)
        {
            var match = _hedgingPattern.Match(claim.Statement).Value;
            yield return new ClaimValidationDiagnostic(
                "SEM1",
                ClaimValidationSeverity.Warning,
                claim.Id,
                $"T1 claim contains epistemic hedge \"{match}\". " +
                "Demote to T2/T3 or rephrase if consensus is truly absolute.");
        }

        // SEM6: Epistemic rigidity — T3/T4 claim without any hedging
        if (claim.Tier is Tier.T3 or Tier.T4 && !hasHedge)
        {
            yield return new ClaimValidationDiagnostic(
                "SEM6",
                ClaimValidationSeverity.Notice,
                claim.Id,
                $"Claim is {claim.Tier} (frontier/traditional) but lacks hedging vocabulary. " +
                "Verify extraction hasn't stripped nuance, or consider if the claim warrants promotion.");
        }
    }
}
