namespace Ckp.Core.Field;

/// <summary>
/// Pure computation of attestation weights and confidence scores using the locked
/// exponential decay formula with survival bonus.
/// <para>
/// Weight = Authority_base × (1 + α·ln(n_editions)) × e^(-λ·(Y_current - Y_pub))
/// </para>
/// <para>Defaults: λ = 0.058 (half-life ≈ 12 years), α = 0.1</para>
/// </summary>
public static class ConfidenceScoreCalculator
{
    /// <summary>Default decay rate constant. Half-life ≈ 12 years for medical/biological fields.</summary>
    public const double DefaultLambda = 0.058;

    /// <summary>Default survival bonus scaling factor.</summary>
    public const double DefaultAlpha = 0.1;

    /// <summary>
    /// Computes the weight for a single attestation.
    /// </summary>
    /// <param name="baseAuthority">Base authority of the source book (0.0–1.0).</param>
    /// <param name="publicationYear">Publication year of the book edition.</param>
    /// <param name="currentYear">Reference year for decay computation.</param>
    /// <param name="editionsSurvived">Number of editions the claim has survived (≥1).</param>
    /// <param name="lambda">Decay rate constant.</param>
    /// <param name="alpha">Survival bonus scaling factor.</param>
    public static double ComputeWeight(
        double baseAuthority,
        int publicationYear,
        int currentYear,
        int editionsSurvived,
        double lambda = DefaultLambda,
        double alpha = DefaultAlpha)
    {
        int age = currentYear - publicationYear;
        double survivalMultiplier = 1.0 + alpha * Math.Log(Math.Max(1, editionsSurvived));
        double decayFactor = Math.Exp(-lambda * Math.Max(0, age));
        return baseAuthority * survivalMultiplier * decayFactor;
    }

    /// <summary>
    /// Computes the aggregate confidence score from a set of attestation weights.
    /// </summary>
    /// <param name="attestations">Attestations with pre-computed weights.</param>
    /// <param name="baseAuthorities">Base authority per book (keyed by BookId). Defaults to 1.0 if not specified.</param>
    /// <param name="lambda">Decay rate constant used (stored in the score for audit).</param>
    /// <param name="alpha">Survival bonus constant used (stored in the score for audit).</param>
    /// <param name="currentYear">Reference year for decay computation.</param>
    public static ConfidenceScore ComputeScore(
        IReadOnlyList<Attestation> attestations,
        IReadOnlyDictionary<string, double>? baseAuthorities = null,
        double lambda = DefaultLambda,
        double alpha = DefaultAlpha,
        int? currentYear = null)
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
            double baseAuth = baseAuthorities is not null && baseAuthorities.TryGetValue(att.BookId, out var ba)
                ? ba
                : 1.0;

            double rawWeight = baseAuth * Math.Exp(-lambda * Math.Max(0, year - att.PublicationYear));
            double survivalBonus = baseAuth * alpha * Math.Log(Math.Max(1, att.EditionsSurvived))
                                   * Math.Exp(-lambda * Math.Max(0, year - att.PublicationYear));
            double fullWeight = rawWeight + survivalBonus;

            totalBaseAuthority += baseAuth;
            totalDecayPenalty += baseAuth - rawWeight;
            totalSurvivalBonus += survivalBonus;
            totalWeight += fullWeight;
        }

        double normalized = totalWeight / attestations.Count;

        return new ConfidenceScore(
            FinalValue: normalized,
            BaseAuthoritySum: totalBaseAuthority,
            DecayPenalty: totalDecayPenalty,
            SurvivalBonus: totalSurvivalBonus);
    }
}
