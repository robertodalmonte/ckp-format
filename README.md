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
├── Ckp.Transpiler/     -- KnowledgeBase JSON → .ckp
├── Ckp.Epub/           -- ePub → .ckp (skeleton + chapter text)
tests/
└── Ckp.Tests/          -- 204 tests
docs/                   -- Format specification
examples/               -- Sample .ckp packages
```

## Tools

### Ckp.Transpiler

Converts a Consilience KnowledgeBase (JSON claim/evidence files) into a `.ckp` package.

```bash
dotnet run --project src/Ckp.Transpiler -- <knowledgebase-path> <output.ckp>
```

### Ckp.Epub

Extracts the chapter structure and text from an `.epub` file and produces a CKP skeleton package -- full book metadata, chapter index, zero claims (ready for downstream enrichment). Chapter text is stored as supplementary `.txt` files inside the ZIP archive.

```bash
dotnet run --project src/Ckp.Epub -- <book.epub> <output.ckp> --key my-textbook-3e [--title "..."] [--authors "..."] [--edition N] [--year YYYY]
```

## Getting Started

```csharp
var reader = new CkpPackageReader();
await using var stream = File.OpenRead("example.ckp");
var package = await reader.ReadAsync(stream);
// package.Claims, package.Citations, package.Alignments...
```

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
