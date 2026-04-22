namespace Ckp.Benchmarks;

using BenchmarkDotNet.Attributes;
using Ckp.Benchmarks.Fixtures;
using Ckp.IO;

/// <summary>
/// Measures <see cref="CkpPackageReader.ReadAsync"/> in default and strict-hash
/// modes. The strict-hash variant exercises <c>CkpContentHash</c> recomputation,
/// giving us a lens on the P2 streaming-writer fix's read-side counterpart.
/// </summary>
[MemoryDiagnoser]
public class ReadBenchmarks
{
    [Params(100, 1_000, 10_000)]
    public int ClaimCount { get; set; }

    private byte[] _bytes = null!;
    private CkpPackageReader _reader = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        var package = SyntheticPackageBuilder.Build(ClaimCount);
        using var ms = new MemoryStream(capacity: ClaimCount * 512);
        await new CkpPackageWriter().WriteAsync(package, ms);
        _bytes = ms.ToArray();
        _reader = new CkpPackageReader();
    }

    [Benchmark(Baseline = true)]
    public async Task Read_Default()
    {
        using var ms = new MemoryStream(_bytes, writable: false);
        _ = await _reader.ReadAsync(ms);
    }

    [Benchmark]
    public async Task Read_WithContentHashCheck()
    {
        using var ms = new MemoryStream(_bytes, writable: false);
        _ = await _reader.ReadAsync(ms, new CkpReadOptions { RequireContentHash = true });
    }
}
