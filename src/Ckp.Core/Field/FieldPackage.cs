namespace Ckp.Core.Field;

/// <summary>
/// The CKP 2.0 root aggregate. A compiled binary produced by the Alignment Engine
/// from one or more CKP 1.0 source packages. Represents the state of knowledge
/// for an entire field (e.g., orthodontics) at a point in time.
/// </summary>
/// <param name="FieldId">Field identifier (e.g., "orthodontics", "fascia-science").</param>
/// <param name="Version">Compilation version (e.g., "2026.4"). Incremented on each recompile.</param>
/// <param name="CompiledAt">UTC timestamp of this compilation.</param>
/// <param name="SourcePackages">Book keys of all CKP 1.0 packages that contributed to this compilation.</param>
/// <param name="Claims">All canonical claims in this field package.</param>
/// <param name="DecayLambda">The λ decay constant used during compilation.</param>
/// <param name="SurvivalAlpha">The α survival bonus constant used during compilation.</param>
/// <param name="TurbulenceTauBase">The τ_base turbulence threshold used during compilation.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record FieldPackage(
    string FieldId,
    string Version,
    DateTimeOffset CompiledAt,
    IReadOnlyList<string> SourcePackages,
    IReadOnlyList<CanonicalClaim> Claims,
    double DecayLambda,
    double SurvivalAlpha,
    double TurbulenceTauBase);
