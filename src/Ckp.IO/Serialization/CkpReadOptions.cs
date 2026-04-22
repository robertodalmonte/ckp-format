namespace Ckp.IO;

/// <summary>
/// Strict-read options for <see cref="CkpPackageReader"/>. The default instance matches
/// pre-S3 behaviour (accept anything parseable). Callers that care about integrity opt in
/// by constructing an options instance with the relevant requirements flipped on.
/// <para>
/// Rationale (S4 threat model): the threats classified as "pending S3" are those the
/// signer cannot detect on its own — a missing signature, a content-hash mismatch with
/// the archive body, or a signature signed by an unexpected key. Once an option is set
/// to <c>true</c>, a failing read throws <see cref="CkpFormatException"/> with a
/// specific <see cref="CkpFormatException.EntryName"/> so consumers can surface the
/// exact reason.
/// </para>
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users opting into S3 strict-read semantics.
/// </remarks>
public sealed record CkpReadOptions
{
    /// <summary>Default options — permissive, behaviour-preserving with pre-S3 readers.</summary>
    public static CkpReadOptions Default { get; } = new();

    /// <summary>
    /// When true, reject any package whose manifest has a null <see cref="PackageSignature"/>.
    /// Defends against T-DOWNGRADE-UNSIGNED (signature strip) from the threat model.
    /// </summary>
    public bool RequireSignature { get; init; }

    /// <summary>
    /// When true, recompute the S1 content hash over the archive body and reject the read
    /// if the manifest's <see cref="ContentFingerprint.Hash"/> is null or does not match.
    /// Defends against T-BYTE and T-ADD.
    /// </summary>
    public bool RequireContentHash { get; init; }

    /// <summary>
    /// When non-null, the package's <see cref="PackageSignature.PublicKey"/> must equal
    /// this string (ordinal). Defends against T-FORGE-KEY — without a pinned expected key,
    /// a valid-signature check proves only that *someone* signed the bytes. Base64 form.
    /// </summary>
    public string? ExpectedPublicKey { get; init; }

    /// <summary>
    /// When true, verify the Ed25519 signature as part of <see cref="CkpPackageReader.ReadAsync"/>.
    /// Implied by <see cref="RequireSignature"/>. Requires Ckp.Signing via the
    /// <see cref="SignatureVerifier"/> delegate to avoid a reverse project reference.
    /// </summary>
    public bool VerifySignature { get; init; }

    /// <summary>
    /// Optional verifier delegate. The reader cannot depend on <c>Ckp.Signing</c> directly
    /// (that would create a cycle), so callers pass a function that wraps
    /// <c>CkpSigner.VerifyManifest</c>. When <see cref="VerifySignature"/> is true but this
    /// is null, the reader throws to make the misconfiguration obvious rather than
    /// silently skipping verification.
    /// </summary>
    public Func<PackageManifest, bool>? SignatureVerifier { get; init; }
}
