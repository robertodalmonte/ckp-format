namespace Ckp.Tests;

using Ckp.Core;

public sealed class PackageManifestTests
{
    [Fact]
    public void CreateNew_generates_id_and_timestamp()
    {
        var book = CreateTestBookMetadata();
        var fingerprint = CreateTestFingerprint();

        var manifest = PackageManifest.CreateNew(book, fingerprint);

        manifest.PackageId.Should().NotBeEmpty();
        manifest.FormatVersion.Should().Be("1.0");
        manifest.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        manifest.Signature.Should().BeNull("new manifests are unsigned");
        manifest.Alignments.Should().BeEmpty();
    }

    [Fact]
    public void CreateNew_with_t0_registry_and_alignments()
    {
        var book = CreateTestBookMetadata();
        var fingerprint = CreateTestFingerprint();
        var t0 = new T0RegistryReference("2026.1", "https://t0-registry.org/v2026.1", 12);
        var alignments = new List<AlignmentSummary>
        {
            new("gamma-2e", null, 342, 89)
        };

        var manifest = PackageManifest.CreateNew(book, fingerprint, t0, alignments);

        manifest.T0Registry.Should().NotBeNull();
        manifest.T0Registry!.Version.Should().Be("2026.1");
        manifest.Alignments.Should().HaveCount(1);
        manifest.Alignments[0].TargetBook.Should().Be("gamma-2e");
    }

    [Fact]
    public void Restore_preserves_all_fields()
    {
        var id = Guid.CreateVersion7();
        var created = new DateTimeOffset(2026, 4, 8, 14, 30, 0, TimeSpan.Zero);
        var sig = new PackageSignature("Ed25519", "pubkey==", "sig==", SignatureSource.Publisher);
        var book = CreateTestBookMetadata();
        var fp = CreateTestFingerprint();

        var manifest = PackageManifest.Restore(
            formatVersion: "1.0",
            packageId: id,
            createdAt: created,
            signature: sig,
            book: book,
            contentFingerprint: fp,
            t0Registry: null,
            alignments: []);

        manifest.PackageId.Should().Be(id);
        manifest.CreatedAt.Should().Be(created);
        manifest.Signature.Should().NotBeNull();
        manifest.Signature!.Algorithm.Should().Be("Ed25519");
        manifest.Signature.Source.Should().Be(SignatureSource.Publisher);
    }

    [Fact]
    public void ContentFingerprint_tier_counts_are_preserved()
    {
        var fp = new ContentFingerprint("SHA-256", 100, 5, 40, 30, 20, 10, 500);

        fp.ClaimCount.Should().Be(100);
        fp.T1Count.Should().Be(40);
        fp.T2Count.Should().Be(30);
        fp.T3Count.Should().Be(20);
        fp.T4Count.Should().Be(10);
        fp.CitationCount.Should().Be(500);
    }

    private static BookMetadata CreateTestBookMetadata() => new(
        Key: "test-1e",
        Title: "Test Textbook",
        Edition: 1,
        Authors: ["Test Author"],
        Publisher: "Test Publisher",
        Year: 2026,
        Isbn: "978-0000000000",
        Language: "en-US",
        Domains: ["physics", "biology"]);

    private static ContentFingerprint CreateTestFingerprint() => new(
        Algorithm: "SHA-256",
        ClaimCount: 50,
        DomainCount: 3,
        T1Count: 20,
        T2Count: 15,
        T3Count: 10,
        T4Count: 5,
        CitationCount: 200);
}
