namespace Ckp.Core.Structure;

/// <summary>
/// Chapter index entry for the structure/ directory of a .ckp package.
/// </summary>
/// <param name="Number">Chapter number.</param>
/// <param name="Title">Chapter title.</param>
/// <param name="ClaimCount">Number of claims sourced from this chapter.</param>
/// <param name="Domains">Knowledge domains covered in this chapter.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record ChapterInfo(
    int Number,
    string Title,
    int ClaimCount,
    IReadOnlyList<string> Domains);
