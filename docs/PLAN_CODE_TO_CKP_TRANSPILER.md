# Plan: KnowledgeBase-to-CKP Transpiler

> **Historical document — do not treat as authoritative.**
>
> This is the April-15 design document that drove the initial
> `Ckp.Transpiler` implementation. The transpiler shipped, the library/CLI
> split landed on 2026-04-21, and subsequent changes (A2 facade, architecture
> layering, hardened reader/writer) have moved beyond what is described here.
> The architectural invariants now live in [`Architecture.md`](Architecture.md);
> the format contract lives in [`CKP_FORMAT_SPEC.md`](CKP_FORMAT_SPEC.md); the
> current executable is `ckp-transpile` at `src/Ckp.Transpiler.Cli`, invoked as
> `dotnet run --project src/Ckp.Transpiler.Cli -- <kb-path> <output.ckp>`.
>
> Retained because it records the reasoning behind the original domain-split
> (mechanisms / integrations / observations / traditions) and the
> one-claim-per-mechanism-file choice, both of which are still load-bearing in
> the current code. Anything else should be cross-checked against the live
> spec and architecture docs before acting on it.

## Goal

Transpile the Consilience KnowledgeBase (JSON files encoding scientific claims, evidence, and integration relationships) into a valid `.ckp` package via `CkpPackageWriter`.

## Status: IMPLEMENTED (April 2026 — see historical note above)

Console tool: `dotnet run --project src/Ckp.Transpiler.Cli -- <knowledgebase-path> <output.ckp>`

## Context

CKP is a general-purpose format for decomposing scientific knowledge into atomic, queryable, versionable claims. The Consilience KnowledgeBase stores claims across three source categories and three integration layers:

**Source categories:**
- `mechanisms/*.json` — one claim per file (T1 mechanisms with evidence and signatures)
- `traditions/*.json` — multiple claims per file (T4 traditional medicine observations)
- `observations/*.json` — multiple claims per file (T4 contested/preliminary observations)

**Integration layers:**
- `integrations/transitions.json` — tier progression history (claim moves from T3→T2→T1 over time)
- `integrations/bridges.json` — ancient-to-modern mechanism mappings
- `integrations/connections.json` — mechanism-to-mechanism relationship graph

**Current dataset:** 47 claims across 21 domains, 30 citations, 18 T1 + 29 T4.

## Architecture

### Project: `Ckp.Transpiler`

A console app under `src/Ckp.Transpiler/` in the ckp-format solution. References `Ckp.Core` for domain types and `Ckp.IO` for `CkpPackageWriter`. Listed under a `/tools/` solution folder.

### Pipeline

```
KnowledgeBase JSON → KnowledgeBaseTranspiler → CkpPackage → CkpPackageWriter → .ckp ZIP
```

No staging step — the KnowledgeBase already contains tier assignments, evidence, and integration metadata. The transpiler maps directly to the CKP domain model.

## Files

| File | Purpose |
|------|---------|
| `Ckp.Transpiler.csproj` | Console app, references Ckp.Core + Ckp.IO |
| `Program.cs` | Entry point, CLI argument handling, writes `.ckp` output |
| `KbClaim.cs` | JSON DTO for KB claims |
| `KbEvidence.cs` | JSON DTO for KB evidence entries |
| `KbPackageMetadata.cs` | JSON DTO for `package.json` metadata |
| `KbSignatures.cs` | JSON DTO for mechanism signatures |
| `KbTraditionSignatures.cs` | JSON DTO for tradition signatures |
| `MechanismFile.cs` | JSON DTO for mechanism source files |
| `TraditionFile.cs` | JSON DTO for tradition source files |
| `ObservationFile.cs` | JSON DTO for observation source files |
| `BridgeRecord.cs` | JSON DTO for ancient→modern bridge mappings |
| `ConnectionRecord.cs` | JSON DTO for mechanism→mechanism connections |
| `TransitionRecord.cs` | JSON DTO for tier transition history |
| `DomainRegistry.cs` | Claim ID parsing (`cl-ans-001` → `consilience-v1.ANS.001`) and domain code → name mapping |
| `KnowledgeBaseTranspiler.cs` | Core orchestrator: reads sources, builds `CkpPackage` |
| `Properties/AssemblyInfo.cs` | `InternalsVisibleTo` for test access |

## Mapping Rules

### Claim IDs

