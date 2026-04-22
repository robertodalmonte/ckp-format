namespace Ckp.Benchmarks;

using BenchmarkDotNet.Running;

/// <summary>
/// Entry point for the benchmark harness. Run with:
///   <c>dotnet run -c Release --project src/Ckp.Benchmarks -- --filter *</c>
/// </summary>
/// <remarks>
/// The project is intentionally excluded from <c>ckp-format.slnx</c> so that
/// <c>dotnet build</c> / <c>dotnet test</c> in CI don't pay its build cost. Run it
/// explicitly when measuring performance. Results are written to
/// <c>BenchmarkDotNet.Artifacts/results/</c>; commit the relevant Markdown files
/// alongside the performance-baseline doc.
/// </remarks>
public static class Program
{
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
