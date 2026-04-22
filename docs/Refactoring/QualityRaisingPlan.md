# Quality-Raising Plan

**Target repo:** `ckp-format` — master at `f8f5993` (post-P1–P3 refactor).
**Baseline:** 205 tests pass, solution builds clean under `TreatWarningsAsErrors`, output is byte-deterministic.
**Goal:** Move from "clean and working" to "top quality" along seven axes (architecture, API surface, test coverage, performance, signing security, spec, documentation).
**Consumer constraint:** `W:\source\robertodalmonte\Consilience\` consumes `Ckp.Core` and `Ckp.IO` via `ProjectReference`. Every breakage in sections 1, 2, 6 is called out with a migration note at the bottom of that section.

Legend — priority: P1 = must do this round, P2 = should do, P3 = nice to have.
Effort: S = ≤½ day, M = 1–2 days, L = 3+ days.

---

## 1. Architecture audit

### 1.1 Current state (from inspection)

**Project graph (actual `ProjectReference` edges, verified against all `.csproj`):**

```
Ckp.Core            (no refs)
Ckp.IO              → Core
Ckp.Signing         → Core, IO
Ckp.Transpiler      → Core, IO
Ckp.Epub            → Core, IO
Ckp.Transpiler.Cli  → Transpiler, IO
Ckp.Epub.Cli        → Epub, IO
Ckp.Tests           → Core, IO, Signing, Transpiler, Epub
```

No wrong-direction project edges. The CLIs do not depend on other CLIs. The one surprise is that `Ckp.Transpiler.Cli` and `Ckp.Epub.Cli` both take a direct reference to `Ckp.IO` (for `CkpPackageWriter`) instead of going through their respective lib project — not a cycle, but it couples the CLIs to the serialization layer and will matter for section 2 (API surface).

**Namespace-level leakage scan** (`src\**\*.cs`, looking for BCL IO / crypto in `Ckp.Core`):

| File | System namespace used | Verdict |
|---|---|---|
| `Core/Claims/CkpHash.cs` | `System.Security.Cryptography`, `System.Text` | **Borderline.** Content-addressing is genuinely a domain rule (spec §10.1, validation rule S2). Keeping a SHA-256 helper in Core is defensible because the hash format is part of the claim contract. The actual crypto primitive is a one-liner; it is not "IO leakage" in the usual sense. |
| `Core/Field/ConfidenceScoreCalculator.cs` | `System` only (`Math`, `DateTimeOffset`) | Pure. OK. |
| `Core/Field/TurbulenceDetector.cs` | `System` only | Pure. OK. |
| `Core/Manifest/PackageManifestConstruction.cs` | `System` (`Guid.CreateVersion7`, `DateTimeOffset.UtcNow`) | Factory uses wall clock and UUIDv7 — acceptable for a `CreateNew` factory, but not testable as written. See 1.4.2. |
| `Core/Validation/*` | none | Pure. OK. |

No `System.IO`, `System.IO.Compression`, or `System.Text.Json` anywhere in `Ckp.Core`. No `Ckp.IO` using in Core. **The P3 refactor held.**

**Ckp.IO inspection** — all stateful code (file/zip/JSON) lives here. Nothing in `Ckp.IO` is pure domain logic that belongs in Core. However:

- `Ckp.IO/Serialization/CkpJsonOptions.cs` is `internal`. Good. That is the right visibility — the spec does not guarantee a particular `JsonSerializerOptions` surface.
- `Ckp.IO/Validation/Rules/*` (5 rule files) correctly implement `IExtractionRule` (declared in Core) with `public` visibility so tests can construct them. The interface lives in Core; the implementations in IO. Directionally correct.

**Ckp.Signing inspection** — clean. Depends on Core (for `PackageManifest`, `PackageSignature`, `SignatureSource`) and IO (for `CkpCanonicalJson`). This is the only outward-going edge: `Signing → IO`. Acceptable because canonical JSON is an IO-layer concern (serialization).

**Ckp.Transpiler inspection** — depends on `Ckp.IO` in the csproj but does not actually use any IO type in its code. Grep of the `Ckp.Transpiler` source tree for `Ckp.IO` symbols returns nothing. **Dead project reference.**

**Ckp.Epub inspection** — same pattern. `Ckp.Epub.csproj` references `Ckp.IO` but the source uses only `Ckp.Core` types. **Dead project reference.**

### 1.2 Work items

| ID | Item | Priority | Effort | Depends |
|---|---|---|---|---|
| A1 | Remove unused `<ProjectReference Include="..\Ckp.IO\..." />` from `Ckp.Transpiler.csproj` and `Ckp.Epub.csproj`. | P1 | S | — |
| A2 | Make `Ckp.Transpiler.Cli` and `Ckp.Epub.Cli` the *only* projects that reference `Ckp.IO` for writing packages. Wrap the `CkpPackageWriter` usage behind a thin façade method in each lib so the CLIs depend only on `Ckp.Transpiler` / `Ckp.Epub`. Result: `Transpiler.Cli → Transpiler` and `Epub.Cli → Epub` — no IO reference. | P2 | M | A1 |
| A3 | Document the architectural invariant in a new `docs/Architecture.md` (project layering, allowed edges, forbidden edges). Include a graph. Run an automated check in CI with a simple script that `dotnet msbuild -getProperty:ProjectReferences` per project and diffs against an allowlist. | P2 | M | A1 |
| A4 | Decision on `Core/Claims/CkpHash.cs`: keep in Core but rename internal usage hint. Alternative: move to IO and have Core expose only an abstract `IHashFunction`. **Recommend keep-in-Core** (simpler, single call site, matches spec) but document the choice explicitly in `docs/Architecture.md`. | P3 | S | A3 |
| A5 | Split the wall-clock / RNG dependency out of `PackageManifest.CreateNew`: accept an optional `TimeProvider` and a `Func<Guid>` (defaulting to `TimeProvider.System` and `Guid.CreateVersion7`). Enables deterministic tests of the transpilers without them having to set both fields afterwards. Keep the zero-arg overload for convenience. | P3 | S | — |

**Acceptance criteria:**

- A1: `dotnet build` passes with no warnings; `ckp-format.slnx` still builds end-to-end.
- A2: CLI `.csproj` files contain exactly one `ProjectReference` each.
- A3: `docs/Architecture.md` exists with a Mermaid graph; CI job fails if a new forbidden edge is introduced.
- A5: `PackageManifest.CreateNew(..., TimeProvider.System)` works; existing no-arg call sites compile unchanged.

### 1.3 Consilience migration

None. All changes are additive or internal. `Ckp.Core` public surface is unchanged. The `Ckp.Transpiler → Ckp.IO` dead reference removal is invisible to consumers because Consilience only references `Ckp.Core` and `Ckp.IO`.

---

## 2. API surface review

### 2.1 Current state

Every public type under `src\` was enumerated by reading each file and the one-time-only grep of cross-assembly consumption. The findings below are grouped by the action they imply, not by project.

**Genuinely public contract** (keep public):

| Type | Project | Why |
|---|---|---|
| `CkpPackage`, `PackageManifest`, `PackageClaim`, `Observable`, `Tier`, `TierHistoryEntry`, `PackageSignature`, `SignatureSource`, `ContentFingerprint`, `BookMetadata`, `T0RegistryEntry`, `T0RegistryReference` | Core | Round-trip data contract. Consumed by Consilience. |
| `CitationEntry`, `EvidenceReference`, `EvidenceReferenceType`, `EvidenceRelationship`, `EvidenceStrength` | Core | Ditto. |
| `AlignmentSummary`, `AlignmentType`, `BookAlignment`, `ClaimAlignment`, `TierMismatch`, `TierMismatchDirection` | Core | Round-trip data. |
| `ChapterInfo`, `DomainInfo`, `EditionInfo`, `GlossaryEntry` | Core | Round-trip data. |
| `MechanismEntry`, `PhenomenonEntry`, `CommentaryEntry` | Core | Enrichment round-trip. |
| `FieldPackage`, `CanonicalClaim`, `ClaimStatus`, `Attestation`, `ConfidenceScore`, `ConfidenceScoreCalculator`, `DivergentBranch`, `WeightBreakdown`, `TurbulenceDetector`, `TurbulenceFlag`, `TurbulenceDirection`, `AlignmentProposal`, `CompilationResult` | Core/Field | CKP 2.0 output types. |
| `CkpHash` | Core/Claims | Public helper; used by validator and could be used by consumers re-hashing statements. Genuine contract. |
| `CkpValidationReport`, `ClaimValidationDiagnostic`, `ClaimValidationSeverity`, `ExtractionPriority`, `ExtractionVocabulary`, `IExtractionRule`, `VocabularyBridge` | Core/Validation | Validator contract. |
| `PackageClaimConstruction` (extension static), `PackageManifestConstruction` (extension static) | Core | Factory; callers use `PackageClaim.CreateNew(...)`. Required for idiomatic construction. |
| `ICkpPackageReader`, `ICkpPackageWriter`, `CkpPackageReader`, `CkpPackageWriter`, `CkpCanonicalJson` | IO | Round-trip contract. Consilience uses all four. |
| `IAlignmentProposer`, `AlignmentProposer`, `IFieldPackageCompiler`, `FieldPackageCompiler`, `ICkpExtractionValidator`, `CkpExtractionValidator` | IO | Advertised feature surface. |
| `ICkpSigner`, `CkpSigner` | Signing | Advertised feature surface. |

**Public today, should be `internal` + `InternalsVisibleTo`** (over-publicized by P3.3):

| Type | Current project | Only consumed by | Recommendation |
|---|---|---|---|
| `DomainRegistry` | Ckp.Transpiler | `KnowledgeBaseTranspiler` (same asm), `DomainRegistryTests` | `internal`; add `[assembly: InternalsVisibleTo("Ckp.Tests")]`. |
| `BridgeRecord`, `ConnectionRecord`, `TransitionRecord`, `MechanismFile`, `ObservationFile`, `TraditionFile`, `KbClaim`, `KbEvidence`, `KbPackageMetadata`, `KbSignatures`, `KbTraditionSignatures` | Ckp.Transpiler | KB-JSON deserialization DTOs. Not re-exported from `KnowledgeBaseTranspiler.TranspileAsync`. | `internal`. These leak implementation detail (shape of the legacy KB JSON). |
| `EpubExtractor` | Ckp.Epub | `EpubTranspiler` (same asm), `EpubExtractorTests` | `internal` + `InternalsVisibleTo("Ckp.Tests")`. CLI does *not* call it. |
| `ChapterText` | Ckp.Epub | `EpubTranspiler.Chapters` property, CLI `Program.cs`, tests | **Keep public.** Returned from the CLI-facing API surface (`EpubTranspiler.Chapters` is iterated by the Epub CLI to write chapter files). Document this explicitly. |
| `BookMetadataArgs` | Ckp.Epub | `EpubTranspiler` ctor, CLI | **Keep public** — CLI argument DTO, consumed across assembly boundary. |
| `KnowledgeBaseTranspiler`, `EpubTranspiler` | Transpiler, Epub | CLIs + tests | **Keep public** — main entrypoints. |

**Stealth leaks to close** (public but unintentionally so):

- `EpubExtractor.StripHtml` is public; only invoked from inside `EpubExtractor`. Make private.
- `KnowledgeBaseTranspiler.ParsePredictedMeasurement` is public; only invoked from inside `KnowledgeBaseTranspiler`. Tests cover it directly (`PredictedMeasurementParserTests.cs`). Either mark `internal` + `InternalsVisibleTo` or move to a dedicated helper class.
- `CkpPackageWriter.DeterministicEpoch` is already `internal`. Good.

### 2.2 Work items

| ID | Item | Priority | Effort | Depends |
|---|---|---|---|---|
| B1 | Add `[assembly: InternalsVisibleTo("Ckp.Tests")]` to `Ckp.Transpiler` and `Ckp.Epub`. | P1 | S | — |
| B2 | Internalize: `DomainRegistry`; all `Kb*`/`*File`/`*Record` DTOs in `Ckp.Transpiler`; `EpubExtractor`; `EpubExtractor.StripHtml`. | P1 | S | B1 |
| B3 | Decide on `ParsePredictedMeasurement` visibility: recommended → make internal, keep test coverage via `InternalsVisibleTo`. | P2 | S | B1 |
| B4 | Generate a public-API snapshot (e.g., `Microsoft.DotNet.ApiCompat` or a hand-maintained `api/*.txt`) checked into the repo. Any future PR that changes public API must update it. | P2 | M | B2 |
| B5 | Add XML doc `<remarks>` to every remaining public type explaining its intended consumer (library user, CLI only, test fixture) so future visibility decisions are not lost. | P3 | M | B2 |

**Acceptance criteria:**

- B1–B3: `dotnet build` still passes; no public members removed from what was advertised as "the CKP library surface" (Reader, Writer, Signer, Validator, Proposer, Compiler, all Core types).
- B4: `api/Ckp.Core.txt`, `api/Ckp.IO.txt`, `api/Ckp.Signing.txt` exist with the committed public API.

### 2.3 Consilience migration notes

All B1–B3 changes target `Ckp.Transpiler` and `Ckp.Epub`, which Consilience does **not** reference. **No migration needed.** Consilience's public surface from `Ckp.Core` and `Ckp.IO` is unchanged.

If in future the B1–B3 changes are extended to Core/IO (they should not be in this round), any narrowing must be flagged here with a before/after listing.

---

## 3. Test coverage

### 3.1 Current state

Test file inventory (counted by `[Fact]` + `[Theory]`, verified by reading):

| File | Approx count | Targets |
|---|---|---|
| `AlignmentProposerTests.cs` | 6 | IO/Alignment |
| `CkpExtractionValidatorTests.cs` | ~31 | IO/Validation (S*, SET*, PC*, SEM* rules) |
| `CkpRoundTripTests.cs` | ~11 | IO/Serialization (Reader+Writer) |
| `CkpSignerTests.cs` | 14 | Signing + CanonicalJson |
| `CkpValidationReportTests.cs` | 7 | Core/Validation |
| `ClaimAlignmentTests.cs` | 4 | Core/Alignment |
| `DomainRegistryTests.cs` | 5 | Transpiler (internal) |
| `EnrichmentRoundTripTests.cs` | 6 | IO enrichment |
| `EpubExtractorTests.cs` | 7 | Epub |
| `EpubTranspilerTests.cs` | 10 | Epub |
| `FieldPackageCompilerTests.cs` | 9 | IO/Field |
| `Field/CanonicalClaimTests.cs` | 8 | Core/Field |
| `Field/ConfidenceScoreCalculatorTests.cs` | 12 | Core/Field |
| `Field/TurbulenceDetectorTests.cs` | 8 | Core/Field |
| `GlossaryEntryTests.cs` | 2 | Core/Structure |
| `KnowledgeBaseTranspilerTests.cs` | ~23 | Transpiler |
| `PackageClaimTests.cs` | 7 | Core/Claims |
| `PackageManifestTests.cs` | 4 | Core/Manifest |
| `PredictedMeasurementParserTests.cs` | 10 | Transpiler |
| `SamplePackageGeneratorTests.cs` | 1 | Regenerates example |

**No measured branch coverage exists.** That is the first gap. The target is ≥85% branch on Core, IO, Signing.

**Adversarial gaps identified by reading the code:**

`CkpPackageReader.cs` (92 LOC) — only happy-path and missing-required-entry paths are covered today:

1. Malformed ZIP central directory → no test.
2. Truncated stream (stream ends mid-entry) → no test.
3. `manifest.json` present but contains `null` (deserializes to null) → handled by `ReadRequiredEntryAsync` but untested.
4. Entry path with `../` traversal (`alignment/external/../../evil.json`) → **no guard at all**; the loop at lines 32–41 filters by `StartsWith("alignment/external/")` and `EndsWith(".json")`, but does not normalize. An entry named `alignment/external/../../../etc/passwd.json` would pass the filter. No file is written, so the impact is limited to reading, but a crafted alignment path could still be surprising. Must test + harden.
5. Duplicate entries (ZIP allows same name twice; .NET returns the first). → No test.
6. `manifest.json` is 500 MB. → No memory bound.
7. Corrupt JSON in a required entry → currently throws `JsonException` from deep in `System.Text.Json`. Not wrapped. No test asserting the shape of the error.
8. Unknown manifest `formatVersion` (e.g., `"2.0"`) → **no check at all**; the reader happily hydrates a future-versioned manifest. Spec §15.4 says readers should reject unsupported versions.
9. Extra unknown top-level ZIP entries → silently ignored (OK, but should be documented).

`CkpPackageWriter.cs` — current tests verify round-trip; none verify byte-for-byte determinism between two independent writes of the same logical package:

10. Write same package twice → are the two streams byte-equal? `CkpRoundTripTests` does not assert this explicitly.
11. Identical packages with `IReadOnlyList` fields in different input order → does the writer sort them? Partially — alignments are sorted, manifest goes through canonical JSON, but `claims/claims.json` is emitted in insertion order. That is a determinism hole.
12. Compression level is `Optimal`. No test pins it.
13. Entry `LastWriteTime` pinned to 2000-01-01. No test reads the raw ZIP and asserts the stamp.

`CkpSigner.cs` — tests cover happy path, tampered data, wrong key, and re-signing. Gaps:

14. Invalid base64 in `signature.publicKey` or `signature.signature` → `Convert.FromBase64String` throws `FormatException`. Not caught, not tested. `VerifyManifest` should return `false`, not throw.
15. Public key of wrong length (not 32 bytes) → NSec throws. Same treatment needed.
16. Private key of wrong length → same.
17. Verify with `SignatureAlgorithm` mismatch (e.g., `manifest.Signature.Algorithm = "RSA"`) → **not checked**; the code hard-codes Ed25519 regardless of what the signature block says. That means if the algorithm field is modified and the rest matches, the signature still verifies. This is arguably a downgrade / confusion risk.
18. `ToString()` of `PackageSignature`, `PackageManifest`, or the tuple returned by `GenerateKeyPair` — none leak private key material today (record `ToString` prints field names; the tuple of `byte[]` prints as `System.Byte[]`), but no test pins this behaviour against future changes.

`CkpCanonicalJson.cs` — tested only indirectly through `CkpSignerTests`. Dedicated test file does not exist.

19. Deterministic for equal manifests — covered.
20. Lexicographic key order at root — covered.
21. Lexicographic ordering at **nested** levels (e.g., inside `book`, `contentFingerprint`, each `alignments[i]`) — **not covered**.
22. `SerializeForSigning` strips pre-existing signature and produces same bytes as signing an already-unsigned manifest — **not covered**.
23. Nested arrays preserve element order (arrays are not sorted) — not covered.

### 3.2 Work items

| ID | Item | Priority | Effort | Depends |
|---|---|---|---|---|
| T1 | Add coverage tooling: `dotnet-coverage` (cross-platform) or `coverlet.collector` (already referenced) → generate `cobertura` report → publish HTML. Script in `scripts/coverage.ps1`. Add CI step. | P1 | S | — |
| T2 | Write `CkpPackageReader` adversarial test file covering items 1–9 above. Name: `CkpPackageReaderAdversarialTests.cs`. Add a `MalformedZipBuilder` fixture helper. | P1 | M | T1 |
| T3 | Harden reader: reject `formatVersion` outside a known-supported set. Wrap alignment-path filter with `Path.GetFullPath`-style normalization + explicit rejection of entries whose normalized relative path escapes `alignment/external/`. Wrap `JsonException` from required entries in a `CkpFormatException` with a message that names the entry. | P1 | M | T2 |
| T4 | Write `CkpPackageWriter` determinism tests covering items 10–13: write same package twice via two separate writer instances, assert `SequenceEqual` on the two output byte arrays; write with input lists in different orders, assert same output; open output with `ZipArchive` and assert every entry's `LastWriteTime == DeterministicEpoch`. | P1 | M | — |
| T5 | Fix emergent determinism holes from T4: sort `claims` by `Id`, `citations` by `Ref`, `axiomRefs` by `Ref`, `domains` by `Name`, `chapters` by `ChapterNumber`, `editions` by `Edition`, enrichment lists by their natural key, before serializing. Update `CkpPackageWriter`. | P1 | S | T4 |
| T6 | Write `CkpCanonicalJson` dedicated test file covering items 19–23. Name: `CkpCanonicalJsonTests.cs`. | P2 | S | — |
| T7 | Write `CkpSignerSecurityTests.cs` covering items 14–18. Every `VerifyManifest` failure mode returns `false` (never throws). Add explicit `Algorithm == "Ed25519"` check in `CkpSigner.Verify`; reject otherwise. | P1 | M | — |
| T8 | Expand `Ckp.Signing` general coverage: key derivation determinism (same seed → same keypair, if exposed), mixed-source round-trips, signed package → modify a single byte of `claims/claims.json` → reader still hydrates but validator-initiated content hash check fails. (Requires wiring up an optional content check on read; see section 5.) | P2 | M | S1 |
| T9 | Run T1 and capture per-project branch coverage. Fill gaps to ≥85% on Core, IO, Signing. Smaller projects (Transpiler, Epub) — target ≥75% branch. Report in `docs/Refactoring/coverage-baseline.md`. | P1 | L | T1, T2, T4, T6, T7 |

**Acceptance criteria:**

- T1: `scripts/coverage.ps1` produces `TestResults/coverage/index.html`.
- T2: ≥9 new tests in `CkpPackageReaderAdversarialTests.cs`, every one of items 1–9 exercised.
- T3: Reader tests for 4 (path escape) assert the reader *rejects* the entry, does not silently include it.
- T4 + T5: Two independent writes of the same package are byte-identical for every MiniKb-scale fixture.
- T9: Coverage report shows Core ≥85%, IO ≥85%, Signing ≥85%, Transpiler ≥75%, Epub ≥75%, branch.

### 3.3 Consilience migration notes

T3 (reader hardening) and T5 (additional sort keys in writer) are the two items that affect behavior.

- T3: Consilience packages that already round-trip will continue to round-trip. Any Consilience package claiming `formatVersion != "1.0"` will now fail to read. None exist today.
- T5: Output bytes will change for any Consilience-generated package that did not happen to hand the writer claims in sorted order. The *content* is identical; only the ZIP entry byte stream differs. Consilience's tests that compare output byte-for-byte will need to regenerate their baselines. Flag this in the release note.

---

## 4. Performance

### 4.1 Current state

No benchmarks exist. Every profile below is static analysis of the hot paths.

**`KnowledgeBaseTranspiler.TranspileAsync` (~138 LOC):**

- Reads the full KB directory into memory: `Directory.GetFiles` per subfolder, then `JsonSerializer.DeserializeAsync<MechanismFile>` per file. All claims and evidence are accumulated into `List<(KbClaim, List<KbEvidence>)>` and `List<KbEvidence>` before any aggregation happens (`ReadAllSourcesAsync`). No streaming. Peak memory ≈ O(KB size).
- `GroupBy(...).ToDictionary(...)` on transitions / bridges / connections → three separate passes, each O(n). Fine.
- Line 86–96: `packageClaims.GroupBy(c => c.Domain).Select(...)`. Inside the `Select`, `g.Count(c => c.Tier == Tier.T1)`, `T2`, `T3`, `T4` → four linear scans per domain group. If N claims are spread across D domains, cost is ≈ 4·N (still O(N) total because the groups partition the set, but it's four passes). Trivially combinable into a single pass emitting a 4-tuple.
- Line 111–118: similar re-count of tiers for the manifest fingerprint — another four passes over the full claim list.
- No O(n²) loops in the path.

**`EpubTranspiler.TranspileAsync`:**

- Calls `EpubExtractor.ExtractAsync` which delegates to `VersOne.Epub` (reads the whole ePub into memory by design of that library).
- Recursive nav-tree walk in `CollectChapters` with `ref int chapterNumber` — depth bounded by TOC depth (typically ≤6). No stack overflow risk on realistic books. Recursion could be flattened to a `Stack<EpubNavigationItem>` but not a priority.
- `StripHtml` uses four compiled regexes per chapter. For very large chapters (≥1 MB HTML) this materializes several intermediate strings. Could be a `Span<char>`-based writer, but again not the hot path for realistic ePubs.

**`AlignmentProposer.Propose`:**

- Double nested loop over `source.Claims × target.Claims`: O(N·M) scoring. `ScorePair` calls `JaccardSimilarity` twice and `ObservableSimilarity` once; each allocates `HashSet<string>` per call. For 5k × 5k → 25M score calls, each allocating 2–3 hash sets. This is **the real hot path**.
- Fix: pre-build per-claim `HashSet<string>` for keywords and MeSH terms once, then share across scoring calls. Reduces allocations from ≈75M to ≈20k (one per claim).
- `candidates.OrderByDescending(...)` sorts the full candidate list. Fine.

**`FieldPackageCompiler.Compile`:**

- `BuildClaimIndex` is O(total claims) — correct.
- Per proposal: two dictionary lookups + attestation construction. O(P) where P = proposal count.
- Frontier pass: nested `foreach package / foreach claim` with `HashSet<string>` lookup. O(total claims).
- Acceptable.

**`CkpExtractionValidator`:**

- `ValidateSetLevel` uses `GroupBy` + `Count` for SET1/SET2 — O(N).
- SET3 builds an `allIds` HashSet once and looks up per evidence entry — O(N·E).
- `RunExtractionRules` — constructs the `rules` array *per Validate call* (line 215). Cheap but redundant; move to field or static.
- Regex compilation is done once (`RegexOptions.Compiled`) — good.

**`CkpPackageReader` / `CkpPackageWriter`:**

- Reader uses `JsonSerializer.DeserializeAsync` on the entry stream. No full-buffer-to-`byte[]` round-trip. Streaming. Good.
- Writer calls a `SerializeAsync` helper that `ToArray()`s a `MemoryStream` per entry. Every entry is buffered in full before being written to the archive. For large packages (hundreds of thousands of claims), `claims/claims.json` could be multi-GB. Should stream directly into `zipEntry.Open()`.
- Writer emits entries sequentially and awaits each. Could parallelize serialization, but then ordering becomes load-bearing; not worth it.

### 4.2 Work items

| ID | Item | Priority | Effort | Depends |
|---|---|---|---|---|
| P1 | Add a `Ckp.Benchmarks` project using BenchmarkDotNet 0.15. Targets: `CkpPackageReader`, `CkpPackageWriter`, `KnowledgeBaseTranspiler`, `EpubTranspiler`, `AlignmentProposer.Propose`, `FieldPackageCompiler.Compile`. Scale fixtures to 1×, 10×, 100×, 1000× MiniKb size. Include `[MemoryDiagnoser]`. Do not add to solution default build — separate profile. | P1 | M | — |
| P2 | Stream the writer: replace `SerializeAsync` → `byte[]` → `entryStream.WriteAsync(bytes)` with `JsonSerializer.SerializeAsync(entryStream, value, options)` directly. Preserves determinism (entry order and timestamps unchanged). | P1 | S | T4 |
| P3 | Collapse the four-pass tier-count in `KnowledgeBaseTranspiler` and in manifest fingerprint construction into a single-pass `foreach` producing a `(int t1, int t2, int t3, int t4)` tuple. | P2 | S | — |
| P4 | In `AlignmentProposer.Propose`, pre-tokenize and pre-set keywords, MeSH terms, and observables once per claim, cache in local arrays indexed by position. `ScorePair` becomes allocation-free in the inner loop. | P2 | M | P1 |
| P5 | Move the `IExtractionRule[]` construction in `CkpExtractionValidator.RunExtractionRules` out of the hot method into a field initialized in the constructor (with vocabulary captured). | P3 | S | — |
| P6 | Identify and fix any new O(n²) paths flagged by benchmarks at scale 100× and 1000×. | P2 | M | P1 |
| P7 | Report baseline + post-fix numbers in `docs/Refactoring/performance-baseline.md`. | P2 | S | P1–P6 |

**Acceptance criteria:**

- P1: `dotnet run -c Release --project src/Ckp.Benchmarks -- --filter *` produces a Markdown results file.
- P2: Writer peak memory at 1000× MiniKb drops by ≥50% vs baseline.
- P4: `AlignmentProposer.Propose` allocation rate at 1000× drops by ≥80%.

### 4.3 Consilience migration notes

All perf changes are internal. Reader/writer signatures unchanged. Output bytes unchanged (determinism preserved). **No migration.**

---

## 5. Signing security

### 5.1 Current state (from reading `CkpSigner.cs`, `ICkpSigner.cs`, `CkpCanonicalJson.cs`, `CkpSignerTests.cs`)

**Algorithm.** Ed25519 via NSec.Cryptography 25.4 (`SignatureAlgorithm.Ed25519`). Hardcoded. ✓

**What gets signed.** `CkpCanonicalJson.SerializeForSigning(manifest)` — which produces `manifest with { Signature = null }` → canonical JSON (RFC 8785-like) → UTF-8 bytes. So the signed scope is:

- `formatVersion`, `packageId`, `createdAt`, `book{*}`, `contentFingerprint{*}`, `t0Registry?{*}`, `alignments[]`
- Explicitly not `signature` itself (correct — it would be self-referential).

**What is NOT signed.** The actual `claims/claims.json`, `evidence/*`, `structure/*`, `alignment/external/*`, `enrichment/*`, `history/*`. The manifest carries `contentFingerprint` (claim counts, citation count) but no *content hash* over the claims. This means: an attacker who can rewrite a ZIP archive can swap claim statements — as long as the counts don't change (or they also rewrite the fingerprint), the signed manifest still verifies.

**This is the single most important finding of this audit.** Spec §10.2 says the signature is "over the package content" — the implementation does not match. Either the spec is wrong or the implementation is incomplete.

**Canonicalization.**

- `StringComparer.Ordinal` on property names. ✓
- Recursive at every object depth. ✓
- Arrays preserve element order (correct for JCS). ✓
- Numbers emitted via `GetRawText()` — safe because the manifest contains only integers and no floats.
- `JsonIgnoreCondition.WhenWritingNull` applied at the tree level. This means optional null fields are **omitted**, not serialized as `null`. That is consistent between sign and verify because both routes go through `SerializeForSigning`, which also has the ignore-when-null set on the tree options. So the canonical form is consistent — but the spec should document that omission, not null, is canonical.

**Signature field stripping.** `SignManifest` → `SerializeForSigning` → sets `Signature = null`. Hence the signed bytes are stable regardless of whether the input manifest was already signed. Tested.

**Verification path.**

- `VerifyManifest`: `if (manifest.Signature is null) return false;` → returns `false` cleanly for unsigned.
- Then calls `Verify(canonical, manifest.Signature)`.
- `Verify`: `Convert.FromBase64String(signature.PublicKey)` + `Convert.FromBase64String(signature.Signature)` — **neither is try-variant; bad base64 throws**. Bug.
- `PublicKey.Import` — throws if wrong key length.
- `Algorithm.Verify(...)` — returns bool. ✓ Timing-safe via NSec.
- **`signature.Algorithm` is never checked.** If a future package sets `Algorithm = "RSA"` but provides an Ed25519 key+signature, verification still succeeds. Downgrade/confusion risk.

**Key material leakage.** `PackageSignature` record's default `ToString()` prints field names with base64-encoded *public* key and signature — no private-key exposure. `GenerateKeyPair()` returns a tuple `(byte[], byte[])`; tuple `ToString()` prints `(System.Byte[], System.Byte[])` without contents. No exception currently catches and rethrows with key material. ✓

**Downgrade attacks.**

1. **Strip signature, read still accepts.** `CkpPackageReader` deserializes `PackageSignature?` as nullable. An attacker who strips the `signature` block can present an unsigned package; the reader accepts; a caller that forgets to call `VerifyManifest` is silently working with unsigned content. There is no "strict mode" reader that requires signatures.
2. **Truncate to empty `claims/claims.json`.** Reader returns `[]`. Validator SET5 catches the count mismatch — but only if the validator is run. Signing does not notice.
3. **Swap in a different `claims/claims.json` with same counts.** As discussed above — signing does not catch this because `claims/claims.json` bytes are not part of the signed payload.

### 5.2 Work items

| ID | Item | Priority | Effort | Depends |
|---|---|---|---|---|
| S1 | **Extend signed scope to cover package content.** Add a `PackageContentHash` field to `PackageManifest` (or a field inside `ContentFingerprint`) holding a SHA-256 merkle-like digest of all non-manifest ZIP entries in canonical order. Compute on write, verify on read. Signing already covers the manifest, so once this field is present, signing covers the whole package by transitivity. | P1 | L | — |
| S2 | Harden `CkpSigner.Verify`: check `signature.Algorithm == "Ed25519"` (case-insensitive, ordinal). Return `false` on mismatch. Wrap `Convert.FromBase64String` in try/catch → `false`. Wrap `PublicKey.Import` exceptions → `false`. Never throw from the verification path; throws are reserved for real programming errors (e.g., null arguments). | P1 | S | — |
| S3 | Add a "strict-read" mode to `CkpPackageReader`: `ReadAsync(stream, CkpReadOptions { RequireSignature = true, RequireContentHash = true, ExpectedPublicKey = ... })`. Default options remain permissive (same as today). | P2 | M | S1, S2 |
| S4 | Define and document the CKP signing threat model in `docs/Refactoring/SigningThreatModel.md`: who the adversary is (passive observer vs. intermediate tamperer vs. malicious publisher), what guarantees CKP provides (integrity of signed scope, non-repudiation with trusted key), and what it does **not** (key distribution, key revocation, confidentiality). Cross-reference spec §10. | P1 | M | — |
| S5 | Add negative-path tests: bad base64, wrong algorithm string, wrong key length, swapped signature and public key fields. All must return `false` from `VerifyManifest`, never throw. | P1 | S | S2 |
| S6 | Add tests verifying `ToString()` of `PackageSignature`, `PackageManifest`, and the result of `GenerateKeyPair` do not contain raw private-key bytes (construct a keypair with a known byte pattern and assert the pattern is absent). | P3 | S | — |
| S7 | Ensure `GenerateKeyPair`'s returned `byte[]` is suggested to be zeroed by callers after use. Document in XML doc. Consider returning `IMemoryOwner<byte>` or adopting `ReadOnlySpan<byte>` callbacks for long-lived keys in a future breaking change. | P3 | S | — |
| S8 | Run the reference spec § (`docs/CKP_FORMAT_SPEC.md` §10) line-by-line against `CkpSigner.cs` and list divergences in section 6. | P1 | S | — |

**Acceptance criteria:**

- S1: Signed package where any byte of `claims/claims.json` has been flipped post-signing → `VerifyManifest` returns false **and** the reader's content-hash check fails with a distinct error. Test: `Tamper_claims_post_sign_fails_verify`.
- S2: All items 14–17 from section 3.1 now return `false`, verified by dedicated tests.
- S4: Document exists, at least 4 named threats and their mitigation-or-out-of-scope status.

### 5.3 Consilience migration notes

S1 is **breaking**: packages written by the new code have an extra manifest field (`packageContentHash` or inside fingerprint). Old readers without this field tolerate it thanks to `JsonIgnoreCondition.WhenWritingNull` / unknown-property tolerance (default System.Text.Json **does** ignore unknown properties on deserialize). Old *signed* packages, when read by the new reader, will have a null content hash — treat as "legacy, unverifiable-at-content-level, signature still valid at manifest level." Consilience test fixtures that assert a specific manifest byte length will need regeneration.

S2, S3, S5–S7: internal or opt-in. No migration.

---

## 6. CKP format spec review

### 6.1 Current state — drift between `docs/CKP_FORMAT_SPEC.md` and code

**Spec §3 archive structure vs. writer output:**

- Spec lists `claims/by-tier/t1.json` … `t4.json` and `claims/by-domain/{name}.json`. **Writer does not emit these.** Reader does not read them. Either they are a spec aspiration (dead letter) or implementation is incomplete. Choose one.
- Spec lists `history/tier-changes.json`. **Writer emits it** (line 43). Reader **does not read it back**. (`CkpPackageReader` has no entry for `history/tier-changes.json`.) Round-trip is lossy — the data lives on `PackageClaim.TierHistory` and is re-emitted on each write, but is not round-tripped from the file. Not a bug (data is redundant with claims), but should be documented as "derived on write, ignored on read."
- Spec silent on `enrichment/commentary/{publisher,community}.json`. Writer emits, reader reads. Need a spec line.

**Spec §4 manifest example vs. canonical JSON output:**

- Spec example JSON at §4 lists keys in the order `formatVersion, packageId, createdAt, signature, book, contentFingerprint, t0Registry, alignments`. Canonical JSON output emits them in `alignments, book, contentFingerprint, createdAt, formatVersion, packageId, signature, t0Registry` order. Spec example is illustrative only — human-readable JSON from the reader does not canonicalize. Either add a note to §4 that the example is not canonical, or reorder it.
- Spec §4.1 says `t0Registry.constraintsReferenced` is in the manifest. Code has `T0RegistryReference(Version, Source?, ConstraintsReferenced)`. ✓
- Spec says `book.isbn` is optional. Code has `string? Isbn`. ✓
- Spec says `book.language` is optional. Code has `string Language` (non-nullable). **Drift.** Either make the field nullable or mark required in the spec.

**Spec §5 claim schema vs. `PackageClaim`:**

- Spec lists required/optional exactly matching `PackageClaim` record fields. ✓
- Spec ID regex: `^[a-z0-9]+-[a-z0-9]+\.[A-Z]{2,4}\.\d{3}$`. Validator uses the same. ✓
- Spec hash regex: `^sha256:[a-f0-9]{64}$`. Validator uses the same. ✓

**Spec §10 integrity vs. `CkpSigner.cs`:**

- §10.1 (hash): matches code.
- §10.2: "The manifest's `signature` block contains an Ed25519 signature over the package content." **Code signs manifest only, not content.** Drift — see section 5. This is the single largest spec↔code drift.
- §10.2 trust tier table is informational; code exposes `SignatureSource` enum matching the first four rows (Publisher, Author, Scholar, Community). No "AI-assisted" value. Either remove the row from the spec or add the enum value.

**Spec §12 field package vs. `FieldPackage.cs`:**

- Field ordering ✓. `FieldPackage` record fields match.
- Turbulence `tauBase` default in spec is 1.5 (§12.2 example), in code `TurbulenceDetector.DefaultTauBase` is 0.7. **Drift** — confirm the intended default and fix whichever is wrong.

**Missing spec topics (code has made a decision; spec is silent):**

- Canonical JSON algorithm details (RFC 8785 reference is in code comment, nowhere in spec).
- ZIP entry timestamps pinned to 2000-01-01. Spec does not mention determinism requirements.
- `enrichment/` directory "optional, written only when non-empty". Spec §15.5 mentions the directory but not the emit-only-when-non-empty rule. Reader handles both.
- Error contract: what exception types should readers raise? (Reader currently throws `InvalidOperationException` + `JsonException`. Spec silent.)
- Versioning/compat rules. Spec §15.4 says "readers should check this field and reject packages with unsupported versions" but code does not do this (see item T3 in section 3). Align.

### 6.2 Work items

| ID | Item | Priority | Effort | Depends |
|---|---|---|---|---|
| X1 | Decide `by-tier`/`by-domain` files: **recommend remove from spec** (writer correctness > aspirational files). Delete §3 references; keep a note that readers may build these views in memory. | P1 | S | — |
| X2 | Add spec §3.1 row: `history/tier-changes.json` — derived from claim tier history at write, ignored at read. | P1 | S | — |
| X3 | Spec §4 example: add a sentence "Property order in this example is illustrative only; the canonical form required by `signature` sorts keys lexicographically (RFC 8785)." Optionally reorder the example. | P2 | S | — |
| X4 | Reconcile `book.language` nullability: recommend **keep required** in code, fix spec to mark "Yes" instead of "No". Matches transpiler default of `"en"`. | P1 | S | — |
| X5 | Spec §10.2: rewrite to match code ("the manifest, including content fingerprint counts and content hash"). Add a sub-section 10.2.1 describing the content-hash construction (see work item S1). | P1 | M | S1 |
| X6 | Reconcile `TurbulenceTauBase` default: audit tests to determine which constant is load-bearing (likely 0.7 as implemented); fix spec §12.2 example to show `0.7`. | P1 | S | — |
| X7 | Add spec §3.2 "Determinism": state that entry order is sorted lexicographically, timestamps pinned, no fields with floats in the manifest, canonical JSON per RFC 8785. | P1 | S | — |
| X8 | Add spec §16 (new) "Error handling contract": enumerate the exception types and conditions a conformant reader raises (missing required entry, malformed archive, unknown format version, content hash mismatch, signature invalid). Align `CkpPackageReader` with it. | P2 | M | T3 |
| X9 | Add spec §15.6 "Enrichment emission rule": enrichment files written only when non-empty, reader tolerates missing. | P1 | S | — |
| X10 | `SignatureSource`: add `AiAssisted` enum value or remove "AI-assisted" row from §10.2 table. Recommend **remove** (ambiguous trust tier, not signed today). | P2 | S | — |
| X11 | Bump spec version to 1.1 after all X-items land. Keep `formatVersion` at `"1.0"` on wire until a genuine breaking change. Section "Changelog" at end of spec. | P2 | S | X1–X10 |

**Acceptance criteria:**

- X1–X10: Grep of `docs/CKP_FORMAT_SPEC.md` matches the actual behavior of `CkpPackageReader`/`CkpPackageWriter`/`CkpSigner` — no remaining drift.
- X11: Spec header shows "Version: 1.1, supersedes 1.0".

### 6.3 Consilience migration notes

Spec-only changes (X1–X4, X7, X9–X11) are documentation. No code impact.

X5 is linked to S1 (content hash) — same migration as section 5.

X6 changes no code; only changes the printed default in a spec example. No impact.

X8 formalizes existing reader error behavior; no impact unless reader's error types change, in which case flag here.

---

## 7. Documentation

### 7.1 Current state

- `README.md` already covers the library/CLI split, project layout, and per-CLI usage. **Already up to date post-P3.** Two gaps:
  - MiniKb fixture is unmentioned — should at least reference it in a "Testing" section.
  - "Consuming Ckp.Core as a library" section missing. Today a third party has to read the test suite to learn the pattern.
- `examples/README.md` describes `sample-biomechanics.ckp` correctly.
- `docs/PLAN_CODE_TO_CKP_TRANSPILER.md` is the April-15 design document, retained as historical. Still accurate.
- XML docs on public types in Core/IO/Signing are comprehensive (spot check of 10 types was 10/10 with `<summary>`). The gap is on **internal-by-default** types after B2 — those lose public XML docs but should gain remarks noting intended consumer.
- No per-project `README.md` inside `src/*`. Not required, but would help discoverability when the repo is published as NuGet packages.
- No `CHANGELOG.md`.
- No CI badge, no build status indicator.

### 7.2 Work items

| ID | Item | Priority | Effort | Depends |
|---|---|---|---|---|
| D1 | README: add a "Testing" section mentioning MiniKb fixture location (`tests/Ckp.Tests/TestData/MiniKb`), its purpose, and the rule that it must not be modified without explicit intent. | P1 | S | — |
| D2 | README: add "Consuming Ckp.Core / Ckp.IO as a library" section with a complete minimum example (read, verify, inspect claims). Link to Consilience as the flagship consumer. | P1 | S | — |
| D3 | README: add "Determinism guarantees" short section summarizing what the writer promises (lexicographic entry order, pinned LastWriteTime, canonical manifest). | P2 | S | X7 |
| D4 | Per-project XML `<Description>` in every `.csproj` so future NuGet packaging has meaningful descriptions without more work. | P2 | S | — |
| D5 | Add `docs/Architecture.md` (from work item A3). | P2 | M | A3 |
| D6 | Add `docs/Refactoring/SigningThreatModel.md` (from S4). | P1 | M | S4 |
| D7 | Add `docs/Refactoring/performance-baseline.md` (from P7) and `docs/Refactoring/coverage-baseline.md` (from T9). | P2 | S | P7, T9 |
| D8 | Add `CHANGELOG.md` at repo root; backfill the two existing commits (April 15 foundation, P1–P3 squash) and append a "Quality-raising pass" entry as items land. | P2 | S | — |
| D9 | Rewrite `docs/PLAN_CODE_TO_CKP_TRANSPILER.md` header to make clear it is historical (designed April 15, implemented and superseded). Prevents future readers from treating it as authoritative. | P3 | S | — |
| D10 | Add CLI usage docs in each CLI project's README or at top of `Program.cs` as a header comment. Today usage is discovered only by running with zero args. | P2 | S | — |
| D11 | Refresh XML doc `<remarks>` on types that will be internalized in B2 to explain the "public for test visibility only" intent; once internalized, the remarks serve the next developer. | P3 | S | B2 |

**Acceptance criteria:**

- D1, D2, D3: README has three new sections; every CLI and library mentioned has a usage snippet.
- D5: `docs/Architecture.md` exists with a Mermaid diagram and the allowed-edges rule.
- D6: `docs/Refactoring/SigningThreatModel.md` covers ≥4 named threats.
- D8: `CHANGELOG.md` at repo root, Keep-a-Changelog format.

### 7.3 Consilience migration notes

None — documentation only.

---

## Cross-axis dependency graph

```
A1 ──► A2
 │
 └──► A3 ──► A4, D5

B1 ──► B2 ──► B3, B4, B5, D11

T1 ──► T2 ──► T3 ──► (reader behavior change)
        │
        └──► T9
T4 ──► T5 ──► (writer byte change)
T6, T7, T8 ──► T9

P1 ──► P2, P3, P4, P5, P6 ──► P7 ──► D7
P2 depends on T4 (must not regress determinism).

S2 ──► S5
S1 ──► S3, X5
S4 ──► D6
S8 informs section 6.

X1…X10 ──► X11
X5 depends on S1.
X6 independent.
X7 depends on section 4 decisions around determinism being stable.

D1–D4 independent.
D5 depends on A3.
D6 depends on S4.
D7 depends on P7, T9.
D8 independent.
D11 depends on B2.
```

---

## Suggested execution order

The plan breaks into four phases; each phase produces a shippable commit.

### Phase 1 — Safety rails (no behavior change)

Order inside the phase is mostly parallel-safe; one PR each.

1. **A1** (remove dead project refs) — 10 minutes.
2. **B1 + B2 + B3** (visibility narrowing in Transpiler/Epub) — half a day.
3. **T1** (coverage tooling) — 1 hour.
4. **S8** (spec vs code drift inventory written up) — half a day; feeds section 6.

Gate: solution builds, all 205 existing tests pass, coverage report generated.

### Phase 2 — Harden and extend tests

5. **T2** (reader adversarial tests).
6. **T4** (writer determinism tests).
7. **T6** (CanonicalJson dedicated tests).
8. **T7 + S2 + S5** (signer hardening + negative-path tests). Ship together.
9. **T3 + T5** — behavior-changing fixes justified by failing tests from steps 5 and 6. Call out in release notes (Consilience re-baseline).

Gate: coverage meets section 3 thresholds (Core/IO/Signing ≥85% branch).

### Phase 3 — Signing, content hash, spec alignment

10. **S4** (threat model document). Informs S1 design.
11. **S1** (content hash over non-manifest entries). This is the largest item in the plan.
12. **S3** (strict-read options).
13. **X5** (spec §10.2 rewrite), **X6** (TauBase), **X4** (book.language), **X1**, **X2**, **X7**, **X9**, **X10**, **X8**, **X3**, **X11** (bump to 1.1). Bundle into a single spec PR.
14. **A5** (TimeProvider / Guid factory parameterization) — optional ride-along.

Gate: spec and code match line-by-line. `VerifyManifest` detects post-sign content tampering.

### Phase 4 — Performance, architecture polish, docs

15. **P1** (benchmarks project). Baseline numbers recorded.
16. **P2 + P3 + P4 + P5 + P6**.
17. **A2** (CLI reference tidy), **A3** (architecture doc), **A4** (CkpHash decision).
18. **D1, D2, D3, D4, D5, D6, D7, D8, D9, D10, D11**.

Gate: performance baseline improved per section 4 acceptance criteria; all docs shipped.

### Constraint check (end of each phase)

- Determinism: round-trip of MiniKb is byte-identical before and after any writer change in each phase. Automated by T4.
- MiniKb fixture: untouched (only reads, no writes).
- Consilience: `cd ..\Consilience && dotnet build && dotnet test` at the end of every phase. Any red is a blocker.

---

## Itemized breaking-change summary for Consilience

| Phase | Change | Consilience impact |
|---|---|---|
| 2 — T3 | Reader rejects unknown `formatVersion`. | None (Consilience is on 1.0). |
| 2 — T5 | Writer sorts claims/citations/domains/chapters/editions by stable key. | Output bytes change — re-baseline any Consilience golden-byte tests. Content is equivalent. |
| 3 — S1 | Manifest gains a `packageContentHash` field. | Writer emits; readers built on the old Core tolerate unknown fields. No compile break unless Consilience has its own manifest model (it does not; it uses `PackageManifest` from Core). Byte stream changes. |
| 3 — X4 | Spec change only; `book.language` stays non-nullable. | None. |
| 3 — A5 | `PackageManifest.CreateNew` gains optional `TimeProvider` parameter. | Source-compatible (default argument); no migration. |

All other phases are internal (no public-API or wire-format change).

---

End of plan. Ready for review.

---

## Addendum — review considerations (2026-04-22)

1. **A1 / A2 reconciliation.** A1 removes an unused `Ckp.IO` reference from `Ckp.Transpiler` and `Ckp.Epub`. A2 reinstates that dependency deliberately — as a thin write-façade (`TranspileAndWriteAsync` or equivalent) so the CLIs stop referencing `Ckp.IO` directly. Net architectural effect: the feature libraries become the sole seam between domain logic and the serialization layer. This is an intentional, documented coupling, not backsliding. Record the rationale in `docs/Architecture.md` (D5).
2. **P4 tokenizer allocations.** Beyond pre-tokenizing per-claim keyword/MeSH/observable sets, `MeasurementsOverlap` itself is worth an allocation pass: use `ArrayPool<string>` for the rented split buffer (or a `Span<char>` manual scan over the value string) so the hottest inner loop is allocation-free. Add a BenchmarkDotNet micro-benchmark alongside the Propose benchmark in P1.
3. **S1 content-hash construction.** Prefer a *sorted list of per-entry SHA-256 hashes, folded into a single SHA-256* (or a Merkle tree if depth becomes interesting). This keeps the wire format simple (one `packageContentHash` string) while preserving the option of emitting the full per-entry list in a future `packageContentManifest` section for partial verification without reading every byte. Design the field name and construction to allow that extension without a format bump.
4. **T5 determinism belt-and-braces.** In addition to sorting claims by `Id` (and the other stable keys enumerated in T5), explicitly set `JsonSerializerOptions.WriteIndented = false` in `CkpJsonOptions` and in every `JsonSerializer.SerializeAsync` call site in the writer, rather than relying on the default. This pins byte-determinism against a future runtime default change.

