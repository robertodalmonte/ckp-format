using System.IO.Compression;
using System.Text;
using Ckp.Epub;
using Ckp.IO;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run -- <book.epub> <output.ckp> --key <key> [options]");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  book.epub           Path to the source ePub file");
    Console.Error.WriteLine("  output.ckp          Path for the output .ckp package");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Required:");
    Console.Error.WriteLine("  --key <key>         Short book identifier (e.g., my-textbook-3e)");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Optional:");
    Console.Error.WriteLine("  --title <title>     Book title (defaults to ePub filename)");
    Console.Error.WriteLine("  --authors <names>   Comma-separated author names");
    Console.Error.WriteLine("  --publisher <name>  Publisher name");
    Console.Error.WriteLine("  --edition <N>       Edition number (default: 1)");
    Console.Error.WriteLine("  --year <YYYY>       Publication year (default: current year)");
    return 1;
}

string epubPath = args[0];
string outputPath = args[1];

if (!File.Exists(epubPath))
{
    Console.Error.WriteLine($"ePub file not found: {epubPath}");
    return 1;
}

// Parse named arguments
string? key = null;
string? title = null;
string? authors = null;
string? publisher = null;
int edition = 1;
int year = DateTime.UtcNow.Year;

for (int i = 2; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--key" when i + 1 < args.Length:
            key = args[++i];
            break;
        case "--title" when i + 1 < args.Length:
            title = args[++i];
            break;
        case "--authors" when i + 1 < args.Length:
            authors = args[++i];
            break;
        case "--publisher" when i + 1 < args.Length:
            publisher = args[++i];
            break;
        case "--edition" when i + 1 < args.Length:
            edition = int.Parse(args[++i]);
            break;
        case "--year" when i + 1 < args.Length:
            year = int.Parse(args[++i]);
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 1;
    }
}

if (key is null)
{
    Console.Error.WriteLine("Error: --key is required.");
    return 1;
}

title ??= Path.GetFileNameWithoutExtension(epubPath);

var metadata = new BookMetadataArgs(
    Key: key,
    Title: title,
    Edition: edition,
    Authors: authors?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [],
    Publisher: publisher ?? "",
    Year: year);

Console.WriteLine($"Extracting: {epubPath}");

var transpiler = new EpubTranspiler(epubPath, metadata);
var package = await transpiler.TranspileAsync();

Console.WriteLine($"  Chapters: {package.Chapters.Count}");
Console.WriteLine($"  Claims:   {package.Claims.Count}");

// Step 1: write standard CKP package
var writer = new CkpPackageWriter();
await using var fileStream = File.Create(outputPath);
await writer.WriteAsync(package, fileStream);

// Step 2: reopen ZIP to append supplementary chapter text
fileStream.Seek(0, SeekOrigin.Begin);
using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Update, leaveOpen: false))
{
    foreach (ChapterText chapter in transpiler.Chapters)
    {
        var entry = archive.CreateEntry(
            $"chapters/{chapter.ChapterNumber:D3}.txt",
            CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var sw = new StreamWriter(stream, Encoding.UTF8);
        await sw.WriteAsync(chapter.Text);
    }
}

Console.WriteLine($"  Chapter text files written: {transpiler.Chapters.Length}");
Console.WriteLine($"Package written: {outputPath}");
return 0;
