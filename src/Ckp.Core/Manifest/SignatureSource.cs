namespace Ckp.Core;

/// <summary>
/// Trust tier of the entity that signed a .ckp package, parallel to ALiveBook's ContributorType.
/// </summary>
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
