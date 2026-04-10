namespace Ckp.Core;

/// <summary>
/// A single extraction validation rule that checks a claim against a quality criterion.
/// Implementations are composed into the validation pipeline via the Strategy pattern —
/// drop a new rule into the collection without changing pipeline logic.
/// </summary>
/// <remarks>
/// Rules that need package-level context (set-level rules like SET1, SET2) are handled
/// separately by the validator. This interface is for per-claim rules only.
/// </remarks>
public interface IExtractionRule
{
    /// <summary>
    /// Validates a single claim and yields zero or more diagnostics.
    /// </summary>
    /// <param name="claim">The claim to validate.</param>
    /// <param name="priority">The extraction priority annotation, if available.</param>
    /// <returns>Diagnostics found. Empty if the claim passes this rule.</returns>
    IEnumerable<ClaimValidationDiagnostic> Validate(
        PackageClaim claim,
        ExtractionPriority? priority);
}
