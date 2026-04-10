namespace Ckp.Core;

/// <summary>
/// A single validation diagnostic produced by the CKP extraction validator.
/// </summary>
/// <param name="RuleId">Rule identifier (e.g., "S1", "SET3", "SEM1").</param>
/// <param name="Severity">Whether this diagnostic blocks acceptance or is advisory.</param>
/// <param name="ClaimId">The claim that triggered the diagnostic, or null for package-level rules.</param>
/// <param name="Message">Human-readable explanation of the issue.</param>
public sealed record ClaimValidationDiagnostic(
    string RuleId,
    ClaimValidationSeverity Severity,
    string? ClaimId,
    string Message);
