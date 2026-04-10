namespace Ckp.Core;

/// <summary>
/// Strength classification for an evidence reference within a CKP claim.
/// </summary>
public enum EvidenceStrength
{
    /// <summary>Primary evidence directly demonstrating the claim.</summary>
    Primary = 0,

    /// <summary>Confirmatory evidence from independent replication.</summary>
    Confirmatory = 1,

    /// <summary>Peripheral evidence providing indirect support.</summary>
    Peripheral = 2
}
