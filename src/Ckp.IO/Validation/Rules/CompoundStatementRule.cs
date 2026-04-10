namespace Ckp.IO.Rules;

using System.Text.RegularExpressions;
using Ckp.Core;

/// <summary>
/// SEM2: Detects statements containing multiple independent assertions.
/// </summary>
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
