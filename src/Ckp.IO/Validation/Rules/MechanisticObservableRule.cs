namespace Ckp.IO.Rules;

using Ckp.Core;

/// <summary>
/// SEM4: Flags claims containing mechanistic keywords that lack observables.
/// Keywords are loaded from external JSON — no hardcoded keyword list.
/// </summary>
public sealed class MechanisticObservableRule : IExtractionRule
{
    private readonly IReadOnlyList<string> _mechanisticKeywords;

    public MechanisticObservableRule(IReadOnlyList<string> mechanisticKeywords)
    {
        _mechanisticKeywords = mechanisticKeywords;
    }

    public IEnumerable<ClaimValidationDiagnostic> Validate(
        PackageClaim claim, ExtractionPriority? priority)
    {
        if (claim.Observables.Count > 0 || string.IsNullOrEmpty(claim.Statement))
            yield break;

        if (_mechanisticKeywords.Count == 0)
            yield break;

        bool hasMechanisticKeyword = _mechanisticKeywords
            .Any(kw => claim.Statement.Contains(kw, StringComparison.OrdinalIgnoreCase));

        if (hasMechanisticKeyword)
        {
            yield return new("SEM4", ClaimValidationSeverity.Warning, claim.Id,
                "Claim contains mechanistic keywords but has no observables. Consider adding measurable predictions.");
        }
    }
}
