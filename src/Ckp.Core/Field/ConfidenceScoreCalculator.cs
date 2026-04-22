namespace Ckp.Core.Field;

/// <summary>
/// Pure computation of attestation weights and confidence scores using the locked
/// exponential decay formula with survival bonus.
/// <para>
/// Weight = BaseAuthority × (1 + α·ln(n_editions)) × e^(-λ·(Y_current − Y_pub))
/// </para>
/// <para>Defaults: λ = 0.058 (half-life ≈ 12 years), α = 0.1.</para>
/// <para>
/// <see cref="ComputeBreakdown"/> is the single source of truth. <see cref="ComputeWeight"/>
/// and <see cref="ComputeScore"/> both route through it — there is no separate inline
/// formula anywhere in the codebase.
/// </para>
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Pure static helper — no state, no I/O.
/// Safe to call from any thread; commonly used by transpilers, validators, and tests.
/// </remarks>
public static class ConfidenceScoreCalculator
{
    /// <summary>Default decay rate constant. Half-life ≈ 12 years for medical/biological fields.</summary>
    public const double DefaultLambda = 0.058;

    /// <summary>Default survival bonus scaling factor.</summary>
    public const double DefaultAlpha = 0.1;

    /// <summary>
    /// Computes a full <see cref="WeightBreakdown"/> for one attestation. This is the
    /// canonical formula — all other helpers delegate here.
    /// </summary>
    public static WeightBreakdown ComputeBreakdown(
        double baseAuthority,
        int publicationYear,
        int currentYear,
        int editionsSurvived,
        double lambda = DefaultLambda,
        double alpha = DefaultAlpha)
    {
        int age = Math.Max(0, currentYear - publicationYear);
        double decayFactor = Math.Exp(-lambda * age);
        double survivalMultiplier = 1.0 + alpha * Math.Log(Math.Max(1, editionsSurvived));

        double total = baseAuthority * survivalMultiplier * decayFactor;

        // Decompose: imagine the sequence baseAuthority → baseAuthority·e^(-λ·age) → total.
        //   decayed       = baseAuthority · decayFactor
        //   decayPenalty  = baseAuthority − decayed         (≥ 0)
        //   survivalBonus = total − decayed                 (≥ 0, since multiplier ≥ 1)
        double decayed = baseAuthority * decayFactor;
        double decayPenalty = baseAuthority - decayed;
        double survivalBonus = total - decayed;

        return new WeightBreakdown(total, baseAuthority, decayPenalty, survivalBonus);
    }

    /// <summary>
    /// Computes the scalar weight for a single attestation. Shorthand for
    /// <c>ComputeBreakdown(...).Total</c>.
    /// </summary>
    public static double ComputeWeight(
        double baseAuthority,
        int publicationYear,
        int currentYear,
        int editionsSurvived,
        double lambda = DefaultLambda,
        double alpha = DefaultAlpha) =>
        ComputeBreakdown(baseAuthority, publicationYear, currentYear,
            editionsSurvived, lambda, alpha).Total;

    /// <summary>
    /// Aggregates pre-computed attestation weights into an auditable
    /// <see cref="ConfidenceScore"/>.
    /// <para>
    /// <c>FinalValue</c> is the arithmetic mean of <see cref="Attestation.Weight"/>.
    /// The decomposition sums are reconstructed from each attestation's self-contained
    /// inputs (<see cref="Attestation.BaseAuthority"/>, <see cref="Attestation.PublicationYear"/>,
    /// <see cref="Attestation.EditionsSurvived"/>) using the same <see cref="ComputeBreakdown"/>
    /// formula that produced the stored weights, so the audit view cannot drift from the
    /// operational value.
    /// </para>
    /// </summary>
    /// <param name="attestations">Attestations with <see cref="Attestation.Weight"/> populated
    /// by <see cref="ComputeWeight"/> (typically via the compiler's <c>BuildAttestation</c>).</param>
    /// <param name="currentYear">Reference year for decomposition. Defaults to UTC now.</param>
    /// <param name="lambda">Decay rate used for decomposition. Must match what produced the weights.</param>
    /// <param name="alpha">Survival bonus constant used for decomposition. Must match what produced the weights.</param>
    public static ConfidenceScore ComputeScore(
        IReadOnlyList<Attestation> attestations,
        int? currentYear = null,
        double lambda = DefaultLambda,
        double alpha = DefaultAlpha)
    {
        if (attestations.Count == 0)
            return new ConfidenceScore(0.0, 0.0, 0.0, 0.0);

        int year = currentYear ?? DateTimeOffset.UtcNow.Year;

        double totalWeight = 0;
        double totalBaseAuthority = 0;
        double totalDecayPenalty = 0;
        double totalSurvivalBonus = 0;

        foreach (var att in attestations)
        {
            totalWeight += att.Weight;

            var breakdown = ComputeBreakdown(
                att.BaseAuthority, att.PublicationYear, year,
                att.EditionsSurvived, lambda, alpha);

            totalBaseAuthority += breakdown.BaseAuthority;
            totalDecayPenalty += breakdown.DecayPenalty;
            totalSurvivalBonus += breakdown.SurvivalBonus;
        }

        double finalValue = totalWeight / attestations.Count;

        return new ConfidenceScore(
            FinalValue: finalValue,
            BaseAuthoritySum: totalBaseAuthority,
            DecayPenalty: totalDecayPenalty,
            SurvivalBonus: totalSurvivalBonus);
    }
}
