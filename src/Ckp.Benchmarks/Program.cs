namespace Ckp.Benchmarks;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.NoEmit;

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
/// <para>
/// Pass <c>--inprocess</c> to force the <see cref="InProcessNoEmitToolchain"/>. This is
/// needed when the repo contains a git worktree (<c>.claude/worktrees/...</c>) that
/// duplicates <c>Ckp.Benchmarks.csproj</c> — BenchmarkDotNet's default CsProj toolchain
/// refuses to build when two matching project files are found in the parent directory
/// tree. InProcess mode skips project discovery entirely and runs the benchmarks
/// in the host process, which is fine for measuring CPU/allocations but loses the
/// fresh-AppDomain isolation of the regular toolchain.
/// </para>
/// </remarks>
public static class Program
{
    public static int Main(string[] args)
    {
        bool inProcess = args.Contains("--inprocess");
        var filtered = args.Where(a => a != "--inprocess").ToArray();

        IConfig? config = inProcess
            ? ManualConfig.CreateMinimumViable().AddJob(
                Job.Default.WithToolchain(InProcessNoEmitToolchain.Instance))
            : null;

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(filtered, config);
        return 0;
    }
}
