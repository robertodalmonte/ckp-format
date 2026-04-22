namespace Ckp.Tests;

using Ckp.Core;
using Ckp.Signing;

/// <summary>
/// S6 — pins the invariant that no diagnostic / debugger / logger path accidentally
/// prints raw private-key bytes. Every assertion below works by computing what the
/// private key would look like in the common encodings (hex, base64) and proving
/// that substring never appears in any candidate <c>ToString()</c>.
/// <para>
/// If any future change starts formatting <see cref="System.Byte"/> arrays inside these
/// types' <c>ToString()</c> — for example by swapping records for classes with custom
/// formatting, or by switching to a debug-friendly <c>DebuggerDisplay</c> — these
/// tests will fail before the leak ships.
/// </para>
/// </summary>
public sealed class CkpSignerToStringLeakTests
{
    private readonly CkpSigner _signer = new();

    [Fact]
    public void GenerateKeyPair_tuple_ToString_does_not_expose_private_key_bytes()
    {
        var keypair = _signer.GenerateKeyPair();

        var rendered = keypair.ToString();

        AssertNoKeyMaterial(rendered, keypair.PrivateKey, "tuple ToString");
    }

    [Fact]
    public void PackageSignature_ToString_does_not_expose_private_key_bytes()
    {
        // PackageSignature never owns the private key — only the public key + signature.
        // This is a belt-and-braces check: sign a real message, then prove the private
        // scalar cannot be reconstructed from the rendered record.
        var (privateKey, _) = _signer.GenerateKeyPair();
        var signature = _signer.Sign("probe"u8.ToArray(), privateKey, SignatureSource.Publisher);

        var rendered = signature.ToString();

        AssertNoKeyMaterial(rendered, privateKey, "PackageSignature ToString");
    }

    [Fact]
    public void SignedManifest_ToString_does_not_expose_private_key_bytes()
    {
        // Full manifest round-trip: sign → render. The manifest carries a signature
        // block, so the rendered string legitimately contains the public key (base64)
        // and signature (base64). It must not contain the private key in any encoding.
        var (privateKey, _) = _signer.GenerateKeyPair();
        var manifest = CreateManifest();
        var signed = _signer.SignManifest(manifest, privateKey, SignatureSource.Author);

        var rendered = signed.ToString();

        AssertNoKeyMaterial(rendered, privateKey, "PackageManifest ToString");
    }

    private static void AssertNoKeyMaterial(string rendered, byte[] privateKey, string because)
    {
        // Check every encoding the key could reasonably take. Pick a window that avoids
        // false-positive collisions on 1-2 byte prefixes — 8 bytes of the private key
        // is already 1-in-2^64, so a substring match is not accidental.
        var sliceLen = Math.Min(8, privateKey.Length);
        var slice = privateKey.AsSpan(0, sliceLen).ToArray();

        string hex = Convert.ToHexString(slice);
        string hexLower = hex.ToLowerInvariant();
        string b64 = Convert.ToBase64String(slice);

        rendered.Should().NotContain(hex,
            $"{because} must not contain even a prefix of the private key in hex");
        rendered.Should().NotContain(hexLower,
            $"{because} must not contain even a prefix of the private key in lowercase hex");
        rendered.Should().NotContain(b64,
            $"{because} must not contain even a prefix of the private key in base64");
    }

    private static PackageManifest CreateManifest()
    {
        var book = new BookMetadata(
            "leak-probe-1e", "Leak Probe", 1, ["Author"], "Pub", 2026, null, "en-US",
            ["biomechanics"]);
        var fp = new ContentFingerprint("SHA-256", 1, 0, 0, 0, 0, 0, 0);
        return PackageManifest.CreateNew(book, fp);
    }
}
