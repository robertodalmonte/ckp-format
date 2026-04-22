namespace Ckp.Core.Manifest;

/// <summary>
/// Top-level manifest for a .ckp (Consilience Knowledge Package). Contains book metadata,
/// content fingerprint, signature, T0 registry reference, and alignment summaries.
/// Stored as manifest.json at the root of the ZIP archive.
/// </summary>
/// <param name="FormatVersion">CKP format version (e.g., "1.0").</param>
/// <param name="PackageId">Unique package identifier (UUID).</param>
/// <param name="CreatedAt">UTC timestamp of package creation.</param>
/// <param name="Signature">Ed25519 signature block, or null for unsigned/draft packages.</param>
/// <param name="Book">Book metadata.</param>
/// <param name="ContentFingerprint">Statistical fingerprint of the package content.</param>
/// <param name="T0Registry">Reference to the T0 axiom registry version used.</param>
/// <param name="Alignments">Summaries of cross-book alignments included in this package.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. This is the signed root of every CKP archive;
/// <see cref="SignatureSource"/> / <see cref="PackageSignature"/> / <see cref="ContentFingerprint"/>
/// all hang off it. Construct via <see cref="PackageManifestConstruction.CreateNew"/>
/// rather than the primary constructor unless you need full control over all fields.
/// </remarks>
public sealed record PackageManifest(
    string FormatVersion,
    Guid PackageId,
    DateTimeOffset CreatedAt,
    PackageSignature? Signature,
    BookMetadata Book,
    ContentFingerprint ContentFingerprint,
    T0RegistryReference? T0Registry,
    IReadOnlyList<AlignmentSummary> Alignments);
