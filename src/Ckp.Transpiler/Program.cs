using Ckp.IO;
using Ckp.Transpiler;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run -- <knowledgebase-path> <output.ckp>");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  knowledgebase-path  Path to the Consilience KnowledgeBase directory");
    Console.Error.WriteLine("  output.ckp          Path for the output .ckp package");
    return 1;
}

string kbPath = args[0];
string outputPath = args[1];

if (!Directory.Exists(kbPath))
{
    Console.Error.WriteLine($"KnowledgeBase directory not found: {kbPath}");
    return 1;
}

Console.WriteLine($"Transpiling KnowledgeBase: {kbPath}");

var transpiler = new KnowledgeBaseTranspiler(kbPath);
var package = await transpiler.TranspileAsync();

Console.WriteLine($"  Claims:    {package.Claims.Count}");
Console.WriteLine($"  Citations: {package.Citations.Count}");
Console.WriteLine($"  Domains:   {package.Domains.Count}");
Console.WriteLine($"  T1: {package.Manifest.ContentFingerprint.T1Count}  T2: {package.Manifest.ContentFingerprint.T2Count}  T3: {package.Manifest.ContentFingerprint.T3Count}  T4: {package.Manifest.ContentFingerprint.T4Count}");

var writer = new CkpPackageWriter();
await using var outStream = File.Create(outputPath);
await writer.WriteAsync(package, outStream);

Console.WriteLine($"Package written: {outputPath}");
return 0;