KB format: `cl-{domain}-{seq}` (e.g., `cl-ans-001`)
CKP format: `{book-key}.{DOMAIN}.{seq:D3}` (e.g., `consilience-v1.ANS.001`)

Book key: `consilience-v1`

### Tiers

Integer `1–4` → string `"T1"–"T4"`.

### Domains

The domain code is extracted from the claim ID and lowercased: `cl-ans-001` → domain `"ans"`. The transpiler is domain-agnostic — it does not interpret what domain codes mean. Any KnowledgeBase with `cl-{code}-{seq}` claim IDs will work regardless of topic.

### Evidence → Citations + EvidenceReference

Each KB evidence entry becomes:
- A `CitationEntry` (deduplicated by evidence ID) with `PMID:{pubMedId}` ref (or evidence ID as fallback when no PMID)
- An `EvidenceReference` on the claim with `Type=Citation`, `Relationship=Supports`, `Strength=Primary`

### Bridges → InternalRef

Each bridge adds an `EvidenceReference` on the ancient observation claim:
- `Type=InternalRef`, `Ref={modern-mechanism-ckp-id}`, `Relationship=Supports`

### Connections → InternalRef

Each connection adds an `EvidenceReference` on the source claim:
- `Type=InternalRef`, `Ref={target-ckp-id}`, `Relationship=Supports`

### Transitions → TierHistory

Tier transition records are grouped by claim ID and mapped to `TierHistoryEntry` with justification notes including the transition date.

### Observables

Claims with falsification criteria get a `falsification-test` observable. Mechanism `signatures.predictedMeasurements` are parsed into structured `Observable` records by extracting direction (Increased/Decreased/Altered → increase/decrease/change), measurement name, and instrument (from "measured by" / "using" patterns).

### Package Metadata

```json
{
  "key": "consilience-v1",
  "title": "Consilience Knowledge Base",
  "edition": 1,
  "authors": ["Roberto Dalmonte"],
  "publisher": "Consilience",
  "year": 2026,
  "language": "en"
}
```

## Tests

41 tests in `Ckp.Tests`:

**`KnowledgeBaseTranspilerTests.cs` (30 tests):**
- Claim count (47 total, 18 T1, 29 T4)
- All claim IDs match CKP regex `^[a-z0-9]+-[a-z0-9]+\.[A-Z]{2,4}\.\d{3}$`
- All claim IDs unique
- All hashes start with `sha256:` and are unique
- All tiers are valid (T1–T4)
- All claims have non-empty domain
- Domain index matches claims (count + tier breakdown)
- Manifest fingerprint matches actual counts
- Book metadata correctness
- Citations present with valid refs (PMID or evidence ID)
- Citations reference existing claims
- T1 mechanism claims have citation evidence
- Claims with transitions have tier history (ANS.001 has 2 entries)
- Bridge claims have internal refs (TCM.001 → FAS.001)
- Connection claims have internal refs (FAS.001 → MCT.001)
- All claims have observables
- Full write→read round-trip preserves claims, citations, domains, book key
- Round-trip preserves claim hashes and statements
- Round-trip preserves tier history

**`DomainRegistryTests.cs` (11 tests):**
- Domain code extraction from claim IDs
- Sequence number extraction
- CKP claim ID formatting
- Domain name mapping for known codes
- Error on invalid claim ID format

## Design Decisions

1. **Direct-to-CKP, no staging step.** The KnowledgeBase already has tier assignments and evidence — no manual annotation pass needed. This differs from the code-to-CKP workflow (which would need staging for code-derived claims lacking tier/evidence metadata).

2. **Hash computed via `PackageClaim.CreateNew()`.** SHA-256 of the statement, locked at transpilation time.

3. **Evidence entries without PMIDs use the evidence ID as the citation ref.** Some historical papers (Fukada & Yasuda, 1957) have no PubMed entry.

4. **Bridges and connections map to `InternalRef` evidence.** CKP's `EvidenceReference` with `Type=InternalRef` captures cross-claim relationships within the same package.

5. **Structured observables from predicted measurements.** The KB's `signatures.predictedMeasurements` are parsed into structured `Observable` records by extracting direction, measurement name, and instrument from free-text descriptions.

6. **Tradition keywords flow to claim Keywords.** `traditionSignatures.searchKeywords`, `physiologicalTerms`, and `practiceTerms` are mapped to the claim's `Keywords` array.

## Open Questions

- [ ] Should a DentalOne.Mouth code-to-CKP transpiler be added alongside this JSON transpiler, using the staging envelope pattern described in the original plan?
