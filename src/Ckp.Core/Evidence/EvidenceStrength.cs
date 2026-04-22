namespace Ckp.Core.Evidence;

/// <summary>
/// Strength classification for an evidence reference within a CKP claim.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public enum EvidenceStrength
{
    /// <summary>Primary evidence directly demonstrating the claim.</summary>
    Primary = 0,

    /// <summary>Confirmatory evidence from independent replication.</summary>
    Confirmatory = 1,

    /// <summary>Peripheral evidence providing indirect support.</summary>
    Peripheral = 2
}
