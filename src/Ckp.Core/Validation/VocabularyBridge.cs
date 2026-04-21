namespace Ckp.Core.Validation;

/// <summary>
/// Maps vocabulary fragments between two aligned claims from different books,
/// making cross-book vocabulary fragmentation analysis computable.
/// </summary>
/// <param name="SourceTerms">Terms used in the source book for this concept.</param>
/// <param name="TargetTerms">Terms used in the target book for the same concept.</param>
/// <param name="SharedConcept">The underlying concept that both vocabularies describe.</param>
public sealed record VocabularyBridge(
    IReadOnlyList<string> SourceTerms,
    IReadOnlyList<string> TargetTerms,
    string SharedConcept);
