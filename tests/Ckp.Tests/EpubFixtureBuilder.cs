namespace Ckp.Tests;

using System.IO.Compression;
using System.Text;

/// <summary>
/// Builds minimal valid EPUB 3 archives on disk for exercising
/// <c>EpubTranspiler</c>/<c>EpubExtractor</c> end-to-end without shipping a
/// real ePub fixture (which would bloat the repo and complicate licensing).
/// </summary>
/// <remarks>
/// An EPUB archive is a ZIP with five load-bearing members:
/// <list type="bullet">
///   <item><c>mimetype</c> — literal <c>application/epub+zip</c>, STORED (not deflated), first entry.</item>
///   <item><c>META-INF/container.xml</c> — points at the package document (<c>content.opf</c>).</item>
///   <item><c>OEBPS/content.opf</c> — manifest + spine; references the nav document.</item>
///   <item><c>OEBPS/nav.xhtml</c> — EPUB 3 navigation TOC.</item>
///   <item><c>OEBPS/chN.xhtml</c> — one chapter body per spine item.</item>
/// </list>
/// The builder is deliberately permissive — it accepts a chapter list and a
/// nested-TOC flag, and never tries to validate against the full EPUB schema.
/// VersOne.Epub tolerates every ePub we produce here; if it ever rejects one,
/// the test will fail loudly rather than silently.
/// </remarks>
internal static class EpubFixtureBuilder
{
    public sealed record EpubChapter(string Title, string BodyHtml);

    /// <summary>
    /// Writes an .epub file at <paramref name="path"/> with the given flat
    /// chapter list (EPUB 3 nav only, no nesting).
    /// </summary>
    public static void WriteFlatEpub(string path, IReadOnlyList<EpubChapter> chapters)
        => Write(path, chapters, nested: false);

    /// <summary>
    /// Writes an .epub file with a two-level nav — chapters are grouped into
    /// pairs under "Part 1", "Part 2" navigation points. Exercises the
    /// recursive <c>CollectChapters</c> walk in <c>EpubExtractor</c>.
    /// </summary>
    public static void WriteNestedEpub(string path, IReadOnlyList<EpubChapter> chapters)
        => Write(path, chapters, nested: true);

    private static void Write(
        string path, IReadOnlyList<EpubChapter> chapters, bool nested)
    {
        if (chapters.Count == 0)
            throw new ArgumentException("At least one chapter is required.", nameof(chapters));

        using var fs = File.Create(path);
        using var archive = new ZipArchive(fs, ZipArchiveMode.Create);

        // mimetype MUST be first, STORED (uncompressed), uncontaminated by extra fields.
        var mimeEntry = archive.CreateEntry("mimetype", CompressionLevel.NoCompression);
        using (var sw = new StreamWriter(mimeEntry.Open(), new UTF8Encoding(false)))
            sw.Write("application/epub+zip");

        WriteEntry(archive, "META-INF/container.xml", ContainerXml());
        WriteEntry(archive, "OEBPS/content.opf", ContentOpf(chapters));
        WriteEntry(archive, "OEBPS/nav.xhtml", NavXhtml(chapters, nested));

        for (int i = 0; i < chapters.Count; i++)
            WriteEntry(archive, $"OEBPS/ch{i + 1}.xhtml", ChapterXhtml(chapters[i]));
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var sw = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        sw.Write(content);
    }

    private static string ContainerXml() =>
        """
        <?xml version="1.0" encoding="UTF-8"?>
        <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
          <rootfiles>
            <rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/>
          </rootfiles>
        </container>
        """;

    private static string ContentOpf(IReadOnlyList<EpubChapter> chapters)
    {
        var manifest = new StringBuilder();
        manifest.AppendLine("    <item id=\"nav\" href=\"nav.xhtml\" media-type=\"application/xhtml+xml\" properties=\"nav\"/>");
        for (int i = 0; i < chapters.Count; i++)
            manifest.AppendLine($"    <item id=\"ch{i + 1}\" href=\"ch{i + 1}.xhtml\" media-type=\"application/xhtml+xml\"/>");

        var spine = new StringBuilder();
        for (int i = 0; i < chapters.Count; i++)
            spine.AppendLine($"    <itemref idref=\"ch{i + 1}\"/>");

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <package xmlns="http://www.idpf.org/2007/opf" unique-identifier="book-id" version="3.0">
              <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                <dc:identifier id="book-id">urn:uuid:test-fixture-0000</dc:identifier>
                <dc:title>Test Fixture Book</dc:title>
                <dc:language>en</dc:language>
              </metadata>
              <manifest>
            {manifest.ToString().TrimEnd()}
              </manifest>
              <spine>
            {spine.ToString().TrimEnd()}
              </spine>
            </package>
            """;
    }

    private static string NavXhtml(IReadOnlyList<EpubChapter> chapters, bool nested)
    {
        var body = new StringBuilder();
        body.AppendLine("<nav epub:type=\"toc\"><ol>");

        if (!nested)
        {
            for (int i = 0; i < chapters.Count; i++)
                body.AppendLine($"  <li><a href=\"ch{i + 1}.xhtml\">{System.Net.WebUtility.HtmlEncode(chapters[i].Title)}</a></li>");
        }
        else
        {
            // Group pairs of chapters under "Part N" navigation points.
            for (int partIndex = 0; partIndex < chapters.Count; partIndex += 2)
            {
                int partNum = partIndex / 2 + 1;
                body.AppendLine($"  <li><span>Part {partNum}</span><ol>");
                body.AppendLine($"    <li><a href=\"ch{partIndex + 1}.xhtml\">{System.Net.WebUtility.HtmlEncode(chapters[partIndex].Title)}</a></li>");
                if (partIndex + 1 < chapters.Count)
                    body.AppendLine($"    <li><a href=\"ch{partIndex + 2}.xhtml\">{System.Net.WebUtility.HtmlEncode(chapters[partIndex + 1].Title)}</a></li>");
                body.AppendLine("  </ol></li>");
            }
        }

        body.AppendLine("</ol></nav>");

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <html xmlns="http://www.w3.org/1999/xhtml" xmlns:epub="http://www.idpf.org/2007/ops">
              <head><title>Table of Contents</title></head>
              <body>
            {body}
              </body>
            </html>
            """;
    }

    private static string ChapterXhtml(EpubChapter chapter) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <html xmlns="http://www.w3.org/1999/xhtml">
          <head><title>{System.Net.WebUtility.HtmlEncode(chapter.Title)}</title></head>
          <body>
            <h1>{System.Net.WebUtility.HtmlEncode(chapter.Title)}</h1>
            {chapter.BodyHtml}
          </body>
        </html>
        """;
}
