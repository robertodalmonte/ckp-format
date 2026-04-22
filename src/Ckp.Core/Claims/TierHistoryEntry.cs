namespace Ckp.Core.Claims;

/// <summary>
/// A record of a claim's tier assignment in a specific edition of the book.
/// The promotion/demotion trail IS the forensic record.
/// </summary>
/// <param name="Edition">The book edition in which this tier was assigned.</param>
/// <param name="Tier">The tier assigned in this edition (T1–T4).</param>
/// <param name="Note">Human-readable note explaining the assignment or change.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record TierHistoryEntry(
    int Edition,
    Tier Tier,
    string? Note);
