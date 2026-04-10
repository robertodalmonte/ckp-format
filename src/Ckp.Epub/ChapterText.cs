namespace Ckp.Epub;

/// <summary>
/// A single chapter extracted from an ePub file.
/// </summary>
/// <param name="ChapterNumber">One-based chapter number.</param>
/// <param name="Title">Chapter title from the TOC or a generated label.</param>
/// <param name="Text">Full plain-text content of the chapter.</param>
internal sealed record ChapterText(int ChapterNumber, string Title, string Text);
