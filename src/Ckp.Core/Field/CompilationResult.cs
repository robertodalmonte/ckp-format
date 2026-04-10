namespace Ckp.Core.Field;

/// <summary>
/// Output of the Field Package Compiler. Contains the compiled field package
/// and metadata about the compilation process (auto-merged vs. review-needed).
/// </summary>
/// <param name="Package">The compiled CKP 2.0 field package.</param>
/// <param name="AutoMergedCount">Number of alignment proposals that merged automatically.</param>
/// <param name="FrontierCount">Number of claims with only one attestation (no alignment found).</param>
/// <param name="ReviewNeeded">Proposals that fell below the auto-merge threshold.</param>
public sealed record CompilationResult(
    FieldPackage Package,
    int AutoMergedCount,
    int FrontierCount,
    IReadOnlyList<AlignmentProposal> ReviewNeeded);
