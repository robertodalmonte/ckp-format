namespace Ckp.Tests;

using System.IO.Compression;
using Ckp.Core;
using Ckp.Epub;
using Ckp.IO;

public sealed class EpubTranspilerTests
{
    [Fact]
    public void Skeleton_package_has_zero_claims()
    {
        var package = CreateSkeletonPackage(chapterCount: 5);

        package.Claims.Should().BeEmpty();
        package.Manifest.ContentFingerprint.ClaimCount.Should().Be(0);
    }

    [Fact]
    public void Skeleton_package_has_zero_tier_counts()
    {
        var package = CreateSkeletonPackage(chapterCount: 3);
        var fp = package.Manifest.ContentFingerprint;

        fp.T1Count.Should().Be(0);
        fp.T2Count.Should().Be(0);
        fp.T3Count.Should().Be(0);
        fp.T4Count.Should().Be(0);
    }

    [Fact]
    public void Skeleton_package_has_zero_citations()
    {
        var package = CreateSkeletonPackage(chapterCount: 3);

        package.Citations.Should().BeEmpty();
        package.Manifest.ContentFingerprint.CitationCount.Should().Be(0);
    }

    [Fact]
    public void Skeleton_package_has_correct_book_metadata()
    {
        var package = CreateSkeletonPackage(chapterCount: 1);

        package.Manifest.Book.Key.Should().Be("test-1e");
        package.Manifest.Book.Title.Should().Be("Test Book");
        package.Manifest.Book.Edition.Should().Be(1);
        package.Manifest.Book.Authors.Should().ContainSingle().Which.Should().Be("Author");
    }

    [Fact]
    public void Skeleton_package_has_chapter_infos()
    {
        var package = CreateSkeletonPackage(chapterCount: 4);

        package.Chapters.Should().HaveCount(4);
        package.Chapters[0].Number.Should().Be(1);
        package.Chapters[0].Title.Should().Be("Chapter 1");
        package.Chapters[0].ClaimCount.Should().Be(0);
        package.Chapters[0].Domains.Should().BeEmpty();
    }

    [Fact]
    public void Skeleton_package_has_format_version()
    {
        var package = CreateSkeletonPackage(chapterCount: 1);

        package.Manifest.FormatVersion.Should().Be("1.0");
    }

    [Fact]
    public void Skeleton_package_has_edition_info()
    {
        var package = CreateSkeletonPackage(chapterCount: 1);

        package.Editions.Should().ContainSingle();
        package.Editions[0].Edition.Should().Be(1);
        package.Editions[0].Year.Should().Be(2026);
    }

    [Fact]
    public async Task Skeleton_package_round_trips()
    {
        var package = CreateSkeletonPackage(chapterCount: 3);
        var ct = TestContext.Current.CancellationToken;

        using var ms = new MemoryStream();
        var writer = new CkpPackageWriter();
        await writer.WriteAsync(package, ms, ct);
        ms.Position = 0;

        var reader = new CkpPackageReader();
        var roundTripped = await reader.ReadAsync(ms, ct);

        roundTripped.Claims.Should().BeEmpty();
        roundTripped.Chapters.Should().HaveCount(3);
        roundTripped.Chapters[0].Title.Should().Be("Chapter 1");
        roundTripped.Manifest.Book.Key.Should().Be("test-1e");
    }

    [Fact]
    public async Task Supplementary_chapters_can_be_appended_to_zip()
    {
        var package = CreateSkeletonPackage(chapterCount: 2);
        var chapters = new[]
        {
            new ChapterText(1, "Chapter 1", "Content of chapter one."),
            new ChapterText(2, "Chapter 2", "Content of chapter two.")
        };
        var ct = TestContext.Current.CancellationToken;

        using var ms = new MemoryStream();
        var writer = new CkpPackageWriter();
        await writer.WriteAsync(package, ms, ct);

        // Reopen in Update mode to append chapter text
        ms.Seek(0, SeekOrigin.Begin);
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            foreach (var ch in chapters)
            {
                var entry = archive.CreateEntry($"chapters/{ch.ChapterNumber:D3}.txt", CompressionLevel.Optimal);
                await using var stream = entry.Open();
                await using var sw = new StreamWriter(stream, System.Text.Encoding.UTF8);
                await sw.WriteAsync(ch.Text);
            }
        }

        // Verify chapter text entries exist and are readable
        ms.Seek(0, SeekOrigin.Begin);
        using var readArchive = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry1 = readArchive.GetEntry("chapters/001.txt");
        entry1.Should().NotBeNull();
        using var reader = new StreamReader(entry1!.Open());
        var text = await reader.ReadToEndAsync(ct);
        text.Should().Be("Content of chapter one.");

        readArchive.GetEntry("chapters/002.txt").Should().NotBeNull();
    }

    [Fact]
    public async Task Supplementary_chapters_do_not_break_ckp_reader()
    {
        var package = CreateSkeletonPackage(chapterCount: 1);
        var chapters = new[] { new ChapterText(1, "Ch 1", "Some text.") };
        var ct = TestContext.Current.CancellationToken;

        using var ms = new MemoryStream();
        var writer = new CkpPackageWriter();
        await writer.WriteAsync(package, ms, ct);

        ms.Seek(0, SeekOrigin.Begin);
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.CreateEntry("chapters/001.txt", CompressionLevel.Optimal);
            await using var stream = entry.Open();
            await using var sw = new StreamWriter(stream, System.Text.Encoding.UTF8);
            await sw.WriteAsync(chapters[0].Text);
        }

        // CkpPackageReader should still read the package without errors
        ms.Seek(0, SeekOrigin.Begin);
        var ckpReader = new CkpPackageReader();
        var roundTripped = await ckpReader.ReadAsync(ms, ct);

        roundTripped.Manifest.Book.Key.Should().Be("test-1e");
        roundTripped.Claims.Should().BeEmpty();
        roundTripped.Chapters.Should().ContainSingle();
    }

    private static CkpPackage CreateSkeletonPackage(int chapterCount)
    {
        var chapterInfos = Enumerable.Range(1, chapterCount)
            .Select(n => new ChapterInfo(n, $"Chapter {n}", ClaimCount: 0, Domains: []))
            .ToList();

        var book = new BookMetadata("test-1e", "Test Book", 1, ["Author"], "Publisher", 2026, null, "en", []);
        var fp = new ContentFingerprint("SHA-256", 0, 0, 0, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        var edition = new EditionInfo(1, 2026, null, null, "Structure extracted from ePub");

        return new CkpPackage
        {
            Manifest = manifest,
            Chapters = chapterInfos,
            Editions = [edition],
        };
    }
}
