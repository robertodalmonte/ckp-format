namespace Ckp.Epub;

using System.IO.Compression;
using System.Text;
using Ckp.Core;
using Ckp.IO;

/// <summary>
/// Convenience façade over <see cref="EpubTranspiler"/> that bundles the transpile,
/// package-write, and chapter-text append steps behind one call. Exists so
/// <c>Ckp.Epub.Cli</c> can depend only on <c>Ckp.Epub</c> (and <c>Ckp.Core</c>) without
/// taking a direct reference on <c>Ckp.IO</c> — the architectural invariant documented
/// in <c>docs/Architecture.md</c> (A2/A3).
/// </summary>
/// <remarks>
/// <para>
/// The chapter-text append is specific to the ePub flow: after <see cref="CkpPackageWriter"/>
/// emits the canonical package, we re-open the archive in <see cref="ZipArchiveMode.Update"/>
/// and splice in one <c>chapters/NNN.txt</c> entry per extracted chapter. Doing this after
/// the writer closes means the chapter texts are not included in the content-hash fold —
/// by design, because they are auxiliary source material, not claim content.
/// </para>
/// </remarks>
public static class EpubTranspilerExtensions
{
    /// <summary>
    /// Transpiles the ePub and writes the resulting skeleton package (plus the extracted
    /// chapter text files) to the given destination file path.
    /// </summary>
    public static async Task<CkpPackage> TranspileAndWriteAsync(
        this EpubTranspiler transpiler,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transpiler);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var package = await transpiler.TranspileAsync(cancellationToken);

        var writer = new CkpPackageWriter();
        await using (var fileStream = File.Create(outputPath))
        {
            await writer.WriteAsync(package, fileStream, cancellationToken);
        }

        await AppendChapterTextAsync(transpiler, outputPath, cancellationToken);
        return package;
    }

    private static async Task AppendChapterTextAsync(
        EpubTranspiler transpiler, string outputPath, CancellationToken cancellationToken)
    {
        await using var fileStream = File.Open(outputPath, FileMode.Open, FileAccess.ReadWrite);
        using var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, leaveOpen: false);
        foreach (ChapterText chapter in transpiler.Chapters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = archive.CreateEntry(
                $"chapters/{chapter.ChapterNumber:D3}.txt",
                CompressionLevel.Optimal);
            await using var stream = entry.Open();
            await using var sw = new StreamWriter(stream, Encoding.UTF8);
            await sw.WriteAsync(chapter.Text);
        }
    }
}
