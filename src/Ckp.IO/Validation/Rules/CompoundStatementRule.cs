namespace Ckp.IO.Rules;

using System.Text.RegularExpressions;
using Ckp.Core;

/// <summary>
/// SEM2: Detects statements containing multiple independent assertions.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users constructing <see cref="CkpExtractionValidator"/>
/// manually (e.g., tests). In production, these rules are wired in by the validator's
/// constructor and need not be instantiated directly.
/// </remarks>
public sealed class CompoundStatementRule : IExtractionRule
{
    public IEnumerable<ClaimValidationDiagnostic> Validate(
        PackageClaim claim, ExtractionPriority? priority)
    {
        if (string.IsNullOrEmpty(claim.Statement))
            yield break;

        if (claim.Statement.Contains("; ") ||
            Regex.IsMatch(claim.Statement, @",\s+and\s+[a-z]+(s|ed|ing)\b"))
        {
            yield return new("SEM2", ClaimValidationSeverity.Warning, claim.Id,
                "Statement may contain multiple independent assertions. Consider splitting.");
        }
    }
}
