namespace Ckp.Core.Validation;

/// <summary>
/// A single validation diagnostic produced by the CKP extraction validator.
/// </summary>
/// <param name="RuleId">Rule identifier (e.g., "S1", "SET3", "SEM1").</param>
/// <param name="Severity">Whether this diagnostic blocks acceptance or is advisory.</param>
/// <param name="ClaimId">The claim that triggered the diagnostic, or null for package-level rules.</param>
/// <param name="Message">Human-readable explanation of the issue.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record ClaimValidationDiagnostic(
    string RuleId,
    ClaimValidationSeverity Severity,
    string? ClaimId,
    string Message);
