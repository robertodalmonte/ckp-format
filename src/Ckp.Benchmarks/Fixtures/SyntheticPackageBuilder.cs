namespace Ckp.Benchmarks.Fixtures;

using Ckp.Core;

/// <summary>
/// Deterministic synthetic-package builder used by every benchmark class so that
/// run-to-run variance comes from the code under test, not from fixture construction.
/// All RNG is seeded; the same <paramref name="claimCount"/> always yields byte-identical
/// packages (modulo <see cref="PackageManifest.CreatedAt"/>, which is pinned via
/// <see cref="TimeProvider"/>).
/// </summary>
internal static class SyntheticPackageBuilder
{
    private static readonly DateTimeOffset PinnedNow =
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Pool sizes — chosen so that a sample of N claims exhibits realistic
    // keyword/MeSH overlap (important for AlignmentProposer benchmarks).
    private const int KeywordPoolSize = 64;
    private const int MeshPoolSize = 64;
    private const int DomainCount = 8;

    private static readonly string[] KeywordPool = BuildPool("kw", KeywordPoolSize);
    private static readonly string[] MeshPool = BuildPool("D", MeshPoolSize);
    private static readonly string[] DomainPool =
    [
        "autonomic-nervous-system", "fascial-anatomy", "biomechanics",
        "inflammation", "cardiovascular", "respiratory", "endocrine", "musculoskeletal",
    ];

    /// <summary>Build a synthetic package with a deterministic shape.</summary>
    /// <param name="claimCount">Number of <see cref="PackageClaim"/> entries.</param>
    /// <param name="bookKey">Used both as book key and as the claim-id prefix.</param>
    /// <param name="seed">RNG seed so two builders with different seeds produce
    /// partially-overlapping keyword/MeSH distributions for alignment benchmarks.</param>
    public static CkpPackage Build(int claimCount, string bookKey = "bench-1e", int seed = 42)
    {
        var rng = new Random(seed);
        var claims = new List<PackageClaim>(claimCount);

        // Per-domain sequence counters so generated ids fit the required format
        // `{book-key}.{DOMAIN-CODE}.{NNN}` used by validation rule S4.
        var perDomainSeq = new int[DomainCount];

        for (int i = 0; i < claimCount; i++)
        {
            int domainIdx = rng.Next(DomainCount);
            string domain = DomainPool[domainIdx];
            string domainCode = GetDomainCode(domainIdx);
            int seq = ++perDomainSeq[domainIdx];
            string id = $"{bookKey}.{domainCode}.{seq:D3}";

            claims.Add(PackageClaim.CreateNew(
                id: id,
                statement: $"Synthetic claim {i:D5} describes mechanism {i % 128} in {domain}.",
                tier: (Tier)((i & 3) + 1), // T1–T4
                domain: domain,
                keywords: SampleFromPool(rng, KeywordPool, count: 6),
                meshTerms: SampleFromPool(rng, MeshPool, count: 4)));
        }

        var domains = Enumerable.Range(0, DomainCount)
            .Where(i => perDomainSeq[i] > 0)
            .Select(i => new DomainInfo(
                Name: DomainPool[i],
                ClaimCount: perDomainSeq[i],
                T1Count: 0, T2Count: 0, T3Count: 0, T4Count: 0))
            .ToList();

        var fingerprint = new ContentFingerprint(
            Algorithm: "SHA-256",
            ClaimCount: claims.Count,
            DomainCount: domains.Count,
            T1Count: claims.Count(c => c.Tier == Tier.T1),
            T2Count: claims.Count(c => c.Tier == Tier.T2),
            T3Count: claims.Count(c => c.Tier == Tier.T3),
            T4Count: claims.Count(c => c.Tier == Tier.T4),
            CitationCount: 0);

        var book = new BookMetadata(
            Key: bookKey,
            Title: $"Benchmarks Book {bookKey}",
            Edition: 1,
            Authors: ["Bench, A."],
            Publisher: "Benchmark Press",
            Year: 2026,
            Isbn: null,
            Language: "en",
            Domains: domains.Select(d => d.Name).ToList());

        var manifest = PackageManifest.CreateNew(
            book: book,
            fingerprint: fingerprint,
            timeProvider: new FixedTimeProvider(PinnedNow),
            idFactory: () => Guid.Empty);

        return new CkpPackage
        {
            Manifest = manifest,
            Claims = claims,
            Domains = domains,
        };
    }

    private static string GetDomainCode(int domainIdx) => domainIdx switch
    {
        0 => "ANS",
        1 => "FAS",
        2 => "BIO",
        3 => "INF",
        4 => "CVS",
        5 => "RES",
        6 => "END",
        7 => "MSK",
        _ => "XYZ",
    };

    private static string[] BuildPool(string prefix, int size) =>
        [.. Enumerable.Range(0, size).Select(i => $"{prefix}{i:D3}")];

    private static List<string> SampleFromPool(Random rng, string[] pool, int count)
    {
        var picked = new HashSet<string>();
        while (picked.Count < count)
            picked.Add(pool[rng.Next(pool.Length)]);
        return [.. picked];
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
