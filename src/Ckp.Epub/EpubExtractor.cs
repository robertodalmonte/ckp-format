namespace Ckp.Epub;

using System.Text.RegularExpressions;
using VersOne.Epub;

/// <summary>
/// Extracts chapters from ePub files using the publisher's navigation TOC.
/// Falls back to reading order when no TOC is present.
/// </summary>
internal static partial class EpubExtractor
{
    [GeneratedRegex(@"<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagPattern();

    [GeneratedRegex(@"[ \t]+", RegexOptions.Compiled)]
    private static partial Regex MultipleWhitespace();

    [GeneratedRegex(@"\n{3,}", RegexOptions.Compiled)]
    private static partial Regex MultipleNewlines();

    [GeneratedRegex(@"<(br|p|div|h[1-6]|li|tr|blockquote)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex BlockLevelTags();

    /// <summary>
    /// Reads an ePub file and extracts chapters using the navigation TOC.
    /// </summary>
    public static async Task<ChapterText[]> ExtractAsync(string epubPath)
    {
        if (!Path.GetExtension(epubPath).Equals(".epub", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Unsupported file format: {Path.GetExtension(epubPath)}. Only .epub files are supported.",
                nameof(epubPath));

        if (!File.Exists(epubPath))
            throw new FileNotFoundException($"ePub file not found: {epubPath}", epubPath);

        EpubBook book = await EpubReader.ReadBookAsync(epubPath);

        List<ChapterText> chapters = [];

        if (book.Navigation is { Count: > 0 })
        {
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            int chapterNumber = 0;
            CollectChapters(book.Navigation, chapters, seen, ref chapterNumber);
        }
        else
        {
            int chapterNumber = 0;
            foreach (EpubLocalTextContentFile contentFile in book.ReadingOrder)
            {
                string text = StripHtml(contentFile.Content);
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                chapterNumber++;
                chapters.Add(new ChapterText(chapterNumber, $"Section {chapterNumber}", text));
            }
        }

        return [.. chapters];
    }

    private static void CollectChapters(
        List<EpubNavigationItem> items,
        List<ChapterText> chapters,
        HashSet<string> seen,
        ref int chapterNumber)
    {
        foreach (EpubNavigationItem item in items)
        {
            if (item.HtmlContentFile is not null)
            {
                string fileKey = item.HtmlContentFile.FilePath ?? "";
                if (seen.Add(fileKey))
                {
                    string text = StripHtml(item.HtmlContentFile.Content);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        chapterNumber++;
                        string title = item.Title?.Trim() ?? $"Chapter {chapterNumber}";
                        chapters.Add(new ChapterText(chapterNumber, title, text));
                    }
                }
            }

            if (item.NestedItems is { Count: > 0 })
                CollectChapters(item.NestedItems, chapters, seen, ref chapterNumber);
        }
    }

    /// <summary>
    /// Strips HTML tags from XHTML content and normalizes whitespace.
    /// </summary>
    public static string StripHtml(string html)
    {
        string text = BlockLevelTags().Replace(html, "\n");
        text = HtmlTagPattern().Replace(text, "");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = MultipleWhitespace().Replace(text, " ");
        text = MultipleNewlines().Replace(text, "\n\n");
        return text.Trim();
    }
}
