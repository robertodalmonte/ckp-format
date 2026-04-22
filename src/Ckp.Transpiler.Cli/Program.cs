// ckp-transpile — Consilience KnowledgeBase → CKP package
//
// USAGE
//   ckp-transpile <knowledgebase-path> <output.ckp>
//
// ARGUMENTS
//   knowledgebase-path  Directory containing mechanisms/, integrations/,
//                       observations/, and traditions/ subfolders.
//   output.ckp          File path for the written package. Truncated if it
//                       already exists.
//
// EXIT CODES
//   0  Package written successfully.
//   1  Argument parse failure or missing input directory.
//
// EXAMPLE
//   ckp-transpile ./my-kb ./out/my-kb.ckp
//
// ARCHITECTURE
//   A2: this CLI depends only on Ckp.Transpiler (which transitively brings
//   Ckp.Core and Ckp.IO types surfaced through the façade). The direct Ckp.IO
//   reference that pre-A2 existed here has been dropped — see
//   docs/Architecture.md for the allowed-edges graph that forbids CLI projects
//   from reaching across the library layer.
using Ckp.Transpiler;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: ckp-transpile <knowledgebase-path> <output.ckp>");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  knowledgebase-path  Path to the KnowledgeBase directory");
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
var package = await transpiler.TranspileAndWriteAsync(outputPath);

Console.WriteLine($"  Claims:    {package.Claims.Count}");
Console.WriteLine($"  Citations: {package.Citations.Count}");
Console.WriteLine($"  Domains:   {package.Domains.Count}");
Console.WriteLine($"  T1: {package.Manifest.ContentFingerprint.T1Count}  T2: {package.Manifest.ContentFingerprint.T2Count}  T3: {package.Manifest.ContentFingerprint.T3Count}  T4: {package.Manifest.ContentFingerprint.T4Count}");
Console.WriteLine($"Package written: {outputPath}");
return 0;
