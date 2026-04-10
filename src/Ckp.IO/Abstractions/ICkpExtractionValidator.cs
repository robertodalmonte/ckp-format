namespace Ckp.IO;

using Ckp.Core;

/// <summary>
/// Validates a hydrated <see cref="CkpPackage"/> against the CKP 1.1 extraction criteria.
/// Enforces semantic and set-level constraints that JSON Schema cannot express:
/// ID uniqueness, hash integrity, tier-language consistency, priority-conditional rules.
/// </summary>
/// <remarks>
/// <para>
/// The validator is synchronous and read-only — it does not modify the package or access
/// external resources. It returns a <see cref="CkpValidationReport"/> containing all
/// diagnostics. The caller decides whether to reject the package (any Error-level diagnostic)
/// or proceed with warnings.
/// </para>
/// <para>Spec: <c>Docs/CKP_Extraction_Validator_Spec.md</c></para>
/// </remarks>
public interface ICkpExtractionValidator
{
    /// <summary>
    /// Validates the package and returns a report with all diagnostics.
    /// </summary>
    /// <param name="package">The hydrated CKP package to validate.</param>
    /// <param name="priorities">
    /// Optional extraction priority annotations keyed by claim ID.
    /// When provided, priority-conditional rules (PC1, PC2) are enforced.
    /// </param>
    /// <returns>A validation report. <see cref="CkpValidationReport.IsValid"/> is true only
    /// when zero Error-level diagnostics exist.</returns>
    CkpValidationReport Validate(
        CkpPackage package,
        IReadOnlyDictionary<string, ExtractionPriority>? priorities = null);
}
