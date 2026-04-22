namespace Ckp.Tests;

using System.IO.Compression;
using Ckp.Core;
using Ckp.IO;
using Ckp.Signing;

/// <summary>
/// S3 — strict-read mode. Every test exercises one threat from
/// <c>docs/Refactoring/SigningThreatModel.md</c> and asserts the strict reader rejects
/// it via <see cref="CkpFormatException"/>. The same input under
/// <see cref="CkpReadOptions.Default"/> must still succeed, so the strictness is
/// strictly opt-in (pre-S3 callers keep working).
/// </summary>
public sealed class CkpReadOptionsStrictModeTests
{
    private readonly CkpSigner _signer = new();
    private readonly CkpPackageReader _reader = new();
    private readonly CkpPackageWriter _writer = new();

    [Fact]
    public async Task Default_options_accept_unsigned_package_with_hash()
    {
        // Regression safety: adding S3 must not change the behaviour of
        // any existing caller who passes no options.
        var bytes = await WriteUnsignedPackage(BuildPackage());
        using var ms = new MemoryStream(bytes);

        var ct = TestContext.Current.CancellationToken;
        var pkg = await _reader.ReadAsync(ms, ct);

        pkg.Manifest.Signature.Should().BeNull();
        pkg.Manifest.ContentFingerprint.Hash.Should().NotBeNull();
    }

    [Fact]
    public async Task RequireSignature_rejects_unsigned_package()
    {
        // T-DOWNGRADE-UNSIGNED: attacker strips the signature block.
        var bytes = await WriteUnsignedPackage(BuildPackage());
        using var ms = new MemoryStream(bytes);

        var options = new CkpReadOptions { RequireSignature = true };
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<CkpFormatException>(() =>
            _reader.ReadAsync(ms, options, ct));
        ex.EntryName.Should().Be("manifest.json");
        ex.Message.Should().Contain("signature");
    }

    [Fact]
    public async Task RequireSignature_accepts_signed_package()
    {
        var bytes = await WriteSignedPackage();
        using var ms = new MemoryStream(bytes);

        var options = new CkpReadOptions { RequireSignature = true };
        var ct = TestContext.Current.CancellationToken;

        var pkg = await _reader.ReadAsync(ms, options, ct);
        pkg.Manifest.Signature.Should().NotBeNull();
    }

    [Fact]
    public async Task RequireContentHash_accepts_untampered_package()
    {
        var bytes = await WriteUnsignedPackage(BuildPackage());
        using var ms = new MemoryStream(bytes);

        var options = new CkpReadOptions { RequireContentHash = true };
        var ct = TestContext.Current.CancellationToken;

        var pkg = await _reader.ReadAsync(ms, options, ct);
        pkg.Claims.Should().HaveCount(1);
    }

    [Fact]
    public async Task RequireContentHash_rejects_post_write_tampering()
    {
        // T-BYTE: flip one byte inside claims/claims.json after signing+writing.
        var bytes = await WriteSignedPackage();
        var tampered = TamperEntry(bytes, "claims/claims.json", "[]"u8.ToArray());
        using var ms = new MemoryStream(tampered);

        var options = new CkpReadOptions { RequireContentHash = true };
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<CkpFormatException>(() =>
            _reader.ReadAsync(ms, options, ct));
        ex.Message.Should().Contain("content-hash mismatch");
    }

    [Fact]
    public async Task RequireContentHash_rejects_package_without_hash()
    {
        // Simulate a pre-S1 package by hand-building an archive whose manifest has a
        // null ContentFingerprint.Hash. The writer cannot produce this directly
        // (S1 always injects) so we craft the bytes.
        var bytes = await WriteSignedPackage();
        var legacy = RewriteManifest(bytes, m => m with
        {
            ContentFingerprint = m.ContentFingerprint with { Hash = null },
        });
        using var ms = new MemoryStream(legacy);

        var options = new CkpReadOptions { RequireContentHash = true };
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<CkpFormatException>(() =>
            _reader.ReadAsync(ms, options, ct));
        ex.Message.Should().Contain("pre-S1");
    }

    [Fact]
    public async Task ExpectedPublicKey_rejects_forged_key()
    {
        // T-FORGE-KEY: signature is valid (from attacker's own key) but the expected
        // publisher key doesn't match. The strict reader catches this even though the
        // math verifies.
        var bytes = await WriteSignedPackage();
        using var ms = new MemoryStream(bytes);

        var options = new CkpReadOptions
        {
            ExpectedPublicKey = "Z2Fycmlzb24tanVzdC1hLXRlc3Qta2V5LW5vdC1yZWFs", // arbitrary wrong key
        };
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<CkpFormatException>(() =>
            _reader.ReadAsync(ms, options, ct));
        ex.Message.Should().Contain("pinned a public key");
    }

    [Fact]
    public async Task ExpectedPublicKey_accepts_matching_key()
    {
        var (privateKey, publicKey) = _signer.GenerateKeyPair();
        var bytes = await WriteSignedPackageWith(privateKey);
        using var ms = new MemoryStream(bytes);

        var options = new CkpReadOptions
        {
            ExpectedPublicKey = Convert.ToBase64String(publicKey),
        };
        var ct = TestContext.Current.CancellationToken;

        var pkg = await _reader.ReadAsync(ms, options, ct);
        pkg.Manifest.Signature!.PublicKey.Should().Be(options.ExpectedPublicKey);
    }

