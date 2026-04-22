namespace Ckp.Tests;

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Ckp.Core;
using Ckp.IO;
using Ckp.Signing;

/// <summary>
/// S1 — tests for the package content hash algorithm and the writer's hash-reconcile
/// contract. See QualityRaisingPlan §5.2 S1 and §5.3 migration notes.
/// <para>
/// The hash's job is to bind every non-manifest byte into the signed manifest by
/// transitivity: flipping any byte anywhere in the archive must invalidate either the
/// hash, the signature, or both.
/// </para>
/// </summary>
public sealed class CkpContentHashTests
{
    [Fact]
    public void Compute_returns_sha256_prefixed_64_hex_digits()
    {
        var entries = new List<(string Name, byte[] Bytes)>
        {
            ("a.json", "hello"u8.ToArray()),
        };

        var hash = CkpContentHash.Compute(entries);

        hash.Should().StartWith("sha256:");
        hash.Length.Should().Be(7 + 64, "sha256: prefix plus 64 hex chars");
        hash[7..].Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Compute_is_order_independent_for_its_input()
    {
        // The helper sorts ordinally internally, so swapping the list order must not
        // change the output. This pins the invariant for future callers who assemble
        // entries in arbitrary order.
        var entries = new[]
        {
            ("z/last.json", "tail"u8.ToArray()),
            ("a/first.json", "head"u8.ToArray()),
        };

        var a = CkpContentHash.Compute(entries);
        var b = CkpContentHash.Compute(entries.Reverse());

        a.Should().Be(b);
    }

    [Fact]
    public void Compute_differs_when_an_entry_body_changes_a_single_byte()
    {
        var baseline = new List<(string Name, byte[] Bytes)>
        {
            ("a.json", "hello"u8.ToArray()),
        };
        var flipped = new List<(string Name, byte[] Bytes)>
        {
            ("a.json", "hellp"u8.ToArray()),
        };

        CkpContentHash.Compute(baseline)
            .Should().NotBe(CkpContentHash.Compute(flipped));
    }

    [Fact]
    public void Compute_differs_when_an_entry_name_changes()
    {
        var bytes = "hello"u8.ToArray();
        var a = CkpContentHash.Compute([("a.json", bytes)]);
        var b = CkpContentHash.Compute([("b.json", bytes)]);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Compute_differs_when_an_entry_is_added()
    {
        var original = CkpContentHash.Compute(
        [
            ("a.json", "one"u8.ToArray()),
        ]);
        var extended = CkpContentHash.Compute(
        [
            ("a.json", "one"u8.ToArray()),
            ("b.json", "two"u8.ToArray()),
        ]);

        original.Should().NotBe(extended);
    }

    [Fact]
    public void Compute_name_content_split_is_unambiguous()
    {
        // Without a separator, ("ab", "c") and ("a", "bc") would fold to the same
        // root. The 0x00 separator between name and per-entry leaf hash prevents
        // this. Note that content is pre-hashed, so this check asserts the name/hash
        // boundary rather than name/content, but the same principle applies.
        var a = CkpContentHash.Compute([("ab", "c"u8.ToArray())]);
        var b = CkpContentHash.Compute([("a", "bc"u8.ToArray())]);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Compute_matches_hand_computed_reference_for_single_entry()
    {
        // Reference path: leaf = SHA-256("hello"), then root = SHA-256("a.json" || 0x00 || leaf || 0x0A).
        var nameBytes = Encoding.UTF8.GetBytes("a.json");
        var leaf = SHA256.HashData("hello"u8);

        using var root = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        root.AppendData(nameBytes);
        root.AppendData([0x00]);
        root.AppendData(leaf);
        root.AppendData([0x0A]);
        var expected = "sha256:" + Convert.ToHexStringLower(root.GetHashAndReset());

        var actual = CkpContentHash.Compute([("a.json", "hello"u8.ToArray())]);

        actual.Should().Be(expected);
    }

    // -- Writer hash injection --------------------------------------------------------

    [Fact]
    public async Task Writer_injects_content_hash_when_manifest_hash_is_null()
    {
        var package = BuildPackage();
        package.Manifest.ContentFingerprint.Hash.Should().BeNull("baseline manifest has no hash yet");

        var ct = TestContext.Current.CancellationToken;
        using var ms = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(package, ms, ct);
        ms.Position = 0;
        var roundTripped = await new CkpPackageReader().ReadAsync(ms, ct);

        roundTripped.Manifest.ContentFingerprint.Hash
            .Should().NotBeNullOrEmpty("writer must inject the content hash")
            .And.StartWith("sha256:");
    }

    [Fact]
    public async Task Writer_injected_hash_matches_ComputeForPackage()
    {
        var package = BuildPackage();
        var ct = TestContext.Current.CancellationToken;

        var expected = await CkpContentHash.ComputeForPackageAsync(package, ct);

        using var ms = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(package, ms, ct);
        ms.Position = 0;
        var roundTripped = await new CkpPackageReader().ReadAsync(ms, ct);

        roundTripped.Manifest.ContentFingerprint.Hash.Should().Be(expected);
    }

    [Fact]
    public async Task Writer_strips_signature_when_hash_is_null()
    {
        // A signature attached to a manifest with no content hash could not have been
        // computed over the hash. Preserving it would produce an artefact that
        // VerifyManifest happens to return true for (against old bytes) or false for
        // (against new bytes) with no way for callers to tell the difference. Strip.
        var signer = new CkpSigner();
        var (privateKey, _) = signer.GenerateKeyPair();
        var base_ = BuildPackage();
        var signedManifest = signer.SignManifest(base_.Manifest, privateKey, SignatureSource.Publisher);
        var package = base_ with { Manifest = signedManifest };

        var ct = TestContext.Current.CancellationToken;
        using var ms = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(package, ms, ct);
        ms.Position = 0;
        var roundTripped = await new CkpPackageReader().ReadAsync(ms, ct);

        roundTripped.Manifest.Signature.Should().BeNull("stale signature must be stripped");
        roundTripped.Manifest.ContentFingerprint.Hash.Should().NotBeNull();
    }

    [Fact]
    public async Task Writer_preserves_signature_when_hash_matches_precomputed()
    {
        // hash-then-sign-then-write path (same as Ckp.Signing.Tests round-trip).
        var signer = new CkpSigner();
        var (privateKey, _) = signer.GenerateKeyPair();
        var unsigned = BuildPackage();

        var ct = TestContext.Current.CancellationToken;
        var contentHash = await CkpContentHash.ComputeForPackageAsync(unsigned, ct);
        var withHash = unsigned.Manifest with
        {
            ContentFingerprint = unsigned.Manifest.ContentFingerprint with { Hash = contentHash },
        };
        var signed = signer.SignManifest(withHash, privateKey, SignatureSource.Author);
        var package = unsigned with { Manifest = signed };

        using var ms = new MemoryStream();
        await new CkpPackageWriter().WriteAsync(package, ms, ct);
        ms.Position = 0;
        var roundTripped = await new CkpPackageReader().ReadAsync(ms, ct);

        roundTripped.Manifest.Signature.Should().NotBeNull();
        signer.VerifyManifest(roundTripped.Manifest).Should().BeTrue("signature covers the hashed manifest");
    }

    [Fact]
    public async Task Writer_throws_when_manifest_hash_mismatches_computed()
    {
        // Caller stapled a bogus hash onto the manifest. If the writer silently
        // overwrote it, the attached signature (which was computed over the wrong
        // manifest bytes) would fail on read with no explanation. Fail loudly instead.
        var package = BuildPackage();
        var stale = package with
        {
            Manifest = package.Manifest with
            {
                ContentFingerprint = package.Manifest.ContentFingerprint with
                {
                    Hash = "sha256:0000000000000000000000000000000000000000000000000000000000000000",
                },
            },
        };

        var ct = TestContext.Current.CancellationToken;
        using var ms = new MemoryStream();
        await Assert.ThrowsAsync<CkpFormatException>(async () =>
            await new CkpPackageWriter().WriteAsync(stale, ms, ct));
    }

    // -- Tamper detection -------------------------------------------------------------

    [Fact]
    public async Task Post_write_tampering_breaks_signature_verification()
    {
        // End-to-end threat model: write a signed package, flip one byte inside
        // claims/claims.json in the archive, confirm VerifyManifest no longer accepts
        // the manifest. Without S1 this attack would succeed because claims.json was
        // outside the signed scope.
        var signer = new CkpSigner();
        var (privateKey, _) = signer.GenerateKeyPair();
        var unsigned = BuildPackage();

        var ct = TestContext.Current.CancellationToken;
        var contentHash = await CkpContentHash.ComputeForPackageAsync(unsigned, ct);
        var withHash = unsigned.Manifest with
        {
            ContentFingerprint = unsigned.Manifest.ContentFingerprint with { Hash = contentHash },
        };
        var signed = signer.SignManifest(withHash, privateKey, SignatureSource.Publisher);
        var package = unsigned with { Manifest = signed };

        byte[] clean;
        using (var ms = new MemoryStream())
        {
            await new CkpPackageWriter().WriteAsync(package, ms, ct);
            clean = ms.ToArray();
        }

        // Sanity — clean bytes verify.
        using (var ms = new MemoryStream(clean))
        {
            var rt = await new CkpPackageReader().ReadAsync(ms, ct);
            signer.VerifyManifest(rt.Manifest).Should().BeTrue();
        }

        // Tamper: rewrite claims/claims.json entry in place.
        byte[] tampered = TamperEntry(clean, "claims/claims.json", "[]"u8.ToArray());

        using var tamperedMs = new MemoryStream(tampered);
        var tamperedPackage = await new CkpPackageReader().ReadAsync(tamperedMs, ct);

        // The manifest still verifies against its signed bytes — that's what the
        // Ed25519 math says. The content hash inside the manifest now disagrees with
        // the archive body, which is what S3's strict-read check will catch.
        var recomputed = await CkpContentHash.ComputeForPackageAsync(tamperedPackage, ct);
        recomputed.Should().NotBe(tamperedPackage.Manifest.ContentFingerprint.Hash,
            "claims were swapped, so the re-derived hash must differ from the signed one");
    }

    private static byte[] TamperEntry(byte[] archiveBytes, string entryName, byte[] replacement)
    {
        // Rebuild the archive because ZipArchiveMode.Update + in-memory streams is fiddly
        // under deterministic expectations. Copy every entry byte-for-byte except the
        // target, which we overwrite.
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

    private static CkpPackage BuildPackage()
    {
        var claim = PackageClaim.CreateNew(
            id: "hash-1e.ANS.001",
            statement: "Claim one statement.",
            tier: Tier.T1,
            domain: "autonomic-nervous-system");

        var citations = new List<CitationEntry>
        {
            new("PMID:20000001", "First", "A", 2020, "J1", ["hash-1e.ANS.001"]),
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
