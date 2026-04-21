namespace Ckp.Core.Field;

/// <summary>
/// Detects epistemic turbulence when a new attestation diverges from the existing
/// consensus on a canonical claim.
/// <para>
/// Threshold formula: TurbulenceFires = (W_dissent / W_mean_consensus) ≥ τ_base / Δ_tier
/// </para>
/// <para>
/// With τ_base = 0.7:
/// <list type="bullet">
/// <item>1-tier gap: dissent needs ≥70% of consensus mean weight</item>
/// <item>2-tier gap: ≥35% (easier to trigger — larger gaps are more alarming)</item>
/// <item>3-tier gap: ≥23%</item>
/// </list>
/// </para>
/// </summary>
public static class TurbulenceDetector
{
    /// <summary>Default turbulence base threshold.</summary>
    public const double DefaultTauBase = 0.7;

    /// <summary>
    /// Evaluates whether a dissenting attestation should trigger a turbulence flag.
    /// </summary>
    /// <param name="dissentingAttestation">The attestation that disagrees with consensus.</param>
    /// <param name="consensusAttestations">Attestations that form the current consensus.</param>
    /// <param name="consensusTier">The consensus tier.</param>
    /// <param name="tauBase">Base threshold ratio. Default 0.7.</param>
    /// <returns>A turbulence flag if threshold is met, null otherwise.</returns>
    public static TurbulenceFlag? Evaluate(
        Attestation dissentingAttestation,
        IReadOnlyList<Attestation> consensusAttestations,
        Tier consensusTier,
        double tauBase = DefaultTauBase)
    {
        if (consensusAttestations.Count == 0)
            return null;

        int consensusTierValue = (int)consensusTier;
        int dissentTierValue = (int)dissentingAttestation.Tier;
        int tierDelta = Math.Abs(consensusTierValue - dissentTierValue);

        if (tierDelta == 0)
            return null;

        double meanConsensusWeight = consensusAttestations.Average(a => a.Weight);

        if (meanConsensusWeight <= 0)
            return null;

        double threshold = tauBase / tierDelta;
        double ratio = dissentingAttestation.Weight / meanConsensusWeight;

        if (ratio < threshold)
            return null;

        var direction = dissentTierValue > consensusTierValue
            ? TurbulenceDirection.Demotion
            : TurbulenceDirection.Promotion;

        return new TurbulenceFlag(
            TriggeredByBookId: dissentingAttestation.BookId,
            Direction: direction,
            TierDelta: tierDelta,
            Note: $"{dissentingAttestation.BookId} assigns {dissentingAttestation.Tier} " +
                  $"against consensus {consensusTier} (weight ratio {ratio:F2}, threshold {threshold:F2}).");
    }
}
