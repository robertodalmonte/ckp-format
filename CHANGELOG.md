# Changelog

All notable changes to **ckp-format** are recorded here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/) loosely; versions map
to on-disk `formatVersion` (wire format) plus a repo tag for implementation
milestones. The wire format is currently `"1.0"` and the spec file itself is
stamped `1.1` (documentation alignment, no wire break).

## [Unreleased]

Quality-raising pass — reads as four phases inside a single pre-release cycle.
No wire-format break; `formatVersion` stays `"1.0"`.

### Tail — P2/P3 closure (2026-04-22)

- **B4** Public-API snapshot: `api/Ckp.Core.txt`, `api/Ckp.IO.txt`,
  `api/Ckp.Signing.txt` pin the shipping public surface. Regenerate with
  `pwsh ./scripts/api-snapshot.ps1`. `PublicApiSnapshotTests` fails on any
  drift so an unintentional API change cannot slip through review.
- **B5** Every public type in `Ckp.Core`, `Ckp.IO`, `Ckp.Signing`,
  `Ckp.Transpiler`, and `Ckp.Epub` now carries an XML `<remarks>` block
  naming its intended consumer (library user / CLI-facing DTO / internal with
  `InternalsVisibleTo`). Locks the visibility rationale into the source.
- **T8** `CkpSigningExtendedCoverageTests`: Ed25519 key-derivation
  determinism (same private key → same embedded public key; RFC 8032 signing
  is bit-identical across calls), every `SignatureSource` round-trips through
  write → read → verify, and single-byte tamper in `claims/claims.json` is
  caught by both the content-hash recompute and the S3 strict reader.
- **S6** `CkpSignerToStringLeakTests`: the `ToString()` of
  `PackageSignature`, `PackageManifest`, and the tuple returned by
  `GenerateKeyPair` is asserted to never contain the private key bytes in
  hex or base64 encodings.
- **S7** `GenerateKeyPair` and `SignManifest` XML docs now carry explicit
  lifetime guidance: callers should wrap the returned `PrivateKey` in a
  `try/finally` and call `CryptographicOperations.ZeroMemory(privateKey)`
  on exit. A future breaking change may swap the `byte[]` return for an
  `IMemoryOwner<byte>` or span callback; the `ReadOnlySpan<byte>` parameter
  shape on `Sign*` already supports that migration.

### Phase 4 — performance, architecture polish, docs (2026-04-22)

- **P1** Added `Ckp.Benchmarks` project (BenchmarkDotNet 0.15) with fixtures
  for reader/writer, alignment proposer, field compiler, and the transpilers.
  Not part of the shipping solution; run with
  `dotnet run -c Release --project src/Ckp.Benchmarks`. `--inprocess` switch
  sidesteps BDN's duplicate-project detection in git worktree setups.
- **P2** Writer now serializes each entry straight into the `ZipArchive` entry
  stream via `JsonSerializer.SerializeAsync` instead of buffering to a
  `byte[]` per entry. Peak allocations drop ~60% at 10 000-claim scale
  without touching byte output.
- **P3 + P5** Collapsed the four-pass tier-count (`GroupBy` + four
  `Count(... == Tier.X)`) in `KnowledgeBaseTranspiler` and the manifest
  fingerprint into a single-pass sweep. Hoisted the validator's
  `IExtractionRule[]` construction into a field so it is built once per
  validator instance, not per `Validate` call.
- **P4** `AlignmentProposer` now pre-tokenizes MeSH terms, keywords, and
  observables into case-insensitive `HashSet<string>` / `ObservableFeatures[]`
  once per claim before the N×M scoring loop. Jaccard similarity iterates the
  smaller set and probes the larger. Alignment allocations at 1000×1000 drop
  from tens of MB to 2.2 MB; wall time drops ~4×.
- **P6** Folded the remaining multi-pass tier and severity counts in
  `CkpValidationReport` and `CkpExtractionValidator` into single-pass switches.
- **P7** `docs/Refactoring/performance-baseline.md` records measured
  before/after numbers at 100, 500, 1000, and 10 000 claim scales.
- **A2** CLIs no longer reference `Ckp.IO` directly. `KnowledgeBaseTranspiler`
  and `EpubTranspiler` each gain a `TranspileAndWriteAsync(...)` extension
  that internalizes the writer step; `Ckp.Transpiler.Cli` and `Ckp.Epub.Cli`
  now depend only on their respective libraries.
- **A3 + A4** New `docs/Architecture.md` documents the three-tier layering
  (Core / Library / CLI), the closed set of allowed `ProjectReference` edges,
  and the rationale for the forbidden edges. Same doc records the decision to
  keep `CkpHash` in `Ckp.Core` (spec-pinned SHA-256, pure function, would
  force Core → IO if moved).
- **A5** `PackageManifest.CreateNew` gains optional `TimeProvider` and
  `Func<Guid>` parameters (defaults `TimeProvider.System` /
  `Guid.CreateVersion7`). Lets tests and benchmarks pin `createdAt` and
  `packageId` for determinism without post-construction mutation.
- **D1–D4, D7–D11** README gains Testing, Library-consumer, and Determinism
  sections; every `.csproj` has a `<Description>`; this `CHANGELOG.md` exists;
  `docs/PLAN_CODE_TO_CKP_TRANSPILER.md` is marked historical; each CLI's
  `Program.cs` carries a usage header; B2-internalized types have `<remarks>`
  explaining the visibility intent.

