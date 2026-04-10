namespace Ckp.Core.Field;

/// <summary>
/// Auditable confidence score for a canonical claim. Stores not just the final value
/// but the full decomposition so that a reviewer (or the P5 agent) can inspect exactly
/// why a claim scored 0.94.
/// </summary>
/// <param name="FinalValue">Bottom-line confidence (0.0–1.0+, may exceed 1.0 with survival bonus).</param>
/// <param name="BaseAuthoritySum">Sum of base authority values across attestations.</param>
/// <param name="DecayPenalty">Total weight lost to exponential decay across attestations.</param>
/// <param name="SurvivalBonus">Total weight gained from edition survival across attestations.</param>
public sealed record ConfidenceScore(
    double FinalValue,
    double BaseAuthoritySum,
    double DecayPenalty,
    double SurvivalBonus);
