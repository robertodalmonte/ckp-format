namespace Ckp.IO.Rules;

using Ckp.Core;

/// <summary>
/// SEM3: Flags domain strings not in the controlled vocabulary.
/// Vocabulary is loaded from external JSON — no hardcoded domain list.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users constructing <see cref="CkpExtractionValidator"/>
/// manually (e.g., tests). In production, these rules are wired in by the validator's
/// constructor and need not be instantiated directly.
/// </remarks>
public sealed class UnknownDomainRule : IExtractionRule
{
    private readonly IReadOnlySet<string> _knownDomains;

    public UnknownDomainRule(IReadOnlySet<string> knownDomains)
    {
        _knownDomains = knownDomains;
    }

    public IEnumerable<ClaimValidationDiagnostic> Validate(
        PackageClaim claim, ExtractionPriority? priority)
    {
        if (_knownDomains.Count == 0)
            yield break;

        if (!string.IsNullOrWhiteSpace(claim.Domain) && !_knownDomains.Contains(claim.Domain))
        {
            yield return new("SEM3", ClaimValidationSeverity.Warning, claim.Id,
                $"Domain '{claim.Domain}' is not in the controlled vocabulary. Review and add or map.");
        }
    }
}