### Phase 3 — signing, content hash, spec alignment (2026-04-22)

- **S1** The manifest now carries a sorted-leaf SHA-256 `PackageContentHash`
  folded over every non-manifest ZIP entry. Because the manifest is signed,
  the signature now transitively covers package content. Tampering with any
  claim, citation, or alignment entry post-signing causes verification to
  fail. Construction: `sha256( Σ ( name_utf8 || 0x00 || sha256(leaf_bytes) || 0x0A ) )`.
- **S2 + T7 + S5** Hardened `CkpSigner.Verify`: checks
  `signature.Algorithm == "Ed25519"` (ordinal, case-insensitive); wraps
  `Convert.FromBase64String`, `PublicKey.Import`, and `Signature.Verify` in
  try/catch. Negative-path tests cover bad base64, wrong algorithm string,
  wrong key length, and swapped signature/publicKey fields. Every failure
  mode returns `false`; nothing throws from the verification path.
- **S3** `CkpPackageReader.ReadAsync` gains an optional `CkpReadOptions`
  parameter: strict-read mode can require a signature, require the content
  hash to match, and (via injected `SignatureVerifier` delegate) verify the
  signature without adding a `Ckp.IO → Ckp.Signing` reference cycle.
- **S4** `docs/Refactoring/SigningThreatModel.md` — lists adversary
  assumptions and what CKP does and does not defend against.
- **A5** (shipped in this phase, see Phase 4 note above; logically a
  Phase 3 ride-along.)
- **X1–X11** Full line-by-line reconciliation of `docs/CKP_FORMAT_SPEC.md`
  against the reference implementation. Removed the aspirational
  `claims/by-tier/*.json` and `claims/by-domain/*.json` entries; clarified
  `history/tier-changes.json` as write-emit / read-ignore; added §3.2
  "Determinism" and §15.6 "Enrichment emission"; fixed `book.language`
  nullability; reconciled `TurbulenceTauBase` default (0.7); rewrote §10.2
  around the content-hash construction (S1). Spec version bumped to 1.1;
  `formatVersion` wire value stays `"1.0"`.

### Phase 2 — harden and extend tests (2026-04-22)

- **T2** `CkpPackageReaderAdversarialTests.cs` + a `MalformedZipBuilder`
  fixture covering: malformed central directory, truncated stream, null
  manifest, path-traversal in alignment filenames, duplicate entries, huge
  manifest, corrupt JSON, unknown `formatVersion`, unknown top-level
  entries.
- **T3** Reader hardening driven by T2: reject `formatVersion` outside the
  known-supported set, normalize and reject alignment paths that escape
  `alignment/external/`, wrap `JsonException` from required entries in
  `CkpFormatException` with the entry name.
- **T4 + T5** Writer determinism tests: two independent writes of the same
  package produce byte-identical archives; shuffled input lists produce
  identical output; every ZIP entry's `LastWriteTime` equals the pinned
  2000-01-01 epoch. Emergent sort fixes: claims by `Id`, citations by
  `Ref`, axiom refs by `Ref`, chapters by `ChapterNumber`, editions by
  `Edition`, domains by `Name`; enrichment lists by natural key.
  `JsonSerializerOptions.WriteIndented = false` pinned at every call site.
- **T6** `CkpCanonicalJsonTests.cs` — nested-level lexicographic ordering,
  `SerializeForSigning` strip-signature stability, array element-order
  preservation.
- **T9** `docs/Refactoring/coverage-baseline.md` captures post-Phase 2
  branch coverage per project.

### Phase 1 — safety rails (2026-04-22)

- **A1** Removed the unused `Ckp.IO` `ProjectReference` from `Ckp.Transpiler`
  and `Ckp.Epub` (no code referenced an IO type from either library at that
  point; Phase 4's A2 reinstates the coupling deliberately as a write façade).
- **B1 + B2 + B3** Internalized `DomainRegistry` and the KB-JSON DTOs in
  `Ckp.Transpiler`; `EpubExtractor` in `Ckp.Epub`; private'd
  `EpubExtractor.StripHtml`. Tests stay green via
  `[assembly: InternalsVisibleTo("Ckp.Tests")]`.
- **T1** Coverage tooling wired up (`coverlet.collector` already referenced;
  added `scripts/coverage.ps1`).
- **S8** Initial spec-vs-code drift inventory recorded in
  `docs/Refactoring/SpecCodeDrift.md`; feeds the X1–X11 reconciliation in
  Phase 3.

## [0.2.0] — 2026-04-21

Initial library/CLI separation and byte-deterministic writer.

- Split into `Ckp.Core`, `Ckp.IO`, `Ckp.Signing`, `Ckp.Transpiler`,
  `Ckp.Epub`, and the two CLI projects (`Ckp.Transpiler.Cli`,
  `Ckp.Epub.Cli`).
- `CkpPackageWriter` writes ZIP entries in deterministic order with pinned
  timestamps; manifest goes through `CkpCanonicalJson`.
- Round-trip tests cover claims, citations, alignments, enrichment.
- `ckp-format.slnx` is the canonical solution file.

## [0.1.0] — 2026-04-10

Foundation.

- First working CKP library: core types, reader/writer, validator, signing,
  CLI tools. Single-assembly layout prior to the 0.2 split.

[Unreleased]: https://github.com/robertodalmonte/ckp-format/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/robertodalmonte/ckp-format/releases/tag/v0.2.0
[0.1.0]: https://github.com/robertodalmonte/ckp-format/releases/tag/v0.1.0
