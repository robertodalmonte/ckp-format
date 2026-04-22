namespace Ckp.Core.Manifest;

/// <summary>
/// Statistical fingerprint of a .ckp package's content for quick integrity checks
/// and summary display without reading every claim.
/// <para>
/// <see cref="Hash"/> is the S1 cryptographic content hash covering every non-manifest
/// ZIP entry. When null (legacy packages written before S1), callers must treat the
/// package as unverifiable-at-content-level even if the manifest signature is valid.
/// </para>
/// </summary>
/// <param name="Algorithm">Hash algorithm used (e.g., "SHA-256").</param>
/// <param name="ClaimCount">Total number of claims in the package.</param>
/// <param name="DomainCount">Number of distinct domains.</param>
/// <param name="T1Count">Count of T1 (established mechanism) claims.</param>
/// <param name="T2Count">Count of T2 (supported hypothesis) claims.</param>
/// <param name="T3Count">Count of T3 (speculative bridge) claims.</param>
/// <param name="T4Count">Count of T4 (ancient observation) claims.</param>
/// <param name="CitationCount">Total number of citations across all claims.</param>
/// <param name="Hash">
/// Merkle-style SHA-256 digest of all non-manifest ZIP entries, in the form
/// <c>"sha256:&lt;64-hex&gt;"</c>. Computed on write by the package writer; null on
/// legacy packages. Verified by a reader's strict mode (S3). Signed transitively
/// because it sits inside the manifest, which <see cref="PackageSignature"/> covers.
/// </param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record ContentFingerprint(
    string Algorithm,
    int ClaimCount,
    int DomainCount,
    int T1Count,
    int T2Count,
    int T3Count,
    int T4Count,
    int CitationCount,
    string? Hash = null);
