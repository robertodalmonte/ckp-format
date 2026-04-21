namespace Ckp.Core.Field;

/// <summary>
/// A field-scoped claim deduplicated across authors. The core entity of CKP 2.0.
/// Immutable by design — if new data arrives, the Alignment Engine recompiles.
/// </summary>
/// <param name="CanonicalId">Semantic URN: ckp:{field}:{domain}:{phenomenon} (e.g., "ckp:ortho:biomech:fak-osteoclast-cascade").</param>
/// <param name="Status">Epistemic lifecycle: Frontier → Converged → Divergent.</param>
/// <param name="Statement">The canonical assertion. For divergent claims, this is the common framing; branches hold the contradictions.</param>
/// <param name="ConsensusTier">Weight-adjusted consensus tier (T1–T4). Meaningless when Status is Divergent.</param>
/// <param name="Confidence">Auditable confidence score with decomposition.</param>
/// <param name="Attestations">Provenance links back to CKP 1.0 claims. N books = N attestations.</param>
/// <param name="VocabularyMap">Each book's term for the same concept (BookId → term string).</param>
/// <param name="T0Constraints">Inherited T0 axiom URNs. Back-propagated from any attesting book.</param>
/// <param name="Turbulence">Non-null when a recent authoritative source diverges from consensus.</param>
/// <param name="Branches">Populated only when Status is Divergent. Null otherwise.</param>
public sealed record CanonicalClaim(
    string CanonicalId,
    ClaimStatus Status,
    string Statement,
    Tier ConsensusTier,
    ConfidenceScore Confidence,
    IReadOnlyList<Attestation> Attestations,
    IReadOnlyDictionary<string, string> VocabularyMap,
    IReadOnlyList<string> T0Constraints,
    TurbulenceFlag? Turbulence,
    IReadOnlyList<DivergentBranch>? Branches);
