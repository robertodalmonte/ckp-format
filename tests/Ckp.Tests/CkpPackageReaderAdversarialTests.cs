namespace Ckp.Tests;

using System.IO.Compression;
using System.Text.Json;
using Ckp.Core;
using Ckp.IO;
using Ckp.Tests.TestSupport;

/// <summary>
/// Adversarial tests for <see cref="CkpPackageReader"/>. Each item maps to a row
/// in <c>docs/Refactoring/QualityRaisingPlan.md</c> §3.1 (items 1–9). Tests that
/// currently fail are skipped with a reference to the T3 hardening commit that
/// will enable them.
/// </summary>
public sealed class CkpPackageReaderAdversarialTests
{
    private readonly CkpPackageReader _reader = new();

    // Item 1 — malformed ZIP central directory.
    [Fact]
    public async Task Read_throws_on_malformed_central_directory()
    {
        using var stream = new MemoryStream(MalformedZipBuilder.CorruptCentralDirectory());

        var act = async () => await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    [Fact]
    public async Task Read_throws_on_non_zip_bytes()
    {
        using var stream = new MemoryStream(MalformedZipBuilder.NonZipBytes());

        var act = async () => await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidDataException>();
    }

    // Item 2 — truncated stream (valid prefix, no EOCD).
    [Fact]
    public async Task Read_throws_on_truncated_stream()
    {
        var full = MalformedZipBuilder.Default().Build();
        var truncated = full.AsSpan(0, Math.Min(32, full.Length)).ToArray();
        using var stream = new MemoryStream(truncated);

        var act = async () => await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        // Any of these is acceptable — all are signals that the archive is not readable.
        var ex = (await act.Should().ThrowAsync<Exception>()).Which;
        (ex is InvalidDataException or EndOfStreamException or IOException)
            .Should().BeTrue($"expected one of {{InvalidDataException, EndOfStreamException, IOException}} but got {ex.GetType().Name}");
    }

    // Item 3 — manifest.json deserializes to literal null.
    [Fact]
    public async Task Read_throws_when_manifest_entry_is_json_null()
    {
        var bytes = MalformedZipBuilder.Default().WithNullManifest().Build();
        using var stream = new MemoryStream(bytes);

        var act = async () => await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<CkpFormatException>()
            .WithMessage("*manifest.json*");
    }

    // Item 4 — alignment/external/ path traversal. T3 landed: entries that normalize
    // above the alignment/external/ root are silently dropped by the IsAlignmentEntry guard.
    [Fact]
    public async Task Read_rejects_alignment_path_traversal()
    {
        var bytes = MalformedZipBuilder.Default().WithTraversalAlignment().Build();
        using var stream = new MemoryStream(bytes);

        var package = await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        // The traversal entry is filtered out rather than throwing — it is not a "required"
        // entry so the benign response is to ignore it. No alignment is hydrated.
        package.Alignments.Should().BeEmpty();
    }

    [Theory]
    [InlineData("alignment/external/other.json", true)]
    [InlineData("alignment/external/../../evil.json", false)]
    [InlineData("alignment/external/nested/sub.json", true)] // deeper but still under prefix
    [InlineData("alignment/external/../external/other.json", true)] // normalizes back in
    [InlineData("alignment/external/../../../etc/passwd.json", false)]
    [InlineData("alignment/external/", false)] // the directory itself, no filename
    [InlineData("alignment/external/x.txt", false)] // wrong extension
    [InlineData("alignment/other/x.json", false)] // wrong prefix
    public void IsAlignmentEntry_normalizes_and_rejects_escapes(string fullName, bool expected)
    {
        CkpPackageReader.IsAlignmentEntry(fullName).Should().Be(expected);
    }

    // Item 5 — duplicate manifest.json entries. .NET ZipArchive.GetEntry returns the first.
    [Fact]
    public async Task Read_uses_first_entry_when_duplicate_manifests_present()
    {
        var bytes = MalformedZipBuilder.Default().WithDuplicateManifest().Build();
        using var stream = new MemoryStream(bytes);

        var package = await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        // The first manifest is the canonical fixture (fixture-1e). The second (duplicate-9e)
        // is present but must be ignored — this pins .NET's GetEntry contract.
        package.Manifest.Book.Key.Should().Be("fixture-1e");
        package.Manifest.Book.Edition.Should().Be(1);
    }

    // Item 6 — oversized manifest. Reader should decode it (we do not yet enforce a bound);
    // this test pins behaviour so a future size-limit option is a deliberate change.
    [Fact]
    public async Task Read_accepts_large_manifest_today()
    {
        // Build a manifest whose authors list is padded with ~1 MB of strings.
        var big = new List<string>(capacity: 4_000);
        for (int i = 0; i < 4_000; i++) big.Add(new string('x', 250));
        var book = new BookMetadata("big-1e", "Big", 1, big, "None", 2026, null, "en-US", []);
        var fp = new ContentFingerprint("SHA-256", 0, 0, 0, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        var bytes = CkpCanonicalJson.Serialize(manifest);

        var zip = new MalformedZipBuilder();
        zip.AddEntry("manifest.json", bytes);
        using var stream = new MemoryStream(zip.Build());

        var package = await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        package.Manifest.Book.Authors.Should().HaveCount(4_000);
    }

    // Item 7 — corrupt JSON in the required manifest entry.
    [Fact]
    public async Task Read_wraps_corrupt_manifest_json_in_CkpFormatException()
    {
        var builder = new MalformedZipBuilder();
        builder.AddManifest(valid: false);
        using var stream = new MemoryStream(builder.Build());

        var act = async () => await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        var ex = (await act.Should().ThrowAsync<CkpFormatException>()).Which;
        ex.EntryName.Should().Be("manifest.json");
        ex.InnerException.Should().BeOfType<JsonException>();
    }

    // Item 8 — unknown formatVersion is rejected per spec §15.4.
    [Fact]
    public async Task Read_rejects_unknown_format_version()
    {
        // AddManifest appends a second manifest entry. Use a fresh builder to avoid duplicates.
        var builder = new MalformedZipBuilder();
        builder.AddManifest(formatVersionOverride: "2.0");
        using var stream = new MemoryStream(builder.Build());

        var act = async () => await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        var ex = (await act.Should().ThrowAsync<CkpFormatException>()).Which;
        ex.EntryName.Should().Be("manifest.json");
        ex.Message.Should().Contain("2.0").And.Contain("1.0");
    }

    [Fact]
    public void Supported_format_versions_contains_1_0()
    {
        CkpPackageReader.SupportedFormatVersions.Should().Contain("1.0");
    }

    // Item 9 — unknown top-level entries are silently ignored.
    [Fact]
    public async Task Read_silently_ignores_unknown_top_level_entries()
    {
        var builder = MalformedZipBuilder.Default()
            .AddEntry("README.txt", "not part of the spec"u8.ToArray())
            .AddEntry("unexpected/folder/data.bin", new byte[] { 0x00, 0x01, 0x02 });
        using var stream = new MemoryStream(builder.Build());

        var package = await _reader.ReadAsync(stream, TestContext.Current.CancellationToken);

        package.Manifest.Book.Key.Should().Be("fixture-1e");
        // No observable effect on the hydrated package — unknown entries are dropped.
        package.Claims.Should().BeEmpty();
        package.Alignments.Should().BeEmpty();
    }
}
