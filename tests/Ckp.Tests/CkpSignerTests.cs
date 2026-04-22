namespace Ckp.Tests;

using System.Text;
using Ckp.Core;
using Ckp.IO;
using Ckp.Signing;

public sealed class CkpSignerTests
{
    private readonly CkpSigner _signer = new();

    [Fact]
    public void GenerateKeyPair_produces_valid_key_lengths()
    {
        var (privateKey, publicKey) = _signer.GenerateKeyPair();

        privateKey.Should().HaveCount(32, "Ed25519 private key is 32 bytes");
        publicKey.Should().HaveCount(32, "Ed25519 public key is 32 bytes");
    }

    [Fact]
    public void Sign_produces_valid_signature_block()
    {
        var (privateKey, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("""{"test":"manifest"}""");

        var signature = _signer.Sign(data, privateKey, SignatureSource.Publisher);

        signature.Algorithm.Should().Be("Ed25519");
        signature.Source.Should().Be(SignatureSource.Publisher);
        signature.PublicKey.Should().NotBeNullOrEmpty();
        signature.Signature.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Verify_accepts_valid_signature()
    {
        var (privateKey, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("""{"book":"delta-14e","claims":3847}""");

        var signature = _signer.Sign(data, privateKey, SignatureSource.Author);

        bool valid = _signer.Verify(data, signature);

        valid.Should().BeTrue();
    }

    [Fact]
    public void Verify_rejects_tampered_data()
    {
        var (privateKey, _) = _signer.GenerateKeyPair();
        byte[] original = Encoding.UTF8.GetBytes("""{"claims":100}""");
        byte[] tampered = Encoding.UTF8.GetBytes("""{"claims":999}""");

        var signature = _signer.Sign(original, privateKey, SignatureSource.Scholar);

        bool valid = _signer.Verify(tampered, signature);

        valid.Should().BeFalse();
    }

    [Fact]
    public void Verify_rejects_wrong_key()
    {
        var (privateKey1, _) = _signer.GenerateKeyPair();
        var (privateKey2, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("""{"test":"data"}""");

        var sig1 = _signer.Sign(data, privateKey1, SignatureSource.Community);
        // Replace the public key with key2's public key
        var sig2 = _signer.Sign(data, privateKey2, SignatureSource.Community);

        var forgedSig = sig1 with { PublicKey = sig2.PublicKey };

        bool valid = _signer.Verify(data, forgedSig);

        valid.Should().BeFalse();
    }

    [Theory]
    [InlineData(SignatureSource.Publisher)]
    [InlineData(SignatureSource.Author)]
    [InlineData(SignatureSource.Scholar)]
    [InlineData(SignatureSource.Community)]
    public void Sign_preserves_signature_source(SignatureSource source)
    {
        var (privateKey, _) = _signer.GenerateKeyPair();
        byte[] data = Encoding.UTF8.GetBytes("test");

        var signature = _signer.Sign(data, privateKey, source);

        signature.Source.Should().Be(source);
    }

    [Fact]
    public void Multiple_key_pairs_are_distinct()
    {
        var (pk1, pub1) = _signer.GenerateKeyPair();
        var (pk2, pub2) = _signer.GenerateKeyPair();

        pk1.Should().NotBeEquivalentTo(pk2);
        pub1.Should().NotBeEquivalentTo(pub2);
    }

    // ── Manifest-level signing (P1.2) ───────────────────────────────────

    [Fact]
    public void SignManifest_then_VerifyManifest_round_trips()
    {
        var (privateKey, _) = _signer.GenerateKeyPair();
        var manifest = CreateManifest();

        var signed = _signer.SignManifest(manifest, privateKey, SignatureSource.Publisher);

        signed.Signature.Should().NotBeNull();
        _signer.VerifyManifest(signed).Should().BeTrue();
    }

    [Fact]
    public void VerifyManifest_rejects_tampered_book_title()
    {
        var (privateKey, _) = _signer.GenerateKeyPair();
        var manifest = CreateManifest();

        var signed = _signer.SignManifest(manifest, privateKey, SignatureSource.Author);
        var tampered = signed with { Book = signed.Book with { Title = "Hijacked Textbook" } };

        _signer.VerifyManifest(tampered).Should().BeFalse();
    }

    [Fact]
    public void VerifyManifest_returns_false_for_unsigned_manifest()
    {
        var manifest = CreateManifest();

        _signer.VerifyManifest(manifest).Should().BeFalse();
    }

    [Fact]
    public void SignManifest_replaces_any_prior_signature()
    {
        var (pk1, _) = _signer.GenerateKeyPair();
        var (pk2, _) = _signer.GenerateKeyPair();
        var manifest = CreateManifest();

        var first = _signer.SignManifest(manifest, pk1, SignatureSource.Community);
        var second = _signer.SignManifest(first, pk2, SignatureSource.Publisher);

        second.Signature!.PublicKey.Should().NotBe(first.Signature!.PublicKey);
        _signer.VerifyManifest(second).Should().BeTrue();
    }

    [Fact]
    public async Task Signed_manifest_verifies_after_write_then_read()
    {
        // S1 contract: the signature must be computed over the manifest *after* the content
        // hash has been injected, otherwise the signed bytes will not match what the writer
        // emits. Callers follow the hash-then-sign-then-write flow below.
        var (privateKey, _) = _signer.GenerateKeyPair();
        var writer = new CkpPackageWriter();
        var reader = new CkpPackageReader();

        var ct = TestContext.Current.CancellationToken;
        var unsignedPackage = new CkpPackage { Manifest = CreateManifest() };
        var contentHash = await CkpContentHash.ComputeForPackageAsync(unsignedPackage, ct);

        var hashedManifest = unsignedPackage.Manifest with
        {
            ContentFingerprint = unsignedPackage.Manifest.ContentFingerprint with { Hash = contentHash },
        };
        var signedManifest = _signer.SignManifest(hashedManifest, privateKey, SignatureSource.Author);
        var package = unsignedPackage with { Manifest = signedManifest };

        using var ms = new MemoryStream();
        await writer.WriteAsync(package, ms, ct);
        ms.Position = 0;
        var roundTripped = await reader.ReadAsync(ms, ct);

        _signer.VerifyManifest(roundTripped.Manifest).Should().BeTrue();
        roundTripped.Manifest.ContentFingerprint.Hash.Should().Be(contentHash);
    }

    [Fact]
    public void CanonicalJson_is_deterministic_for_equal_manifests()
    {
        var m1 = CreateManifest();
        var m2 = CreateManifest() with
        {
            PackageId = m1.PackageId,
            CreatedAt = m1.CreatedAt
        };

        byte[] a = CkpCanonicalJson.Serialize(m1);
        byte[] b = CkpCanonicalJson.Serialize(m2);

        a.Should().BeEquivalentTo(b);
    }

    [Fact]
    public void CanonicalJson_sorts_keys_lexicographically()
    {
        var manifest = CreateManifest();

        byte[] bytes = CkpCanonicalJson.Serialize(manifest);
        string json = System.Text.Encoding.UTF8.GetString(bytes);

        // Root keys must appear in strict ordinal order.
        int alignmentsIdx = json.IndexOf("\"alignments\"");
        int bookIdx = json.IndexOf("\"book\"");
        int contentIdx = json.IndexOf("\"contentFingerprint\"");
        int createdIdx = json.IndexOf("\"createdAt\"");
        int formatIdx = json.IndexOf("\"formatVersion\"");
        int packageIdx = json.IndexOf("\"packageId\"");

        alignmentsIdx.Should().BeLessThan(bookIdx);
        bookIdx.Should().BeLessThan(contentIdx);
        contentIdx.Should().BeLessThan(createdIdx);
        createdIdx.Should().BeLessThan(formatIdx);
        formatIdx.Should().BeLessThan(packageIdx);
    }

    private static PackageManifest CreateManifest()
    {
        var book = new BookMetadata(
            "alpha-3e", "Alpha Orthodontics", 3, ["Alice Alpha"],
            "Acme Press", 2019, "978-0000000000", "en-US", ["biomechanics"]);
        var fp = new ContentFingerprint("SHA-256", 10, 3, 4, 2, 1, 0, 5);
        return PackageManifest.CreateNew(
            book, fp,
            alignments: [new AlignmentSummary("beta-2e", null, 4, 2)]);
    }
}
