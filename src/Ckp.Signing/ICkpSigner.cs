namespace Ckp.Signing;

using Ckp.Core;

/// <summary>
/// Ed25519 signing and verification for .ckp package manifests.
/// </summary>
public interface ICkpSigner
{
    /// <summary>
    /// Signs the manifest content and returns a <see cref="PackageSignature"/> block.
    /// </summary>
    /// <param name="manifestJson">The serialized manifest JSON (without the signature field).</param>
    /// <param name="privateKey">Ed25519 private key bytes.</param>
    /// <param name="source">Trust tier of the signer.</param>
    /// <returns>A populated signature block ready to attach to the manifest.</returns>
    PackageSignature Sign(ReadOnlySpan<byte> manifestJson, ReadOnlySpan<byte> privateKey, SignatureSource source);

    /// <summary>
    /// Verifies a manifest signature against the embedded public key.
    /// </summary>
    /// <param name="manifestJson">The serialized manifest JSON (without the signature field).</param>
    /// <param name="signature">The signature block from the manifest.</param>
    /// <returns><see langword="true"/> if the signature is valid.</returns>
    bool Verify(ReadOnlySpan<byte> manifestJson, PackageSignature signature);

    /// <summary>
    /// Generates a new Ed25519 key pair for signing packages.
    /// </summary>
    /// <returns>Tuple of (privateKey, publicKey) as byte arrays.</returns>
    (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair();
}