    [Fact]
    public async Task VerifySignature_uses_caller_supplied_verifier()
    {
        // Wire the signer in via the delegate so the reader never depends on Ckp.Signing.
        var bytes = await WriteSignedPackage();
        using var ms = new MemoryStream(bytes);

        var options = new CkpReadOptions
        {
            VerifySignature = true,
            SignatureVerifier = _signer.VerifyManifest,
        };
        var ct = TestContext.Current.CancellationToken;

        var pkg = await _reader.ReadAsync(ms, options, ct);
        pkg.Manifest.Signature.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifySignature_rejects_invalid_signature()
    {
        // T-BYTE at manifest scope: flip a byte inside manifest.json so canonical JSON
        // changes but the signature doesn't. Wire the real verifier; reader should fail.
        var bytes = await WriteSignedPackage();
        var tampered = TamperManifestBook(bytes, "hash-1e", "xxxx-1e");
        using var ms = new MemoryStream(tampered);

        var options = new CkpReadOptions
        {
            VerifySignature = true,
            SignatureVerifier = _signer.VerifyManifest,
        };
        var ct = TestContext.Current.CancellationToken;

        var ex = await Assert.ThrowsAsync<CkpFormatException>(() =>
            _reader.ReadAsync(ms, options, ct));
        ex.Message.Should().Contain("signature verification failed");
    }

    [Fact]
    public async Task VerifySignature_without_verifier_throws_misconfiguration()
    {
        // Caller forgot to wire the delegate. The reader must fail loudly at the
        // options-validation step rather than silently skipping the check.
        var bytes = await WriteSignedPackage();
        using var ms = new MemoryStream(bytes);

        var options = new CkpReadOptions { VerifySignature = true };
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _reader.ReadAsync(ms, options, ct));
    }

    [Fact]
    public async Task Combined_strict_checks_pass_for_well_formed_signed_package()
    {
        var (privateKey, publicKey) = _signer.GenerateKeyPair();
        var bytes = await WriteSignedPackageWith(privateKey);
        using var ms = new MemoryStream(bytes);

        var options = new CkpReadOptions
        {
            RequireSignature = true,
            RequireContentHash = true,
            ExpectedPublicKey = Convert.ToBase64String(publicKey),
            VerifySignature = true,
            SignatureVerifier = _signer.VerifyManifest,
        };
        var ct = TestContext.Current.CancellationToken;

        // Smoke — all four strict checks applied simultaneously on the clean package.
        var pkg = await _reader.ReadAsync(ms, options, ct);
        pkg.Manifest.Signature.Should().NotBeNull();
        pkg.Manifest.ContentFingerprint.Hash.Should().NotBeNull();
    }

    // -- helpers ---------------------------------------------------------------------

    private async Task<byte[]> WriteUnsignedPackage(CkpPackage package)
    {
        var ct = TestContext.Current.CancellationToken;
        using var ms = new MemoryStream();
        await _writer.WriteAsync(package, ms, ct);
        return ms.ToArray();
    }

    private async Task<byte[]> WriteSignedPackage()
    {
        var (privateKey, _) = _signer.GenerateKeyPair();
        return await WriteSignedPackageWith(privateKey);
    }

    private async Task<byte[]> WriteSignedPackageWith(byte[] privateKey)
    {
        var ct = TestContext.Current.CancellationToken;
        var package = BuildPackage();
        var hash = await CkpContentHash.ComputeForPackageAsync(package, ct);
        var withHash = package.Manifest with
        {
            ContentFingerprint = package.Manifest.ContentFingerprint with { Hash = hash },
        };
        var signed = _signer.SignManifest(withHash, privateKey, SignatureSource.Publisher);
        var toWrite = package with { Manifest = signed };

        using var ms = new MemoryStream();
        await _writer.WriteAsync(toWrite, ms, ct);
        return ms.ToArray();
    }

    private static byte[] TamperEntry(byte[] archiveBytes, string entryName, byte[] replacement)
    {
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
                    writeStream.Write(replacement);
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

    private static byte[] TamperManifestBook(byte[] archiveBytes, string oldKey, string newKey)
    {
        // Flip the book key inside manifest.json. Because canonical JSON is compact and
        // the key appears once, a simple byte replace is fine.
        return RewriteEntryBytes(archiveBytes, "manifest.json", original =>
        {
            var s = System.Text.Encoding.UTF8.GetString(original);
            return System.Text.Encoding.UTF8.GetBytes(s.Replace(oldKey, newKey));
        });
    }

    private static byte[] RewriteManifest(byte[] archiveBytes, Func<PackageManifest, PackageManifest> rewrite)
    {
        return RewriteEntryBytes(archiveBytes, "manifest.json", original =>
        {
            var manifest = System.Text.Json.JsonSerializer.Deserialize<PackageManifest>(
                original, CkpJsonOptions.Instance)!;
            var updated = rewrite(manifest);
            return CkpCanonicalJson.Serialize(updated);
        });
    }

    private static byte[] RewriteEntryBytes(byte[] archiveBytes, string entryName, Func<byte[], byte[]> rewrite)
    {
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
                    var rewritten = rewrite(buf.ToArray());
                    writeStream.Write(rewritten);
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
            id: "hash-1e.ANS.001",
            statement: "Claim one statement.",
            tier: Tier.T1,
            domain: "autonomic-nervous-system");
        var citations = new List<CitationEntry>
        {
            new("PMID:30000001", "First", "A", 2020, "J1", ["hash-1e.ANS.001"]),
        };
        var domains = new List<DomainInfo> { new("autonomic-nervous-system", 1, 1, 0, 0, 0) };
        var chapters = new List<ChapterInfo> { new(1, "Intro", 1, ["autonomic-nervous-system"]) };
        var editions = new List<EditionInfo> { new(1, 2026, null, null, null) };

        var book = new BookMetadata("hash-1e", "Hash Book", 1, ["Author"], "Pub", 2026, null, "en-US",
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
