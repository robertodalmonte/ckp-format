namespace Ckp.Core;

/// <summary>
/// Classification of how two claims from different books relate to each other.
/// </summary>
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
