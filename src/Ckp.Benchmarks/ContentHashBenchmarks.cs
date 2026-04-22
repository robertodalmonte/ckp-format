namespace Ckp.Benchmarks;

using BenchmarkDotNet.Attributes;
using Ckp.Benchmarks.Fixtures;
using Ckp.Core;
using Ckp.IO;

/// <summary>
/// Measures <see cref="CkpContentHash.ComputeForPackageAsync"/>. Included because
/// S1's sorted-leaf fold is now invoked twice on every write (once for hash injection,
/// once transitively via serialization) and once per strict-mode read; a regression
/// here penalizes the whole package lifecycle.
/// </summary>
[MemoryDiagnoser]
public class ContentHashBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int ClaimCount { get; set; }

    private CkpPackage _package = null!;

    [GlobalSetup]
    public void Setup() => _package = SyntheticPackageBuilder.Build(ClaimCount);

    [Benchmark]
    public async Task<string> Hash() =>
        await CkpContentHash.ComputeForPackageAsync(_package);
}
