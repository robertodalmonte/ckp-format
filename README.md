# CKP - Consilience Knowledge Package

An open format for structured, queryable, versionable scientific knowledge.

## The Problem

Scientific textbooks are PDF/print artifacts. Knowledge is trapped in prose -- you can't query a textbook for "all T2 claims in mechanotransduction," compare two textbooks' conclusions on the same topic, or track how a claim's evidence status changed across editions.

## What CKP Does

- Decomposes textbook knowledge into atomic, falsifiable claims
- Assigns epistemic tiers (T1-T4) reflecting evidence strength
- Attaches measurable observables (what you'd test to verify)
- Tracks tier history across editions (when did this become established?)
- Enables cross-book alignment (equivalent, contradictory, complementary claims)
- Content-addressable integrity via SHA-256 hashing
- Ed25519 digital signatures for provenance
- Field-agnostic: works for any scientific discipline

## Quick Example

A single CKP claim:

```json
{
  "id": "sample-3e.BIO.001",
  "statement": "Skeletal muscle generates force through actin-myosin cross-bridge cycling, requiring ATP hydrolysis for each power stroke.",
  "tier": "T1",
  "domain": "muscle-physiology",
  "observables": [
    { "measurement": "Force output", "unit": "N", "direction": "increase", "instrument": "Force transducer" }
  ],
  "hash": "sha256:..."
}
```

## Project Structure

```
src/
├── Ckp.Core/           -- Pure domain types (zero dependencies)
│   ├── Alignment/      -- Cross-book alignment types
│   ├── Claims/         -- PackageClaim, Observable, TierHistory
│   ├── Enrichment/     -- MechanismEntry, PhenomenonEntry, CommentaryEntry
│   ├── Evidence/       -- Citations, evidence references, strength
│   ├── Field/          -- CKP 2.0 field package types
│   ├── Manifest/       -- BookMetadata, ContentFingerprint, signatures, T0RegistryEntry
│   ├── Structure/      -- ChapterInfo, DomainInfo, EditionInfo, Glossary
│   └── Validation/     -- Extraction rules and diagnostics
├── Ckp.IO/             -- Reader, writer, validator, compiler
│   ├── Abstractions/   -- Interfaces
│   ├── Serialization/  -- CkpPackageReader, CkpPackageWriter
│   ├── Validation/     -- Extraction validator + rules
│   ├── Alignment/      -- AlignmentProposer
│   └── Field/          -- FieldPackageCompiler
├── Ckp.Signing/        -- Ed25519 signing (NSec.Cryptography)
├── Ckp.Transpiler/     -- KnowledgeBase JSON → .ckp (library)
├── Ckp.Transpiler.Cli/ -- `ckp-transpile` executable
├── Ckp.Epub/           -- ePub → .ckp skeleton + chapter text (library)
├── Ckp.Epub.Cli/       -- `ckp-epub` executable
└── Ckp.Benchmarks/     -- BenchmarkDotNet harness (not in shipping solution)
tests/
└── Ckp.Tests/          -- 284 tests (see "Testing" below)
docs/                   -- Format spec, architecture, refactoring plan
examples/               -- Sample .ckp packages
```

See [`docs/Architecture.md`](docs/Architecture.md) for the layering invariant
(Core → IO → Signing/Transpiler/Epub → CLIs) and the closed set of allowed
`ProjectReference` edges.

## Tools

### ckp-transpile (Ckp.Transpiler.Cli)

Converts a Consilience KnowledgeBase (JSON claim/evidence files) into a `.ckp` package.

```bash
dotnet run --project src/Ckp.Transpiler.Cli -- <knowledgebase-path> <output.ckp>
```

### ckp-epub (Ckp.Epub.Cli)

Extracts the chapter structure and text from an `.epub` file and produces a CKP skeleton package -- full book metadata, chapter index, zero claims (ready for downstream enrichment). Chapter text is stored as supplementary `.txt` files inside the ZIP archive.

```bash
dotnet run --project src/Ckp.Epub.Cli -- <book.epub> <output.ckp> --key my-textbook-3e [--title "..."] [--authors "..."] [--edition N] [--year YYYY]
```

## Consuming Ckp.Core and Ckp.IO as a library

The shipping NuGet-ready assemblies are `Ckp.Core` (pure domain types, zero
references) and `Ckp.IO` (reader, writer, validator, alignment proposer, field
compiler). Consumers that only need to inspect a hydrated package need `Ckp.Core`
alone; consumers that read or write `.ckp` files also add `Ckp.IO`. Signing is
optional (`Ckp.Signing`, opt-in by reference).

Minimum read + verify example:

```csharp
using Ckp.Core;
using Ckp.IO;
using Ckp.Signing;  // optional, only if you want to verify signatures

var reader = new CkpPackageReader();
var signer = new CkpSigner();

await using var stream = File.OpenRead("example.ckp");

// Permissive read (default). Accepts unsigned packages; ignores unknown entries.
CkpPackage package = await reader.ReadAsync(stream);

// Strict read: reject if the package is unsigned, if the signed-scope content
// hash doesn't match, or if the declared formatVersion is unsupported.
var strict = new CkpReadOptions
{
    RequireSignature = true,
    RequireContentHash = true,
    SignatureVerifier = signer.VerifyManifest
};
stream.Position = 0;
CkpPackage verified = await reader.ReadAsync(stream, strict);

foreach (var claim in verified.Claims.Where(c => c.Tier == Tier.T1))
{
    Console.WriteLine($"{claim.Id}  {claim.Statement}");
}
```

The reverse direction — `CkpPackageWriter` — takes a `CkpPackage` and writes a
byte-deterministic ZIP. See the "Determinism guarantees" section below for what
that means on the wire.

The flagship external consumer is
[Consilience](https://github.com/robertodalmonte/Consilience) — it references
`Ckp.Core` and `Ckp.IO` via `ProjectReference` and exercises every public type.
Treat its test suite as the canonical usage example.

## Determinism guarantees

`CkpPackageWriter` promises: **given the same logical `CkpPackage`, two
independent writes produce byte-identical archives.** The writer enforces this
with:

- **Lexicographic ZIP entry order.** `PackageEntrySerializer.PlanEntries` builds
  one sorted list of `(name, writer-closure)` pairs; both the content-hash fold
  and the archive walk iterate it in the same order.
- **Per-list natural-key sort.** Claims by `Id`, citations by `Ref`, axiom refs
  by `Ref`, chapters by `ChapterNumber`, editions by `Edition`, domains by
  `Name`, alignments by a stable composite key. Determinism does not rely on
  the caller handing in pre-sorted lists.
- **Pinned entry timestamps.** Every ZIP entry's `LastWriteTime` is set to
  `DeterministicEpoch` (2000-01-01 UTC), so wall-clock time never leaks into
  the archive bytes.
- **Canonical JSON for the manifest.** `CkpCanonicalJson` sorts property names
  lexicographically at every depth and emits a compact UTF-8 byte stream
  (RFC 8785-shaped). Arrays preserve element order.
- **`WriteIndented = false`** pinned on every serializer option, including the
  per-entry streaming writes, so a future default change in `System.Text.Json`
  cannot regress byte output.
- **`TimeProvider` + `Func<Guid>` injection** into `PackageManifest.CreateNew`
  (default: `TimeProvider.System` and `Guid.CreateVersion7`). Test fixtures and
  benchmarks pin these to constants to get deterministic `createdAt` and
  `packageId` values when needed.

Tampering is caught by two overlapping mechanisms: the Ed25519 signature over
the manifest (which includes `contentFingerprint` and the package-level content
hash), and the sorted-leaf SHA-256 content hash folded over every
non-manifest ZIP entry. See
[`docs/Refactoring/SigningThreatModel.md`](docs/Refactoring/SigningThreatModel.md)
for the threat model.

## Testing

Run the full suite:

```bash
dotnet test ckp-format.slnx -c Release
```

The canonical test fixture is
[`tests/Ckp.Tests/TestData/MiniKb`](tests/Ckp.Tests/TestData/MiniKb) — a tiny
but representative Consilience KnowledgeBase (mechanisms, integrations,
observations, traditions). It is used by `KnowledgeBaseTranspilerTests`,
`CkpRoundTripTests`, the alignment and field-package fixtures, and the golden
sample generator (`SamplePackageGeneratorTests` regenerates
`examples/sample-biomechanics.ckp`).

**Do not modify the MiniKb fixture without explicit intent.** Many golden-byte
and count assertions are pinned to it; a surprise edit will cascade into dozens
of red tests and silently invalidate the byte-determinism guarantee. If you
need to test a new scenario, add a new fixture folder alongside MiniKb or
construct the package programmatically in the test.

For performance and coverage baselines see
[`docs/Refactoring/performance-baseline.md`](docs/Refactoring/performance-baseline.md)
and
[`docs/Refactoring/coverage-baseline.md`](docs/Refactoring/coverage-baseline.md).

## The Tier System

| Tier | Name | Example |
|------|------|---------|
| T0 | Axioms | Conservation of energy (never assigned by a book) |
| T1 | Established | Cross-bridge cycling, Wolff's law |
| T2 | Supported | Fascial viscoelastic creep parameters |
| T3 | Speculative | ECM fluid flow as signaling pathway |
| T4 | Traditional | Martial arts bone conditioning |

## Format Specification

See [docs/CKP_FORMAT_SPEC.md](docs/CKP_FORMAT_SPEC.md).

## Requirements

.NET 10.0

## Author and AI Disclosure

CKP is designed and implemented by Roberto Dalmonte, drawing on 25+ years of dual-track experience in clinical dentistry/orthodontics and commercial software architecture (.NET/C#). The format design, domain modeling, and cryptographic signing architecture reflect this intersection of clinical domain knowledge and software engineering pragmatism.

The author's formal training does not extend to advanced information science or knowledge representation theory. Generative AI -- specifically Claude Opus 4.6, Gemini Pro, and Grok Plus -- was used extensively as interactive tutors, sounding boards, and pair-programming assistants throughout the design and implementation of CKP. The author assumes full and sole responsibility for the final architecture and all design decisions.

## License

Apache 2.0 -- see [LICENSE](LICENSE).
