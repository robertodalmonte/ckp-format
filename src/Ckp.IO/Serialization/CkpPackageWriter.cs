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
/// <para>
/// P2: each non-manifest entry is serialized straight into the ZIP entry stream via
/// <c>JsonSerializer.SerializeAsync</c> — no intermediate <c>byte[]</c> buffer. The
/// content hash is computed in a separate first pass using a single reusable scratch
/// buffer (see <see cref="CkpContentHash.ComputeForPlanAsync"/>). Peak write-time heap
/// allocation is now one entry's serialized bytes at a time rather than the whole
/// package. The manifest is still buffered (it's canonicalized via
/// <see cref="CkpCanonicalJson"/>) because it has to land at its own sorted position
/// in the archive only after the hash pass completes.
/// </para>
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users. Concrete implementation of
/// <see cref="ICkpPackageWriter"/>; prefer the interface in DI. Output is
/// deterministic — sorted entries, pinned timestamps, and canonical JSON for
/// the manifest — so identical inputs produce byte-identical archives.
/// </remarks>
public sealed class CkpPackageWriter : ICkpPackageWriter
{
    /// <summary>
    /// Fixed timestamp stamped onto every ZIP entry. Matches the epoch used by Nuke,
    /// Gradle, and the reproducible-builds project for deterministic ZIP output.
    /// </summary>
    internal static readonly DateTimeOffset DeterministicEpoch =
        new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private const string ManifestEntryName = "manifest.json";

    public async Task WriteAsync(CkpPackage package, Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(stream);

        // Build the entry plan once; walk it twice (hash pass, write pass). Both passes
        // serialize from the same closures against the same pre-sorted input lists, so
        // the hashed bytes are provably the bytes that end up in the archive.
        var plan = PackageEntrySerializer.PlanEntries(package);

        var computedHash = await CkpContentHash.ComputeForPlanAsync(plan, cancellationToken);
        var manifest = ReconcileManifestHash(package.Manifest, computedHash);
        var manifestBytes = CkpCanonicalJson.Serialize(manifest);

        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);

        // Merge the manifest into its sorted position without rebuilding the plan list.
        // The plan is already ordinal-sorted; we just scan for the first entry whose
        // name sorts after "manifest.json" and splice the manifest in there.
        bool manifestWritten = false;
        foreach (var entry in plan)
        {
            if (!manifestWritten && StringComparer.Ordinal.Compare(ManifestEntryName, entry.Name) < 0)
            {
                await EmitManifestAsync(archive, manifestBytes, cancellationToken);
                manifestWritten = true;
            }

            var zipEntry = archive.CreateEntry(entry.Name, CompressionLevel.Optimal);
            zipEntry.LastWriteTime = DeterministicEpoch;
            await using var entryStream = zipEntry.Open();
            await entry.WriteToAsync(entryStream, cancellationToken);
        }

        if (!manifestWritten)
            await EmitManifestAsync(archive, manifestBytes, cancellationToken);
    }

    private static async Task EmitManifestAsync(
        ZipArchive archive, byte[] manifestBytes, CancellationToken cancellationToken)
    {
        var zipEntry = archive.CreateEntry(ManifestEntryName, CompressionLevel.Optimal);
        zipEntry.LastWriteTime = DeterministicEpoch;
        await using var entryStream = zipEntry.Open();
        await entryStream.WriteAsync(manifestBytes, cancellationToken);
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
