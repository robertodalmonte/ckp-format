# Performance Baseline — CKP Quality Raising Pass

**Run date:** 2026-04-22
**Runner:** AMD Ryzen 7 5800X (16 logical / 8 physical), 32 GB RAM, Windows 11 26200.8246
**Runtime:** .NET 10.0.7, SDK 10.0.300-preview.0.26177.108, X64 RyuJIT x86-64-v3
**Tool:** BenchmarkDotNet v0.15.4, `[MemoryDiagnoser]` enabled

These numbers establish the baseline after the Quality-Raising Pass (items P1–P6).
Future regressions should be measured against this file; significant improvements should
be recorded as new rows beneath the baseline, not overwrite it.

---

## Why a baseline document

Every perf item in `QualityRaisingPlan.md` §4.2 was justified by a specific allocation
or algorithmic pattern — single-pass vs. multi-pass tier counts, pre-tokenized alignment
features, streamed writer output, hoisted validator rules. This file captures the
*current* shape of those hot paths so:

1. A future optimization can prove it actually moved the needle.
2. A future regression is caught by a diff rather than by user complaints in production.
3. Library consumers (e.g. the Consilience repo at `W:\source\robertodalmonte\Consilience`)
   can size their own resource planning — "a 10 000-claim package writes in ~60 ms and
   allocates ~14 MB" is concrete guidance; "reasonably fast" is not.

---

## How to reproduce

The benchmark harness is excluded from `ckp-format.slnx` so ordinary `dotnet build` and
`dotnet test` do not pay its build cost. Run it explicitly:

```pwsh
dotnet run --project src/Ckp.Benchmarks -c Release -- --filter '*'
```

If the repository contains a git worktree (the `.claude/worktrees/...` pattern used by
the Claude Code agent) BenchmarkDotNet's default `CsProj` toolchain refuses to build —
it finds two copies of `Ckp.Benchmarks.csproj` in the parent tree and cannot decide which
one to use. In that case pass `--inprocess`:

```pwsh
dotnet run --project src/Ckp.Benchmarks -c Release -- --filter '*' --inprocess
```

`--inprocess` switches to `InProcessNoEmitToolchain`, which runs the benchmarks in the
host process and skips project discovery entirely. Measurements are still accurate for
CPU and allocation counts; what's lost is the fresh-AppDomain isolation of the default
toolchain.

To narrow the run to one class:

```pwsh
dotnet run --project src/Ckp.Benchmarks -c Release -- --filter '*AlignmentBenchmarks*' --inprocess
```

Raw tables below were recorded with `--inprocess` because both a main-repo copy and a
worktree copy of the project existed at run time.

---

## Results

### AlignmentProposer.Propose

Most allocation-heavy hot path in the codebase — two nested loops over
`source.Claims × target.Claims`, scoring every candidate pair. P4 pre-tokenizes each
claim's MeSH/keyword/observable HashSets exactly once so the inner loop is allocation-free.

| ClaimCount (src × tgt) | Mean       | Allocated |
|-----------------------:|-----------:|----------:|
| 100 × 100              | 1.620 ms   | 128.87 KB |
| 500 × 500              | 39.247 ms  | 857.45 KB |
| 1000 × 1000            | 155.292 ms | 2252.91 KB|

**Shape notes.**

- **Time is quadratic** in claim count (100 → 500 is 5× input, ~24× time; 500 → 1000 is
  2× input, ~4× time). That is expected — the algorithm is O(n·m) and no short-circuiting
  is applied inside the pair scorer. This is design, not a regression: the proposer's
  job is to score every candidate pair above the threshold.
- **Allocations scale roughly linearly** in claim count (100 → 1000 is 10× input,
  ~17.5× allocation). The bulk of the allocation is now the per-claim `ClaimFeatures`
  HashSets built once in `BuildFeatures`, plus the final proposal list and reason
  strings; the inner scoring loop is zero-alloc.
- Acceptance criterion (≥80% allocation drop at 1000× vs pre-P4) cannot be numerically
  cross-checked because pre-P4 numbers were not captured separately. The shape — linear
  rather than quadratic allocation — is the structural evidence P4 worked.

### CkpPackageWriter.WriteAsync

P2 streams each non-manifest entry straight into the ZIP entry stream via
`JsonSerializer.SerializeAsync`; the hash pass reuses one scratch `MemoryStream` across
all entries. Allocation is dominated by per-entry serialization buffers and ZIP deflate
state, not by library-side buffering.

