namespace Ckp.IO;

using Ckp.Core;
using Ckp.Core.Field;

/// <summary>
/// Proposes claim alignments between two CKP 1.0 packages by scoring similarity
/// across MeSH terms, observables, keywords, and domain overlap. Does not merge —
/// it produces proposals for the <see cref="IFieldPackageCompiler"/> to consume.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users building the CKP 1.0 → 2.0 pipeline. The
/// proposer is stateless and pure; it does not hit the filesystem or network, so it is
/// safe to use inside batch jobs, test harnesses, and interactive review tools.
/// </remarks>
public interface IAlignmentProposer
{
    /// <summary>
    /// Proposes alignments between claims in two packages.
    /// </summary>
    /// <param name="source">The source CKP 1.0 package.</param>
    /// <param name="target">The target CKP 1.0 package.</param>
    /// <returns>Scored alignment proposals, ordered by descending confidence.</returns>
    IReadOnlyList<AlignmentProposal> Propose(CkpPackage source, CkpPackage target);
}
