namespace Ckp.Core;

/// <summary>
/// An annotation on a claim by a publisher or community member.
/// Stored in enrichment/commentary/publisher.json or community.json.
/// </summary>
/// <param name="ClaimId">ID of the claim being annotated.</param>
/// <param name="Author">Name or identifier of the annotator.</param>
/// <param name="Text">The commentary text.</param>
/// <param name="CreatedAt">UTC timestamp of the annotation.</param>
public sealed record CommentaryEntry(
    string ClaimId,
    string Author,
    string Text,
    DateTimeOffset CreatedAt);
