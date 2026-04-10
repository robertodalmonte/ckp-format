namespace Ckp.IO.Rules;

using Ckp.Core;

/// <summary>
/// SEM5: Flags claims whose tier history doesn't reach the current book edition.
/// </summary>
public sealed class StaleTierHistoryRule : IExtractionRule
{
    private readonly int _bookEdition;

    public StaleTierHistoryRule(int bookEdition) => _bookEdition = bookEdition;

    public IEnumerable<ClaimValidationDiagnostic> Validate(
        PackageClaim claim, ExtractionPriority? priority)
    {
        if (claim.TierHistory.Count == 0 || _bookEdition <= 0)
            yield break;

        int latestEdition = claim.TierHistory.Max(t => t.Edition);
        if (latestEdition < _bookEdition)
        {
            yield return new("SEM5", ClaimValidationSeverity.Warning, claim.Id,
                $"Latest tier history entry is edition {latestEdition} but book is edition {_bookEdition}. Tier may be stale.");
        }
    }
}
