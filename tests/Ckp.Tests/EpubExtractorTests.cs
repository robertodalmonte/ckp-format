namespace Ckp.Tests;

using Ckp.Epub;

public sealed class EpubExtractorTests
{
    [Fact]
    public async Task ExtractAsync_throws_for_non_epub_extension()
    {
        Func<Task> act = () => EpubExtractor.ExtractAsync("book.txt");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unsupported file format*");
    }

    [Fact]
    public async Task ExtractAsync_throws_for_missing_file()
    {
        Func<Task> act = () => EpubExtractor.ExtractAsync("/nonexistent/book.epub");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public void StripHtml_removes_tags()
    {
        string result = EpubExtractor.StripHtml("<p>Hello <b>world</b></p>");

        result.Should().Be("Hello world");
    }

    [Fact]
    public void StripHtml_decodes_html_entities()
    {
        string result = EpubExtractor.StripHtml("<p>A &amp; B &lt; C</p>");

        result.Should().Be("A & B < C");
    }

    [Fact]
    public void StripHtml_converts_block_tags_to_newlines()
    {
        string result = EpubExtractor.StripHtml("<h1>Title</h1><p>First paragraph.</p><p>Second paragraph.</p>");

        result.Should().Contain("Title");
        result.Should().Contain("First paragraph.");
        result.Should().Contain("Second paragraph.");
        result.Should().NotContain("TitleFirst");
    }

    [Fact]
    public void StripHtml_normalizes_excessive_whitespace()
    {
        string result = EpubExtractor.StripHtml("<p>Too    many     spaces</p>");

        result.Should().Be("Too many spaces");
    }

    [Fact]
    public void StripHtml_collapses_excessive_newlines()
    {
        string result = EpubExtractor.StripHtml("<p>A</p>\n\n\n\n\n<p>B</p>");

        result.Should().NotContain("\n\n\n");
    }
}
