namespace Ckp.Epub;

/// <summary>
/// A single chapter extracted from an ePub file.
/// </summary>
/// <param name="ChapterNumber">One-based chapter number.</param>
/// <param name="Title">Chapter title from the TOC or a generated label.</param>
/// <param name="Text">Full plain-text content of the chapter.</param>
/// <remarks>
/// <b>Intended consumer:</b> the <c>Ckp.Epub.Cli</c> tool, which iterates
/// <see cref="EpubTranspiler.Chapters"/> to write one <c>chapters/NN-slug.txt</c> file
/// per chapter alongside the package. This record is the cross-assembly surface of that
/// CLI-facing side channel; library users writing packages directly do not need it.
/// </remarks>
public sealed record ChapterText(int ChapterNumber, string Title, string Text);
