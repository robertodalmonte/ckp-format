namespace Ckp.Core;

/// <summary>
/// Direction of a tier mismatch between aligned claims in two books.
/// </summary>
public enum TierMismatchDirection
{
    /// <summary>Both books agree on the tier. Strong consensus signal.</summary>
    Same = 0,

    /// <summary>Source book considers the claim more established than target.</summary>
    SourceAhead = 1,

    /// <summary>Target book considers the claim more established than source.</summary>
    TargetAhead = 2
}
