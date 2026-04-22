namespace Ckp.Core.Alignment;

/// <summary>
/// Direction of a tier mismatch between aligned claims in two books.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public enum TierMismatchDirection
{
    /// <summary>Both books agree on the tier. Strong consensus signal.</summary>
    Same = 0,

    /// <summary>Source book considers the claim more established than target.</summary>
    SourceAhead = 1,

    /// <summary>Target book considers the claim more established than source.</summary>
    TargetAhead = 2
}
