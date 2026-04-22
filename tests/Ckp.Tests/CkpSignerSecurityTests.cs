namespace Ckp.Tests;

using System.Text;
using Ckp.Core;
using Ckp.IO;
using Ckp.Signing;

/// <summary>
/// Negative-path security tests for <see cref="CkpSigner"/>. Covers items 14–18 in
/// <c>docs/Refactoring/QualityRaisingPlan.md</c> §3.1 and the S2 hardening contract
/// ("VerifyManifest/Verify never throw on malformed signature blocks — they return false").
/// </summary>
public sealed class CkpSignerSecurityTests
{
    private readonly CkpSigner _signer = new();

    // Item 14 — bad base64 in PublicKey.
    [Fact]
    public void Verify_returns_false_for_bad_base64_public_key()
    {
        var (priv, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("{\"k\":1}");
        var good = _signer.Sign(data, priv, SignatureSource.Publisher);
        var bad = good with { PublicKey = "!!!not-base64!!!" };

        var act = () => _signer.Verify(data, bad);

        act.Should().NotThrow();
        _signer.Verify(data, bad).Should().BeFalse();
    }

    // Item 14b — bad base64 in Signature.
    [Fact]
    public void Verify_returns_false_for_bad_base64_signature()
    {
        var (priv, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("{\"k\":1}");
        var good = _signer.Sign(data, priv, SignatureSource.Publisher);
        var bad = good with { Signature = "@@@nope@@@" };

        _signer.Verify(data, bad).Should().BeFalse();
    }

    // Item 15 — public key of wrong length (not 32 bytes). NSec would throw; we return false.
    [Fact]
    public void Verify_returns_false_for_public_key_of_wrong_length()
    {
        var (priv, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("{\"k\":1}");
        var good = _signer.Sign(data, priv, SignatureSource.Publisher);
        // Valid base64 but decodes to 16 bytes instead of 32.
        var shortKey = Convert.ToBase64String(new byte[16]);
        var bad = good with { PublicKey = shortKey };

        _signer.Verify(data, bad).Should().BeFalse();
    }

    // Item 15b — signature of wrong length (not 64 bytes).
    [Fact]
    public void Verify_returns_false_for_signature_of_wrong_length()
    {
        var (priv, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("{\"k\":1}");
        var good = _signer.Sign(data, priv, SignatureSource.Publisher);
        var shortSig = Convert.ToBase64String(new byte[32]);
        var bad = good with { Signature = shortSig };

        _signer.Verify(data, bad).Should().BeFalse();
    }

    // Item 17 — Algorithm mismatch. Without the check, a future "RSA" signature block
    // paired with Ed25519-shaped bytes would still verify. Now it must not.
    [Theory]
    [InlineData("RSA")]
    [InlineData("Ed448")]
    [InlineData("")]
    [InlineData(" ed25519 ")] // trailing/leading whitespace shouldn't pass a strict match
    public void Verify_returns_false_when_algorithm_is_not_ed25519(string badAlgorithm)
    {
        var (priv, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("{\"k\":1}");
        var good = _signer.Sign(data, priv, SignatureSource.Publisher);
        var swapped = good with { Algorithm = badAlgorithm };

        _signer.Verify(data, swapped).Should().BeFalse();
    }

    // Item 17b — algorithm match is ordinal-case-insensitive (Ed25519 / ED25519 / ed25519 all OK).
    [Theory]
    [InlineData("Ed25519")]
    [InlineData("ED25519")]
    [InlineData("ed25519")]
    public void Verify_accepts_algorithm_string_case_insensitively(string algorithm)
    {
        var (priv, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("{\"k\":1}");
        var good = _signer.Sign(data, priv, SignatureSource.Publisher);
        var relabeled = good with { Algorithm = algorithm };

        _signer.Verify(data, relabeled).Should().BeTrue();
    }

    // Item 14/15 — PublicKey and Signature fields swapped. Must not throw; must return false.
    [Fact]
    public void Verify_returns_false_when_public_key_and_signature_fields_are_swapped()
    {
        var (priv, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("{\"k\":1}");
        var good = _signer.Sign(data, priv, SignatureSource.Publisher);
        var swapped = good with { PublicKey = good.Signature, Signature = good.PublicKey };

        var act = () => _signer.Verify(data, swapped);

        act.Should().NotThrow();
        _signer.Verify(data, swapped).Should().BeFalse();
    }

    [Fact]
    public void Verify_throws_on_null_signature_argument()
    {
        byte[] data = Encoding.UTF8.GetBytes("{\"k\":1}");

        var act = () => _signer.Verify(data, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // VerifyManifest composes Verify; all of the above must also surface through it as false.
    [Fact]
    public void VerifyManifest_returns_false_for_algorithm_swap()
    {
        var (priv, _) = _signer.GenerateKeyPair();
        var manifest = BuildManifest();
        var signed = _signer.SignManifest(manifest, priv, SignatureSource.Publisher);
        var confused = signed with
        {
            Signature = signed.Signature! with { Algorithm = "RSA" },
        };

        _signer.VerifyManifest(confused).Should().BeFalse();
    }

    [Fact]
    public void VerifyManifest_returns_false_for_bad_base64()
    {
        var (priv, _) = _signer.GenerateKeyPair();
        var manifest = BuildManifest();
        var signed = _signer.SignManifest(manifest, priv, SignatureSource.Publisher);
        var corrupted = signed with
        {
            Signature = signed.Signature! with { PublicKey = "not base64 $$$" },
        };

        _signer.VerifyManifest(corrupted).Should().BeFalse();
    }

    // Item 18 — ToString of PackageSignature and the keypair tuple must not leak raw private-key bytes.
    [Fact]
    public void PackageSignature_ToString_does_not_leak_raw_public_key_bytes()
    {
        // The record-generated ToString exposes the base64-encoded public key, which is public data.
        // This test pins: no private-key-looking raw bytes land in the default ToString.
        var sig = new PackageSignature("Ed25519", "AAAA", "BBBB", SignatureSource.Publisher);

        var printed = sig.ToString();

        printed.Should().Contain("Ed25519");
        printed.Should().NotContain(new string('\0', 32), "no raw binary should leak through record ToString");
    }

    [Fact]
    public void GenerateKeyPair_tuple_ToString_does_not_leak_private_key_bytes()
    {
        var pair = _signer.GenerateKeyPair();

        var printed = pair.ToString();

        // ValueTuple<byte[], byte[]>.ToString() prints "(System.Byte[], System.Byte[])" — no raw bytes.
        printed.Should().NotContain(Convert.ToBase64String(pair.PrivateKey),
            "tuple ToString must not leak the private key material");
    }

    private static PackageManifest BuildManifest()
    {
        var book = new BookMetadata("sec-1e", "Security Fixture", 1, ["Author"], "Pub", 2026, null, "en-US", []);
        var fp = new ContentFingerprint("SHA-256", 0, 0, 0, 0, 0, 0, 0);
        return PackageManifest.CreateNew(book, fp);
    }
}
