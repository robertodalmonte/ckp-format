namespace Ckp.Tests;

using System.IO.Compression;
using Ckp.Core;
using Ckp.IO;
using Ckp.Signing;

/// <summary>
/// T8 — extended Ckp.Signing coverage beyond happy-path and negative-path tests.
/// Pins three invariants the plan flags as under-covered:
/// <list type="bullet">
///   <item>Key derivation determinism — the same private key always yields the same public key.</item>
///   <item>Mixed-source round-trips — every <see cref="SignatureSource"/> survives write → read → verify.</item>
///   <item>Content tamper resistance — a single-byte flip inside a signed, sealed package is
///         detected on read, both by the content-hash recompute and by the strict-read verifier.</item>
/// </list>
/// </summary>
public sealed class CkpSigningExtendedCoverageTests
{
    private readonly CkpSigner _signer = new();
    private readonly CkpPackageReader _reader = new();
    private readonly CkpPackageWriter _writer = new();

    // -- Key derivation determinism ---------------------------------------------------

    [Fact]
    public void Same_private_key_yields_same_public_key_across_signs()
    {
        // Ed25519 derives the public key deterministically from the private scalar. Sign
        // a payload twice with the same private key — the embedded public key must match.
        var (privateKey, publicKey) = _signer.GenerateKeyPair();
        byte[] data = "signed payload"u8.ToArray();

        var sigA = _signer.Sign(data, privateKey, SignatureSource.Publisher);
        var sigB = _signer.Sign(data, privateKey, SignatureSource.Author);

        sigA.PublicKey.Should().Be(sigB.PublicKey);
        Convert.FromBase64String(sigA.PublicKey).Should().BeEquivalentTo(publicKey);
    }

    [Fact]
    public void Signatures_over_same_payload_with_same_key_are_bytewise_identical()
    {
        // Ed25519 is deterministic (RFC 8032 §5.1.6): no RNG in Sign.
        // Two signatures over the same bytes with the same key must match to the byte.
        var (privateKey, _) = _signer.GenerateKeyPair();
        byte[] data = "deterministic-sig-probe"u8.ToArray();

        var sigA = _signer.Sign(data, privateKey, SignatureSource.Publisher);
        var sigB = _signer.Sign(data, privateKey, SignatureSource.Publisher);

        sigA.Signature.Should().Be(sigB.Signature);
    }

    // -- Mixed-source round-trips -----------------------------------------------------

    [Theory]
    [InlineData(SignatureSource.Publisher)]
    [InlineData(SignatureSource.Author)]
    [InlineData(SignatureSource.Scholar)]
    [InlineData(SignatureSource.Community)]
    public async Task Every_source_round_trips_write_read_verify(SignatureSource source)
    {
        // Seal a package under every trust tier. The on-disk representation of the source
        // must survive JSON round-trip and the signature must still verify afterwards.
        var (privateKey, _) = _signer.GenerateKeyPair();
        var bytes = await WriteSignedPackageWith(privateKey, source);

        using var ms = new MemoryStream(bytes);
        var ct = TestContext.Current.CancellationToken;
        var pkg = await _reader.ReadAsync(ms, ct);

        pkg.Manifest.Signature.Should().NotBeNull();
        pkg.Manifest.Signature!.Source.Should().Be(source);
        _signer.VerifyManifest(pkg.Manifest).Should().BeTrue();
    }

    // -- Post-write tamper detection (validator-initiated) ----------------------------

    [Fact]
    public async Task Single_byte_flip_in_claims_json_is_detected_by_content_hash_recompute()
    {
        // Spec §10.2 + S1: flipping one byte in claims/claims.json after signing must
        // cause the re-derived content hash to differ from the one bound into the
        // signed manifest. The signature math itself still verifies (the manifest bytes
        // are untouched), which is exactly why the content hash is load-bearing.
        var (privateKey, _) = _signer.GenerateKeyPair();
        var bytes = await WriteSignedPackageWith(privateKey, SignatureSource.Publisher);
        var tampered = TamperEntryAtOffset(bytes, "claims/claims.json", offset: 3);

        var ct = TestContext.Current.CancellationToken;
        using var ms = new MemoryStream(tampered);
        var pkg = await _reader.ReadAsync(ms, ct);

        _signer.VerifyManifest(pkg.Manifest).Should().BeTrue(
            "manifest bytes are unchanged, so the Ed25519 signature still matches them");

        var recomputed = await CkpContentHash.ComputeForPackageAsync(pkg, ct);
        recomputed.Should().NotBe(pkg.Manifest.ContentFingerprint.Hash,
            "but the manifest's bound content hash no longer matches the archive body");
    }