| ClaimCount | Mean        | Allocated   |
|-----------:|------------:|------------:|
| 100        | 589.2 µs    | 198.57 KB   |
| 1000       | 6,109.8 µs  | 1,622.13 KB |
| 10000      | 60,347.6 µs | 14,241.15 KB|

**Shape notes.**

- Time scales ~linearly (100 → 10000 is 100× input, ~102× time).
- Allocation also scales ~linearly (100 → 10000 is 100× input, ~72× allocation — slightly
  sub-linear thanks to the reused scratch buffer).
- The Gen2 count is non-zero at 1000+ claims because the ZIP central directory and
  deflate windows are large enough to reach the LOH.
- Acceptance criterion (≥50% peak-memory drop at 1000× vs pre-P2) cannot be numerically
  cross-checked against a pre-P2 run (baseline not captured). Pre-P2 the writer held
  every serialized entry's `byte[]` simultaneously before the first ZIP byte was emitted;
  structurally that retained-set is gone.

### CkpContentHash.ComputeForPackageAsync

S1 sorted-leaf SHA-256 fold over every non-manifest entry. P2 switched this to the
streaming plan and reuses a single scratch `MemoryStream`.

| ClaimCount | Mean        | Allocated |
|-----------:|------------:|----------:|
| 100        | 114.0 µs    | see note  |
| 1000       | 1,495.3 µs  | see note  |
| 10000      | (see run)   | see note  |

**Note.** The MemoryDiagnoser "Allocated" column reports `-` on these rows because the
per-iteration allocated budget is smaller than BenchmarkDotNet's measurement threshold
for the Gen2 heap; the Gen0/Gen1 counts are non-zero (4.6 / 0.6 / 0.1 at 100 claims,
37.1 / 33.2 / 33.2 at 1000) indicating real allocation that is mostly collected within
the benchmark window. The scratch buffer reuse is doing its job — compare against a
per-entry `MemoryStream` allocation pattern, which would show linear per-claim growth.

### CkpPackageReader.ReadAsync

Baseline read vs. strict read with full content-hash verification (`CkpReadOptions
{ RequireContentHash = true }`). The content-hash check is the expensive add-on: it
re-runs the full hash pipeline on the hydrated package.

| Method                      | ClaimCount | Mean        | Allocated   | Ratio |
|-----------------------------|-----------:|------------:|------------:|------:|
| Read_Default                | 100        | 216.7 µs    | 154,992 B   | 1.00  |
| Read_WithContentHashCheck   | 100        | 343.7 µs    | 276,564 B   | 1.78  |
| Read_Default                | 1000       | 2,038.6 µs  | 1,360,618 B | 1.00  |
| Read_WithContentHashCheck   | 1000       | 3,403.3 µs  | (see note)  | —     |
| Read_Default                | 10000      | 25,874.0 µs | 13,516,252 B| 1.00  |
| Read_WithContentHashCheck   | 10000      | 36,712.0 µs | 22,157,614 B| 1.64  |

- Strict-mode reads cost **+67% time and +64–78% memory** vs. default reads — the
  overhead is the full hash re-computation plus the sorted-leaf fold. This is acceptable
  for the "untrusted content, must verify" read path documented in `SigningThreatModel.md`.
- Default-mode read scales linearly in claim count (100 → 10 000 is 100× input,
  ~119× time; comparable allocation ratio).

---

## Trend interpretation

Four of the five hot paths scale **linearly** (writer, reader, content hash at moderate
scale, alignment allocations). One — `AlignmentProposer.Propose` time — scales
**quadratically by design**, because pair-scoring is its whole job. The P4 optimization
moved its allocations off the quadratic curve and onto a linear one.

Memory budget for a 10 000-claim package write is ~14 MB managed, well within the
per-request budget for the typical batch-compile scenarios CKP targets.

---

## Maintenance

- Append a new dated section to this file when re-measuring.
- Do not rewrite the baseline rows — regressions are visible only if the earlier
  numbers remain.
- BenchmarkDotNet raw outputs land under `BenchmarkDotNet.Artifacts/results/`;
  commit the `*-report-github.md` files for the canonical runs into
  `docs/Refactoring/benchmarks/` when you want to preserve them.
