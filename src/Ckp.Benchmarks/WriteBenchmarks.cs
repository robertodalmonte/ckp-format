namespace Ckp.Benchmarks;

using BenchmarkDotNet.Attributes;
using Ckp.Benchmarks.Fixtures;
using Ckp.Core;
using Ckp.IO;

/// <summary>
/// Measures <see cref="CkpPackageWriter.WriteAsync"/> at several claim counts.
/// Captures allocations via <see cref="MemoryDiagnoserAttribute"/> so the P2
/// streaming-writer fix has a before/after baseline to beat.
/// </summary>
[MemoryDiagnoser]
public class WriteBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int ClaimCount { get; set; }

    private CkpPackage _package = null!;
    private CkpPackageWriter _writer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _package = SyntheticPackageBuilder.Build(ClaimCount);
        _writer = new CkpPackageWriter();
    }

    [Benchmark]
    public async Task Write()
    {
        using var ms = new MemoryStream(capacity: ClaimCount * 512);
        await _writer.WriteAsync(_package, ms);
    }
}