    [Fact]
    public async Task Single_byte_flip_in_claims_json_is_caught_by_strict_read()
    {
        // Same attack, seen by the S3 strict reader: surface as CkpFormatException with
        // a message naming the content-hash mismatch.
        var (privateKey, _) = _signer.GenerateKeyPair();
        var bytes = await WriteSignedPackageWith(privateKey, SignatureSource.Publisher);
        var tampered = TamperEntryAtOffset(bytes, "claims/claims.json", offset: 3);

        var options = new CkpReadOptions
        {
            RequireContentHash = true,
            VerifySignature = true,
            SignatureVerifier = _signer.VerifyManifest,
        };
        var ct = TestContext.Current.CancellationToken;
        using var ms = new MemoryStream(tampered);

        var ex = await Assert.ThrowsAsync<CkpFormatException>(() =>
            _reader.ReadAsync(ms, options, ct));
        ex.Message.Should().Contain("content-hash mismatch");
    }

    // -- Helpers ----------------------------------------------------------------------

    private async Task<byte[]> WriteSignedPackageWith(byte[] privateKey, SignatureSource source)
    {
        var ct = TestContext.Current.CancellationToken;
        var package = BuildPackage();
        var hash = await CkpContentHash.ComputeForPackageAsync(package, ct);
        var withHash = package.Manifest with
        {
            ContentFingerprint = package.Manifest.ContentFingerprint with { Hash = hash },
        };
        var signed = _signer.SignManifest(withHash, privateKey, source);
        var toWrite = package with { Manifest = signed };

        using var ms = new MemoryStream();
        await _writer.WriteAsync(toWrite, ms, ct);
        return ms.ToArray();
    }

    private static byte[] TamperEntryAtOffset(byte[] archiveBytes, string entryName, int offset)
    {
        // Rebuild the archive flipping a single byte at `offset` inside the named entry.
        // Surgical — changes only one bit, but enough to make the SHA-256 leaf for the
        // entry different, which propagates to the root hash.
        using var output = new MemoryStream();
        using (var sourceMs = new MemoryStream(archiveBytes))
        using (var source = new ZipArchive(sourceMs, ZipArchiveMode.Read))
        using (var dest = new ZipArchive(output, ZipArchiveMode.Create))
        {
            foreach (var entry in source.Entries)
            {
                var newEntry = dest.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                newEntry.LastWriteTime = CkpPackageWriter.DeterministicEpoch;
                using var writeStream = newEntry.Open();
                if (entry.FullName == entryName)
                {
                    using var readStream = entry.Open();
                    using var buf = new MemoryStream();
                    readStream.CopyTo(buf);
                    var bytes = buf.ToArray();
                    if (offset < bytes.Length)
                    {
                        bytes[offset] ^= 0x01;
                    }
                    writeStream.Write(bytes);
                }
                else
                {
                    using var readStream = entry.Open();
                    readStream.CopyTo(writeStream);
                }
            }
        }
        return output.ToArray();
    }

    private static CkpPackage BuildPackage()
    {
        var claim = PackageClaim.CreateNew(
            id: "t8-1e.ANS.001",
            statement: "T8 coverage claim.",
            tier: Tier.T1,
            domain: "autonomic-nervous-system");
        var citations = new List<CitationEntry>
        {
            new("PMID:80000001", "T8 Source", "A", 2026, "J", ["t8-1e.ANS.001"]),
        };
        var domains = new List<DomainInfo> { new("autonomic-nervous-system", 1, 1, 0, 0, 0) };
        var chapters = new List<ChapterInfo> { new(1, "Intro", 1, ["autonomic-nervous-system"]) };
        var editions = new List<EditionInfo> { new(1, 2026, null, null, null) };

        var book = new BookMetadata("t8-1e", "T8 Book", 1, ["Author"], "Pub", 2026, null, "en-US",
            ["autonomic-nervous-system"]);
        var fp = new ContentFingerprint("SHA-256", 1, 1, 1, 0, 0, 0, 1);
        var manifest = PackageManifest.CreateNew(book, fp);

        return new CkpPackage
        {
            Manifest = manifest,
            Claims = [claim],
            Citations = citations,
            Domains = domains,
            Chapters = chapters,
            Editions = editions,
        };
    }
}
