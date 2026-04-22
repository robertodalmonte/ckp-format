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
/// <remarks>
/// <b>Intended consumer:</b> library users. Indirects Ckp.Signing's one stateful
/// surface (Ed25519 via NSec) so applications can mock it in tests.
/// </remarks>
public interface ICkpSigner
{
    /// <summary>
    /// Signs <paramref name="manifest"/> and returns a new <see cref="PackageManifest"/>
    /// with <see cref="PackageManifest.Signature"/> populated. Internally:
    /// strips the existing signature (if any), canonicalizes via RFC 8785 JCS, signs the bytes,
    /// attaches the result.
    /// </summary>
    /// <remarks>
    /// <paramref name="privateKey"/> is <see cref="ReadOnlySpan{T}"/> specifically so the
    /// caller retains ownership and can zero the backing array as soon as signing completes.
    /// See <see cref="GenerateKeyPair"/> remarks for S7 lifetime guidance.
    /// </remarks>
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
    /// <remarks>
    /// <para>
    /// <b>Key-material lifetime (S7).</b> The returned <c>PrivateKey</c> array contains
    /// raw Ed25519 scalar bytes. Callers are responsible for:
    /// </para>
    /// <list type="bullet">
    ///   <item>Keeping the array off persistent / unencrypted storage (writing to a
    ///         protected keystore, HSM, OS credential manager, or user-supplied sealed
    ///         file — not a plain-text file alongside the package).</item>
    ///   <item>Zeroing the array as soon as signing is done, via
    ///         <c>System.Security.Cryptography.CryptographicOperations.ZeroMemory(privateKey)</c>
    ///         or <c>Array.Clear(privateKey)</c>. Once the array is zeroed, subsequent
    ///         <see cref="SignManifest"/> / <see cref="Sign"/> calls with that span will
    ///         throw, which is the intended signal that the key has been disposed.</item>
    ///   <item>Not boxing the private key into long-lived objects, collection types, or
    ///         closures whose lifetime you cannot see.</item>
    /// </list>
    /// <para>
    /// A future breaking change may replace the returned <see cref="T:System.Byte"/>[]
    /// with <c>System.Buffers.IMemoryOwner&lt;byte&gt;</c> or a <c>ReadOnlySpan&lt;byte&gt;</c>
    /// callback so the signer owns the lifetime and zeroing is automatic. Prefer
    /// scoping the private-key array to a narrow <c>using</c>-style block today so that
    /// migration is mechanical when it arrives.
    /// </para>
    /// </remarks>
    (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair();
}
