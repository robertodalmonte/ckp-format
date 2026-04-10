namespace Ckp.Core;

/// <summary>
/// Factory methods for creating <see cref="PackageManifest"/> instances.
/// </summary>
public static class PackageManifestConstruction
{
    extension(PackageManifest)
    {
        /// <summary>
        /// Creates a new unsigned manifest with a freshly generated UUID and current timestamp.
        /// </summary>
        public static PackageManifest CreateNew(
            BookMetadata book,
            ContentFingerprint fingerprint,
            T0RegistryReference? t0Registry = null,
            IReadOnlyList<AlignmentSummary>? alignments = null) => new(
                FormatVersion: "1.0",
                PackageId: Guid.CreateVersion7(),
                CreatedAt: DateTimeOffset.UtcNow,
                Signature: null,
                Book: book,
                ContentFingerprint: fingerprint,
                T0Registry: t0Registry,
                Alignments: alignments ?? []);

        /// <summary>
        /// Restores a manifest from serialized data with all fields explicit.
        /// </summary>
        public static PackageManifest Restore(
            string formatVersion,
            Guid packageId,
            DateTimeOffset createdAt,
            PackageSignature? signature,
            BookMetadata book,
            ContentFingerprint contentFingerprint,
            T0RegistryReference? t0Registry,
            IReadOnlyList<AlignmentSummary> alignments) => new(
                FormatVersion: formatVersion,
                PackageId: packageId,
                CreatedAt: createdAt,
                Signature: signature,
                Book: book,
                ContentFingerprint: contentFingerprint,
                T0Registry: t0Registry,
                Alignments: alignments);
    }
}
