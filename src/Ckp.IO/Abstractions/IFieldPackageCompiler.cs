namespace Ckp.IO;

using Ckp.Core;
using Ckp.Core.Field;

/// <summary>
/// The CKP 2.0 compiler. Takes CKP 1.0 source packages and alignment proposals,
/// and produces a compiled <see cref="FieldPackage"/>. Handles consensus tier computation,
/// confidence scoring, turbulence detection, status classification, and T0 back-propagation.
/// </summary>
public interface IFieldPackageCompiler
{
    /// <summary>
    /// Compiles a field package from one or more CKP 1.0 packages and their alignment proposals.
    /// </summary>
    /// <param name="fieldId">Field identifier (e.g., "orthodontics").</param>
    /// <param name="version">Compilation version (e.g., "2026.4").</param>
    /// <param name="packages">CKP 1.0 source packages to compile.</param>
    /// <param name="proposals">Alignment proposals (from <see cref="IAlignmentProposer"/>).</param>
    /// <param name="bookAuthorities">Base authority per book (keyed by book key). Defaults to 1.0.</param>
    /// <param name="autoMergeThreshold">Minimum alignment score for automatic merging. Default 0.7.</param>
    /// <returns>The compiled field package with metadata about the compilation process.</returns>
    CompilationResult Compile(
        string fieldId,
        string version,
        IReadOnlyList<CkpPackage> packages,
        IReadOnlyList<AlignmentProposal> proposals,
        IReadOnlyDictionary<string, double>? bookAuthorities = null,
        double autoMergeThreshold = 0.7);
}
