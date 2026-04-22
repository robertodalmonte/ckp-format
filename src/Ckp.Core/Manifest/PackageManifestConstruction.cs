namespace Ckp.Core.Manifest;

/// <summary>
/// Factory methods for creating <see cref="PackageManifest"/> instances.
/// </summary>
public static class PackageManifestConstruction
{
    extension(PackageManifest)
    {
        /// <summary>
        /// Creates a new unsigned manifest with a freshly generated UUID and current timestamp.
        /// <para>
        /// A5 — both non-determinism sources are injectable: pass a fake
        /// <see cref="TimeProvider"/> and <paramref name="idFactory"/> to pin the
        /// <see cref="PackageManifest.CreatedAt"/> and <see cref="PackageManifest.PackageId"/>
        /// fields. Defaults are <see cref="TimeProvider.System"/> and
        /// <see cref="Guid.CreateVersion7()"/>, matching the pre-A5 behaviour exactly so
        /// existing callers compile and behave unchanged.
        /// </para>
        /// </summary>
        public static PackageManifest CreateNew(
            BookMetadata book,
            ContentFingerprint fingerprint,
            T0RegistryReference? t0Registry = null,
            IReadOnlyList<AlignmentSummary>? alignments = null,
            TimeProvider? timeProvider = null,
            Func<Guid>? idFactory = null) => new(
                FormatVersion: "1.0",
                PackageId: (idFactory ?? Guid.CreateVersion7)(),
                CreatedAt: (timeProvider ?? TimeProvider.System).GetUtcNow(),
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
