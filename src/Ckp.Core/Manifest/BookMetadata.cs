namespace Ckp.Core.Manifest;

/// <summary>
/// Metadata about the book contained in a .ckp package.
/// </summary>
/// <param name="Key">Short identifier used in claim IDs (e.g., "delta-14e").</param>
/// <param name="Title">Full book title.</param>
/// <param name="Edition">Edition number.</param>
/// <param name="Authors">Author names.</param>
/// <param name="Publisher">Publisher name.</param>
/// <param name="Year">Publication year.</param>
/// <param name="Isbn">ISBN-13 identifier.</param>
/// <param name="Language">BCP-47 language tag (e.g., "en-US").</param>
/// <param name="Domains">Knowledge domains covered by this book.</param>
public sealed record BookMetadata(
    string Key,
    string Title,
    int Edition,
    IReadOnlyList<string> Authors,
    string Publisher,
    int Year,
    string? Isbn,
    string Language,
    IReadOnlyList<string> Domains);
