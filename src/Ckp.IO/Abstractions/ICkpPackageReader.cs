namespace Ckp.IO;

using Ckp.Core;

/// <summary>
/// Reads a .ckp (Consilience Knowledge Package) ZIP archive and hydrates the
/// full <see cref="CkpPackage"/> domain aggregate.
/// </summary>
public interface ICkpPackageReader
{
    /// <summary>
    /// Reads a .ckp archive from a stream and returns the fully hydrated package.
    /// </summary>
    /// <param name="stream">Readable stream containing the .ckp ZIP archive.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deserialized package with all claims, evidence, and structure.</returns>
    Task<CkpPackage> ReadAsync(Stream stream, CancellationToken cancellationToken = default);
}
