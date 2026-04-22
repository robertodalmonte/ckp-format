namespace Ckp.Benchmarks;

using BenchmarkDotNet.Attributes;
using Ckp.Benchmarks.Fixtures;
using Ckp.Core;
using Ckp.IO;

/// <summary>
/// Measures <see cref="AlignmentProposer.Propose"/>. This is the most allocation-heavy
/// path in the codebase — two nested loops over <c>source.Claims × target.Claims</c> where
/// every <c>ScorePair</c> call constructs at least two <see cref="HashSet{T}"/> instances
/// in <c>JaccardSimilarity</c>. The P4 fix pre-tokenizes and caches per-claim sets once.
/// </summary>
[MemoryDiagnoser]
public class AlignmentBenchmarks
{
    // Scale points chosen to stay under a reasonable per-iteration budget while still
    // being large enough to expose the quadratic cost. 100×100 = 10k score calls,
    // 500×500 = 250k, 1000×1000 = 1M.
    [Params(100, 500, 1_000)]
    public int ClaimCount { get; set; }

    private CkpPackage _source = null!;
    private CkpPackage _target = null!;
    private AlignmentProposer _proposer = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Use overlapping keyword/MeSH pools but different seeds so the proposer has
        // real work to do: some pairs score above the threshold, most don't.
        _source = SyntheticPackageBuilder.Build(ClaimCount, bookKey: "bench-src-1e", seed: 1);
        _target = SyntheticPackageBuilder.Build(ClaimCount, bookKey: "bench-tgt-1e", seed: 2);
        _proposer = new AlignmentProposer();
    }

    [Benchmark]
    public int Propose() => _proposer.Propose(_source, _target).Count;
}
