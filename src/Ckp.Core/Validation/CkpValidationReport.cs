namespace Ckp.Core.Validation;

/// <summary>
/// Aggregated result of validating a CKP package against the extraction criteria.
/// A package is valid only when it contains zero Error-level diagnostics.
/// </summary>
/// <param name="IsValid">True only if there are no Error-level diagnostics.</param>
/// <param name="Diagnostics">All diagnostics produced during validation.</param>
public sealed record CkpValidationReport(
    bool IsValid,
    IReadOnlyList<ClaimValidationDiagnostic> Diagnostics)
{
    // P6 — pre-P6 each of ErrorCount/WarningCount/NoticeCount re-scanned the full
    // diagnostics list via `.Count(predicate)`. Reports are immutable, so we count
    // once at construction and cache the tuple. Callers that inspect all three
    // counts (a common CLI pattern) pay one pass instead of three.
    private readonly (int Error, int Warning, int Notice) _severityCounts =
        CountBySeverity(Diagnostics);

    /// <summary>Creates a report indicating the package passed all checks.</summary>
    public static CkpValidationReport Valid() => new(true, []);

    /// <summary>Creates a report from a list of diagnostics.</summary>
    public static CkpValidationReport WithDiagnostics(IReadOnlyList<ClaimValidationDiagnostic> diagnostics) =>
        new(diagnostics.All(d => d.Severity != ClaimValidationSeverity.Error), diagnostics);

    /// <summary>Count of Error-level diagnostics.</summary>
    public int ErrorCount => _severityCounts.Error;

    /// <summary>Count of Warning-level diagnostics.</summary>
    public int WarningCount => _severityCounts.Warning;

    /// <summary>Count of Notice-level diagnostics.</summary>
    public int NoticeCount => _severityCounts.Notice;

    private static (int Error, int Warning, int Notice) CountBySeverity(
        IReadOnlyList<ClaimValidationDiagnostic> diagnostics)
    {
        int e = 0, w = 0, n = 0;
        for (int i = 0; i < diagnostics.Count; i++)
        {
            switch (diagnostics[i].Severity)
            {
                case ClaimValidationSeverity.Error: e++; break;
                case ClaimValidationSeverity.Warning: w++; break;
                case ClaimValidationSeverity.Notice: n++; break;
            }
        }
        return (e, w, n);
    }
}
