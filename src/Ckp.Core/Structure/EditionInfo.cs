namespace Ckp.Core;

/// <summary>
/// Metadata about a specific edition of the book, stored in history/editions.json.
/// </summary>
/// <param name="Edition">Edition number.</param>
/// <param name="Year">Publication year.</param>
/// <param name="Isbn">ISBN for this edition.</param>
/// <param name="Editor">Editor name(s) for this edition.</param>
/// <param name="Note">Optional note about this edition.</param>
public sealed record EditionInfo(
    int Edition,
    int Year,
    string? Isbn,
    string? Editor,
    string? Note);
