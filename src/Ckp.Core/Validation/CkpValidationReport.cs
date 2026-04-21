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
    /// <summary>Creates a report indicating the package passed all checks.</summary>
    public static CkpValidationReport Valid() => new(true, []);

    /// <summary>Creates a report from a list of diagnostics.</summary>
    public static CkpValidationReport WithDiagnostics(IReadOnlyList<ClaimValidationDiagnostic> diagnostics) =>
        new(diagnostics.All(d => d.Severity != ClaimValidationSeverity.Error), diagnostics);

    /// <summary>Count of Error-level diagnostics.</summary>
    public int ErrorCount => Diagnostics.Count(d => d.Severity == ClaimValidationSeverity.Error);

    /// <summary>Count of Warning-level diagnostics.</summary>
    public int WarningCount => Diagnostics.Count(d => d.Severity == ClaimValidationSeverity.Warning);

    /// <summary>Count of Notice-level diagnostics.</summary>
    public int NoticeCount => Diagnostics.Count(d => d.Severity == ClaimValidationSeverity.Notice);
}
