namespace Ckp.Core.Structure;

/// <summary>
/// Metadata about a specific edition of the book, stored in history/editions.json.
/// </summary>
/// <param name="Edition">Edition number.</param>
/// <param name="Year">Publication year.</param>
/// <param name="Isbn">ISBN for this edition.</param>
/// <param name="Editor">Editor name(s) for this edition.</param>
/// <param name="Note">Optional note about this edition.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record EditionInfo(
    int Edition,
    int Year,
    string? Isbn,
    string? Editor,
    string? Note);
