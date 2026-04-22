namespace Ckp.Core.Field;

/// <summary>
/// Decomposition of a single attestation's weight into its three contributing
/// components. Produced by <see cref="ConfidenceScoreCalculator.ComputeBreakdown"/>
/// so that a reviewer can see exactly which term (decay vs. survival) moved the
/// weight in either direction.
/// <para>
/// Total = BaseAuthority × (1 + α·ln(editions)) × e^(-λ·age)
///       = (BaseAuthority − DecayPenalty) + SurvivalBonus
/// </para>
/// </summary>
/// <param name="Total">The final weight used by <see cref="Attestation.Weight"/>.</param>
/// <param name="BaseAuthority">The book's base authority, pre-decay, pre-bonus.</param>
/// <param name="DecayPenalty">Weight lost to exponential decay (always ≥ 0).</param>
/// <param name="SurvivalBonus">Weight gained from edition survival (always ≥ 0).</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public readonly record struct WeightBreakdown(
    double Total,
    double BaseAuthority,
    double DecayPenalty,
    double SurvivalBonus);
