namespace Ckp.Core;

/// <summary>
/// Field-agnostic vocabulary configuration for CKP extraction validation.
/// Loaded from external JSON so the validator can be configured for any scientific discipline
/// without changing code.
/// </summary>
public sealed record ExtractionVocabulary(
    IReadOnlySet<string> KnownDomains,
    IReadOnlyList<string> MechanisticKeywords,
    IReadOnlyList<string> HedgingMarkers)
{
    /// <summary>
    /// Empty vocabulary — all domain/keyword/hedging checks are effectively disabled.
    /// Used when no external configuration is loaded.
    /// </summary>
    public static ExtractionVocabulary Empty { get; } = new(
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        [],
        []);
}
