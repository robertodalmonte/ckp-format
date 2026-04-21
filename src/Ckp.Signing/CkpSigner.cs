namespace Ckp.Signing;

using Ckp.Core;
using Ckp.IO;
using NSec.Cryptography;

/// <summary>
/// Ed25519 signing and verification for .ckp package manifests using NSec.Cryptography.
/// Manifest payloads are canonicalized via <see cref="CkpCanonicalJson"/> before signing
/// so that re-serialization with different property order / whitespace / null handling
/// cannot invalidate an otherwise-unchanged manifest.
/// </summary>
public sealed class CkpSigner : ICkpSigner
{
    private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;

    public PackageManifest SignManifest(
        PackageManifest manifest,
        ReadOnlySpan<byte> privateKey,
        SignatureSource source)
    {
        byte[] canonical = CkpCanonicalJson.SerializeForSigning(manifest);
        PackageSignature signature = Sign(canonical, privateKey, source);
        return manifest with { Signature = signature };
    }

    public bool VerifyManifest(PackageManifest manifest)
    {
        if (manifest.Signature is null) return false;
        byte[] canonical = CkpCanonicalJson.SerializeForSigning(manifest);
        return Verify(canonical, manifest.Signature);
    }

    public PackageSignature Sign(ReadOnlySpan<byte> manifestJson, ReadOnlySpan<byte> privateKeyBytes, SignatureSource source)
    {
        using var key = Key.Import(Algorithm, privateKeyBytes, KeyBlobFormat.RawPrivateKey);
        byte[] signature = Algorithm.Sign(key, manifestJson);
        byte[] publicKey = key.Export(KeyBlobFormat.RawPublicKey);

        return new PackageSignature(
            Algorithm: "Ed25519",
            PublicKey: Convert.ToBase64String(publicKey),
            Signature: Convert.ToBase64String(signature),
            Source: source);
    }

    public bool Verify(ReadOnlySpan<byte> manifestJson, PackageSignature signature)
    {
        byte[] publicKeyBytes = Convert.FromBase64String(signature.PublicKey);
        byte[] signatureBytes = Convert.FromBase64String(signature.Signature);

        var publicKey = PublicKey.Import(Algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
        return Algorithm.Verify(publicKey, manifestJson, signatureBytes);
    }

    public (byte[] PrivateKey, byte[] PublicKey) GenerateKeyPair()
    {
        var creationParameters = new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport };
        using var key = Key.Create(Algorithm, creationParameters);
        byte[] privateKey = key.Export(KeyBlobFormat.RawPrivateKey);
        byte[] publicKey = key.Export(KeyBlobFormat.RawPublicKey);
        return (privateKey, publicKey);
    }
}
