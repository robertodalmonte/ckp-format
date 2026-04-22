namespace Ckp.Core.Manifest;

/// <summary>
/// Ed25519 cryptographic signature block for a .ckp package manifest.
/// </summary>
/// <param name="Algorithm">Signing algorithm (always "Ed25519").</param>
/// <param name="PublicKey">Base64-encoded Ed25519 public key.</param>
/// <param name="Signature">Base64-encoded signature over the manifest content hash.</param>
/// <param name="Source">Trust tier of the signer.</param>
/// <remarks>
/// <b>Intended consumer:</b> library users. Part of the CKP 1.x wire contract —
/// serialized into the package manifest or a section file and consumed by every
/// CKP reader, writer, and validator.
/// </remarks>
public sealed record PackageSignature(
    string Algorithm,
    string PublicKey,
    string Signature,
    SignatureSource Source);
