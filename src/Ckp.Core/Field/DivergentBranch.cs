namespace Ckp.Core.Field;

/// <summary>
/// One side of an explicit contradiction in a divergent canonical claim.
/// Each branch holds the position, its tier, and the attestations that support it.
/// </summary>
/// <param name="Position">The specific contradictory assertion.</param>
/// <param name="Tier">Tier assigned to this position by its supporting books.</param>
/// <param name="Attestations">Books that support this position.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record DivergentBranch(
    string Position,
    Tier Tier,
    IReadOnlyList<Attestation> Attestations);
