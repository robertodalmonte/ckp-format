namespace Ckp.Tests;

using System.IO.Compression;
using Ckp.Core;
using Ckp.Epub;
using Ckp.IO;

/// <summary>
/// End-to-end tests that drive <see cref="EpubTranspiler"/> and
/// <c>EpubTranspilerExtensions.TranspileAndWriteAsync</c> against real (but
/// programmatically constructed) .epub fixtures. Complements
/// <c>EpubTranspilerTests</c>, which only exercises the shape of the resulting
/// <c>CkpPackage</c>.
/// </summary>
public sealed class EpubTranspilerEndToEndTests : IDisposable
{
    private readonly string _tempDir;

    public EpubTranspilerEndToEndTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ckp-epub-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best-effort cleanup */ }
    }

    private static readonly BookMetadataArgs SampleMetadata = new(
        Key: "test-book-1e",
        Title: "Test Book",
        Edition: 1,
        Authors: ["Test Author"],
        Publisher: "Test Publisher",
        Year: 2026);

    [Fact]
    public async Task TranspileAsync_extracts_flat_chapter_list()
    {
        string epubPath = Path.Combine(_tempDir, "flat.epub");
        EpubFixtureBuilder.WriteFlatEpub(epubPath,
        [
            new("Introduction", "<p>Hello world.</p>"),
            new("Methods", "<p>Research methods.</p>"),
            new("Results", "<p>Findings here.</p>")
        ]);

        var transpiler = new EpubTranspiler(epubPath, SampleMetadata);
        var package = await transpiler.TranspileAsync(TestContext.Current.CancellationToken);

        package.Claims.Should().BeEmpty();
        package.Chapters.Should().HaveCount(3);
        package.Chapters[0].Title.Should().Be("Introduction");
        package.Chapters[1].Title.Should().Be("Methods");
        package.Chapters[2].Title.Should().Be("Results");
        package.Manifest.Book.Key.Should().Be("test-book-1e");
        package.Manifest.Book.Authors.Should().ContainSingle().Which.Should().Be("Test Author");

        transpiler.Chapters.Should().HaveCount(3);
        transpiler.Chapters[0].Text.Should().Contain("Hello world");
    }

    [Fact]
    public async Task TranspileAsync_walks_nested_nav_recursively()
    {
        string epubPath = Path.Combine(_tempDir, "nested.epub");
        EpubFixtureBuilder.WriteNestedEpub(epubPath,
        [
            new("Ch 1", "<p>Alpha.</p>"),
            new("Ch 2", "<p>Beta.</p>"),
            new("Ch 3", "<p>Gamma.</p>"),
            new("Ch 4", "<p>Delta.</p>")
        ]);

        var transpiler = new EpubTranspiler(epubPath, SampleMetadata);
        var package = await transpiler.TranspileAsync(TestContext.Current.CancellationToken);

        // All four chapter leaves should be collected despite the two-level nav.
        package.Chapters.Should().HaveCount(4);
        package.Chapters.Select(c => c.Title)
            .Should().ContainInOrder("Ch 1", "Ch 2", "Ch 3", "Ch 4");
    }

    [Fact]
    public async Task TranspileAsync_skips_empty_chapters()
    {
        // A chapter with empty title and empty body produces only whitespace
        // after StripHtml — the extractor's IsNullOrWhiteSpace guard drops it.
        string epubPath = Path.Combine(_tempDir, "has-empty.epub");
        EpubFixtureBuilder.WriteFlatEpub(epubPath,
        [
            new("Real", "<p>Content.</p>"),
            new("", ""),
            new("Also Real", "<p>More content.</p>")
        ]);

        var transpiler = new EpubTranspiler(epubPath, SampleMetadata);
        var package = await transpiler.TranspileAsync(TestContext.Current.CancellationToken);

        // The empty-body chapter is dropped by the StripHtml whitespace check.
        package.Chapters.Should().HaveCount(2);
        package.Chapters.Select(c => c.Title).Should().Equal("Real", "Also Real");
    }

    [Fact]
    public async Task TranspileAndWriteAsync_writes_package_and_chapter_text_files()
    {
        string epubPath = Path.Combine(_tempDir, "writer.epub");
        string ckpPath = Path.Combine(_tempDir, "writer.ckp");
        EpubFixtureBuilder.WriteFlatEpub(epubPath,
        [
            new("Chapter One", "<p>Body of one.</p>"),
            new("Chapter Two", "<p>Body of two.</p>")
        ]);

        var transpiler = new EpubTranspiler(epubPath, SampleMetadata);
        var package = await transpiler.TranspileAndWriteAsync(ckpPath, TestContext.Current.CancellationToken);

        package.Chapters.Should().HaveCount(2);
        File.Exists(ckpPath).Should().BeTrue();

        // Open the archive and verify: manifest present + one chapters/NNN.txt per chapter.
        await using var fs = File.OpenRead(ckpPath);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Read);

        archive.GetEntry("manifest.json").Should().NotBeNull();
        archive.GetEntry("chapters/001.txt").Should().NotBeNull();
        archive.GetEntry("chapters/002.txt").Should().NotBeNull();

        using var r1 = new StreamReader(archive.GetEntry("chapters/001.txt")!.Open());
        string ch1 = await r1.ReadToEndAsync(TestContext.Current.CancellationToken);
        ch1.Should().Contain("Body of one");
    }

    [Fact]
    public async Task TranspileAndWriteAsync_round_trips_through_reader()
    {
        string epubPath = Path.Combine(_tempDir, "roundtrip.epub");
        string ckpPath = Path.Combine(_tempDir, "roundtrip.ckp");
        EpubFixtureBuilder.WriteFlatEpub(epubPath,
        [
            new("A", "<p>First.</p>"),
            new("B", "<p>Second.</p>")
        ]);

        var transpiler = new EpubTranspiler(epubPath, SampleMetadata);
        await transpiler.TranspileAndWriteAsync(ckpPath, TestContext.Current.CancellationToken);

        await using var fs = File.OpenRead(ckpPath);
        var reader = new CkpPackageReader();
        var roundTripped = await reader.ReadAsync(fs, TestContext.Current.CancellationToken);

        roundTripped.Manifest.Book.Key.Should().Be("test-book-1e");
        roundTripped.Chapters.Should().HaveCount(2);
        roundTripped.Claims.Should().BeEmpty();
    }

    [Fact]
    public async Task TranspileAndWriteAsync_rejects_null_transpiler()
    {
        EpubTranspiler? transpiler = null;

        Func<Task> act = () => EpubTranspilerExtensions.TranspileAndWriteAsync(
            transpiler!, Path.Combine(_tempDir, "x.ckp"));

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task TranspileAndWriteAsync_rejects_empty_output_path()
    {
        string epubPath = Path.Combine(_tempDir, "for-null.epub");
        EpubFixtureBuilder.WriteFlatEpub(epubPath, [new("T", "<p>Body.</p>")]);

        var transpiler = new EpubTranspiler(epubPath, SampleMetadata);

        Func<Task> act = () => transpiler.TranspileAndWriteAsync("");
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
