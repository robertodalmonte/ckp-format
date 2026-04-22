namespace Ckp.Core.Evidence;

/// <summary>
/// A bibliographic citation referenced by one or more claims in the package.
/// Lives in the evidence/citations.json file.
/// </summary>
/// <param name="Ref">Citation reference (e.g., "PMID:19834602", "DOI:10.1234/...").</param>
/// <param name="Title">Publication title.</param>
/// <param name="Authors">Author list.</param>
/// <param name="Year">Publication year.</param>
/// <param name="Journal">Journal name.</param>
/// <param name="ReferencedBy">Claim IDs that cite this source.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record CitationEntry(
    string Ref,
    string? Title,
    string? Authors,
    int? Year,
    string? Journal,
    IReadOnlyList<string> ReferencedBy);
