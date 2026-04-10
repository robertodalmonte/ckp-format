namespace Ckp.Core;

/// <summary>
/// Ed25519 cryptographic signature block for a .ckp package manifest.
/// </summary>
/// <param name="Algorithm">Signing algorithm (always "Ed25519").</param>
/// <param name="PublicKey">Base64-encoded Ed25519 public key.</param>
/// <param name="Signature">Base64-encoded signature over the manifest content hash.</param>
/// <param name="Source">Trust tier of the signer.</param>
public sealed record PackageSignature(
    string Algorithm,
    string PublicKey,
    string Signature,
    SignatureSource Source);
