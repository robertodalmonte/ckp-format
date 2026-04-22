namespace Ckp.Core.Alignment;

/// <summary>
/// Classification of how two claims from different books relate to each other.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public enum AlignmentType
{
    /// <summary>Same phenomenon, same or different vocabulary.</summary>
    Equivalent = 0,

    /// <summary>Partial overlap — one claim is broader than the other.</summary>
    Overlapping = 1,

    /// <summary>Same phenomenon, opposite conclusions.</summary>
    Contradictory = 2,

    /// <summary>Different aspects of the same phenomenon (not contradictory).</summary>
    Complementary = 3,

    /// <summary>No equivalent in the target book.</summary>
    Unmatched = 4
}
