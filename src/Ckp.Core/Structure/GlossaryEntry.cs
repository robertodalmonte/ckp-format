namespace Ckp.Core;

/// <summary>
/// Maps a book-specific term to its standard equivalent and cross-book synonyms,
/// making cross-book vocabulary fragmentation analysis computable
/// at packaging time rather than query time.
/// </summary>
/// <param name="BookTerm">The term as used in this book (e.g., "fascial mechanoreceptor").</param>
/// <param name="StandardTerm">Normalized standard term (e.g., "tissue mechanoreceptor").</param>
/// <param name="MeshTerm">MeSH descriptor ID, or null if no MeSH mapping exists.</param>
/// <param name="EquivalentsInOtherBooks">Book key → term mapping for cross-book synonyms.</param>
/// <param name="Note">Optional annotation (e.g., "Four books, four names, one transducer type.").</param>
public sealed record GlossaryEntry(
    string BookTerm,
    string StandardTerm,
    string? MeshTerm,
    IReadOnlyDictionary<string, string> EquivalentsInOtherBooks,
    string? Note);
