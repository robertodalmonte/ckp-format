namespace Ckp.Signing;

using Ckp.Core;

/// <summary>
/// Ed25519 signing and verification for .ckp package manifests.
/// <para>
/// <see cref="SignManifest"/> and <see cref="VerifyManifest"/> are the high-level
/// helpers most callers should use — they route through the canonical JSON
/// serializer so signed bytes are stable across re-serialization.
/// <see cref="Sign"/> and <see cref="Verify"/> remain available as low-level primitives
/// for callers that need to sign raw bytes.
/// </para>
/// </summary>
public interface ICkpSigner
{
    /// <summary>
    /// Signs <paramref name="manifest"/> and returns a new <see cref="PackageManifest"/>
    /// with <see cref="PackageManifest.Signature"/> populated. Internally:
    /// strips the existing signature (if any), canonicalizes via RFC 8785 JCS, signs the bytes,
    /// attaches the result.
    /// </summary>
    PackageManifest SignManifest(PackageManifest manifest, ReadOnlySpan<byte> privateKey, SignatureSource source);

    /// <summary>
    /// Verifies the signature attached to <paramref name="manifest"/> against its canonicalized
    /// unsigned form. Returns <see langword="false"/> if the manifest has no signature.
    /// </summary>
    bool VerifyManifest(PackageManifest manifest);

    /// <summary>
    /// Low-level: signs arbitrary bytes and returns a <see cref="PackageSignature"/> block.
    /// Prefer <see cref="SignManifest"/> for manifest payloads.
    /// </summary>
    /// <param name="manifestJson">Raw bytes to sign.</param>
    /// <param name="privateKey">Ed25519 private key bytes.</param>
    /// <param name="source">Trust tier of the signer.</param>
    PackageSignature Sign(ReadOnlySpan<byte> manifestJson, ReadOnlySpan<byte> privateKey, SignatureSource source);

    /// <summary>
    /// Low-level: verifies a signature over arbitrary bytes. Prefer <see cref="VerifyManifest"/>
    /// for manifest payloads.
    /// </summary>
    bool Verify(ReadOnlySpan<byte> manifestJson, PackageSignature signature);

    /// <summary>
    /// Generates a new Ed25519 key pair for signing packages.
    /// </summary>
    /// <returns>Tuple of (privateKey, publicKey) as byte arrays.</returns>
    (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair();
}
