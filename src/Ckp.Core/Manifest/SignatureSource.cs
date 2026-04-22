namespace Ckp.Core.Manifest;

/// <summary>
/// Trust tier of the entity that signed a .ckp package, parallel to ALiveBook's ContributorType.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public enum SignatureSource
{
    /// <summary>Signed by the book's publisher. Highest trust.</summary>
    Publisher = 0,

    /// <summary>Signed by the book's author. Authoritative.</summary>
    Author = 1,

    /// <summary>Signed by an institutional scholar. Reviewed trust.</summary>
    Scholar = 2,

    /// <summary>Signed by a community contributor. Personal key only.</summary>
    Community = 3,

    /// <summary>AI-assisted extraction, unsigned until human review.</summary>
    AiAssisted = 4
}
