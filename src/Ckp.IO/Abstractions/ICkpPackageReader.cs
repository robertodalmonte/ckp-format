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

    /// <summary>
    /// Strict-mode read (S3). Enforces the integrity checks selected on
    /// <paramref name="options"/> — requires a signature, recomputes the content hash,
    /// pins the public key, or verifies the Ed25519 signature. Any failing check throws
    /// <see cref="CkpFormatException"/>.
    /// </summary>
    Task<CkpPackage> ReadAsync(Stream stream, CkpReadOptions options, CancellationToken cancellationToken = default);
}
