# Coverage baseline — post-Phase 2

**Captured:** 2026-04-22 after T5 commit.
**Runner:** `pwsh -File scripts/coverage.ps1` → `coverlet.collector` → ReportGenerator 5.5.6.
**Raw data:** `TestResults/coverage/Cobertura.xml`. HTML view: `TestResults/coverage/index.html`.

## Overall

| Metric | Value |
|---|---|
| Line coverage | 83.6 % (2177 / 2604) |
| Branch coverage | 73.6 % (598 / 812) |
| Method coverage | 92.3 % (434 / 470) |
| Total tests | 258 passing, 0 skipped |

## Per-project branch coverage

Targets from QualityRaisingPlan T9: Core / IO / Signing ≥ 85 %, Transpiler / Epub ≥ 75 %.

| Package | Line | Branch | Target | Status |
|---|---|---|---|---|
| `Ckp.Core` | 93.4 % | **92.9 %** | ≥ 85 % | ✅ |
| `Ckp.IO` | 92.1 % | **84.1 %** | ≥ 85 % | ⚠ -0.9 pp |
| `Ckp.Signing` | 85.9 % | **88.9 %** | ≥ 85 % | ✅ |
| `Ckp.Transpiler` | 90.7 % | **77.2 %** | ≥ 75 % | ✅ |
| `Ckp.Epub` | 66.8 % | **58.2 %** | ≥ 75 % | ❌ -16.8 pp |

## Gaps to close

### `Ckp.IO` — just under target

Uncovered branches cluster in:

- `CkpPackageReader.IsAlignmentEntry` — the traversal normalization loop has six
  path-segment shapes and only four are currently exercised by the Theory.
  Adding two more `InlineData` rows (empty segments, trailing `/`) would close
  most of the gap.
- `CkpFormatException` constructor with a non-null `innerException` — the T3
  reader wraps `JsonException` into it (covered), but the three-arg form with
  an unrelated exception is not exercised.
- The `isRequired: false` branch of `ReadEntryStreamAsync` where the optional
  entry contains malformed JSON — currently no test hits this path, because the
  catch is gated on `isRequired`.

These are low-effort one-liners and should be bundled into the T9 closure
commit, but were not part of the T3/T5 behaviour work that prompted this
baseline.

### `Ckp.Epub` — 16.8 pp below target

The ePub pipeline has the least test coverage by design — `EpubExtractor`
delegates to VersOne.Epub which itself is not exercised in our tests, and
`EpubTranspiler`'s navigation-tree walk has nine branches for TOC shapes we
haven't built fixtures for. Expected work to close to 75 %:

- Fixture an ePub with a nested nav (≥ 2 TOC depth) to hit `CollectChapters`
  recursion.
- Fixture an ePub whose nav item has no content reference to hit the
  skip-empty branch.
- Add a `StripHtml` test with each of the four compiled regex patterns
  firing in isolation (blocks, whitespace, multiple newlines, html tags).

Tracked as a follow-up in Phase 4.

## Methodology

`coverlet.collector` is configured via the `coverlet.runsettings`-free
default path. The `scripts/coverage.ps1` wrapper runs:

1. `dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults/coverage-raw`
2. `reportgenerator -reports:...\coverage.cobertura.xml -targetdir:TestResults\coverage -reporttypes:HtmlInline_AzurePipelines;TextSummary;Cobertura -assemblyfilters:+Ckp.*;-Ckp.Tests;-Ckp.*.Cli`

CLIs (`Ckp.Transpiler.Cli`, `Ckp.Epub.Cli`) are filtered out because they are
thin argument-parsing shells not intended to carry test coverage.
