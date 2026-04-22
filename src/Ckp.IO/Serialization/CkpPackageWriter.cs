namespace Ckp.IO;

using System.IO.Compression;
using Ckp.Core;

/// <summary>
/// Writes a <see cref="CkpPackage"/> to a .ckp ZIP archive following the directory layout
/// defined in the CKP format specification.
/// <para>
/// Output is byte-deterministic: entries are added in a fixed sorted order, each entry's
/// <see cref="ZipArchiveEntry.LastWriteTime"/> is pinned to <see cref="DeterministicEpoch"/>,
/// and the manifest is canonicalized via <see cref="CkpCanonicalJson"/>. Identical input
/// packages therefore produce byte-identical archives, which is required for content
/// addressing and Ed25519-signature stability.
/// </para>
/// <para>
/// <b>S1 content-hash contract.</b> Every non-manifest entry is hashed (see
/// <see cref="CkpContentHash"/>) and the digest lands in <see cref="ContentFingerprint.Hash"/>.
/// The writer enforces one of two hash states on input:
/// </para>
/// <list type="number">
///   <item>
///     <b>Hash null.</b> The writer computes and injects the hash. Any attached
///     <see cref="PackageManifest.Signature"/> is stripped because it cannot have covered
///     the hash. Callers who want a signed package must follow the hash-then-sign flow:
///     call <see cref="CkpContentHash.ComputeForPackageAsync"/>, inject the hash into
///     the manifest, sign via <c>CkpSigner</c>, then write.
///   </item>
///   <item>
///     <b>Hash pre-populated.</b> The writer recomputes and asserts the two match. Any
///     attached signature is preserved — by contract it was computed over the manifest
///     including this hash, so it transitively covers the whole package. A mismatch
///     throws <see cref="CkpFormatException"/> because emitting a manifest with a stale
///     hash would silently corrupt downstream verification.
///   </item>
/// </list>
/// </summary>
public sealed class CkpPackageWriter : ICkpPackageWriter
{
    /// <summary>
    /// Fixed timestamp stamped onto every ZIP entry. Matches the epoch used by Nuke,
    /// Gradle, and the reproducible-builds project for deterministic ZIP output.
    /// </summary>
    internal static readonly DateTimeOffset DeterministicEpoch =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public async Task WriteAsync(CkpPackage package, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(stream);

        var nonManifestEntries = await PackageEntrySerializer.SerializeAsync(package, cancellationToken);
        var computedHash = CkpContentHash.Compute(nonManifestEntries);

        var manifest = ReconcileManifestHash(package.Manifest, computedHash);
        var manifestBytes = CkpCanonicalJson.Serialize(manifest);

        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        // Collect every entry including the manifest and emit in sorted order so the ZIP
        // central directory is stable.
        var allEntries = new List<(string Name, byte[] Bytes)>(nonManifestEntries.Count + 1)
        {
            ("manifest.json", manifestBytes),
        };
        allEntries.AddRange(nonManifestEntries);

        foreach (var (name, bytes) in allEntries.OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            var zipEntry = archive.CreateEntry(name, CompressionLevel.Optimal);
            zipEntry.LastWriteTime = DeterministicEpoch;
            await using var entryStream = zipEntry.Open();
            await entryStream.WriteAsync(bytes, cancellationToken);
        }
    }

    private static PackageManifest ReconcileManifestHash(PackageManifest manifest, string computedHash)
    {
        var fp = manifest.ContentFingerprint;

        if (fp.Hash is null)
        {
            // Hash was not pre-computed. Inject it and drop any attached signature — a
            // signature on a manifest without the content hash could not have covered
            // the content, so preserving it would create a security-misleading artefact.
            return manifest with
            {
                ContentFingerprint = fp with { Hash = computedHash },
                Signature = null,
            };
        }

        if (!string.Equals(fp.Hash, computedHash, StringComparison.Ordinal))
        {
            throw new CkpFormatException(
                $"Manifest content hash '{fp.Hash}' does not match the computed hash '{computedHash}' " +
                "for the package body. Re-run CkpContentHash.ComputeForPackageAsync after mutating the package, " +
                "then re-sign before calling WriteAsync.",
                entryName: "manifest.json");
        }

        // Hash matches — caller followed the hash-then-sign flow. Preserve the manifest
        // (including signature) verbatim.
        return manifest;
    }
}
