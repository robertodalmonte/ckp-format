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
}
