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

    /// <summary>
    /// Verifies an Ed25519 signature over <paramref name="manifestJson"/>.
    /// <para>
    /// Hardened per QualityRaisingPlan S2: returns <c>false</c> (never throws) for any
    /// of the following tampering / confusion scenarios:
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="PackageSignature.Algorithm"/> is not <c>"Ed25519"</c> (ordinal, case-insensitive).</item>
    ///   <item><see cref="PackageSignature.PublicKey"/> or <see cref="PackageSignature.Signature"/> is not valid base64.</item>
    ///   <item>Decoded public key is not the 32-byte Ed25519 length.</item>
    ///   <item>Decoded signature is not the 64-byte Ed25519 length.</item>
    /// </list>
    /// <para>
    /// The only exception path is <see cref="ArgumentNullException"/> on a null
    /// <paramref name="signature"/>, which is a programming error, not a
    /// verification failure.
    /// </para>
    /// </summary>
    public bool Verify(ReadOnlySpan<byte> manifestJson, PackageSignature signature)
    {
        ArgumentNullException.ThrowIfNull(signature);

        // D10/S2: reject non-Ed25519 algorithm strings up-front. Without this, an attacker
        // who rewrites the algorithm field still verifies as long as the key+signature
        // happen to be Ed25519-shaped.
        if (!string.Equals(signature.Algorithm, "Ed25519", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryFromBase64(signature.PublicKey, out var publicKeyBytes)) return false;
        if (!TryFromBase64(signature.Signature, out var signatureBytes)) return false;

        // Ed25519 has fixed sizes: 32-byte public key, 64-byte signature.
        // NSec would throw on mismatch; we translate to a false result instead.
        if (publicKeyBytes.Length != 32) return false;
        if (signatureBytes.Length != 64) return false;

        PublicKey publicKey;
        try
        {
            publicKey = PublicKey.Import(Algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
        return Algorithm.Verify(publicKey, manifestJson, signatureBytes);
    }

    private static bool TryFromBase64(string value, out byte[] bytes)
    {
        // Overload with char-span + Span<byte> lets us avoid Convert.FromBase64String's throw path.
        // Upper bound the output size and trim to actual bytes written.
        bytes = [];
        if (value is null) return false;
        var maxLen = value.Length * 3 / 4 + 3;
        var buffer = new byte[maxLen];
        if (!Convert.TryFromBase64String(value, buffer, out int written))
            return false;
        if (written == buffer.Length)
        {
            bytes = buffer;
        }
        else
        {
            bytes = new byte[written];
            Buffer.BlockCopy(buffer, 0, bytes, 0, written);
        }
        return true;
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
