namespace Ckp.IO;

using Ckp.Core;

/// <summary>
/// Writes a <see cref="CkpPackage"/> domain aggregate to a .ckp ZIP archive.
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users that emit CKP packages — transpilers, editors,
/// compilers. The output is deterministic (lexicographic entry order, pinned timestamps,
/// canonical manifest JSON) so two semantically equal packages produce byte-identical
/// archives.
/// </remarks>
public interface ICkpPackageWriter
{
    /// <summary>
    /// Serializes the package to a .ckp ZIP archive and writes it to the output stream.
    /// </summary>
    /// <param name="package">The package to serialize.</param>
    /// <param name="stream">Writable output stream for the .ckp archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WriteAsync(CkpPackage package, Stream stream, CancellationToken cancellationToken = default);
}
