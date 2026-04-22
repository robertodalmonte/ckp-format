namespace Ckp.Core.Field;

/// <summary>
/// Output of the Field Package Compiler. Contains the compiled field package
/// and metadata about the compilation process (auto-merged vs. review-needed).
/// </summary>
/// <param name="Package">The compiled CKP 2.0 field package.</param>
/// <param name="AutoMergedCount">Number of alignment proposals that merged automatically.</param>
/// <param name="FrontierCount">Number of claims with only one attestation (no alignment found).</param>
/// <param name="ReviewNeeded">Proposals that fell below the auto-merge threshold.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record CompilationResult(
    FieldPackage Package,
    int AutoMergedCount,
    int FrontierCount,
    IReadOnlyList<AlignmentProposal> ReviewNeeded);
