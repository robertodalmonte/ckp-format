# CKP Format Specification

**Version:** 1.0  
**Date:** 2026-04-10  
**Status:** Living specification  
**Reference implementation:** This repository (`Ckp.Core`, `Ckp.IO`, `Ckp.Signing`)

---

## 1. Introduction

### 1.1 What is CKP?

CKP (Consilience Knowledge Package) is a structured, versioned, cryptographically signed format for representing scientific knowledge as a collection of falsifiable claims extracted from textbooks and reference works.

A `.ckp` file is a ZIP archive containing JSON documents that decompose a book into its atomic knowledge units -- individual claims -- each with a tier classification, an evidence trail, measurable observables, and a content-addressable hash.

### 1.2 The problem

Scientific knowledge in textbooks is trapped in prose. A paragraph in Textbook Delta's Physiology and a paragraph in Textbook Gamma's Fascial Anatomy may describe the same physiological mechanism using completely different vocabulary, assign it different levels of certainty, and cite non-overlapping literature. There is no way to:

- **Query** across books: "Which books make claims about trigeminal-autonomic coupling?"
- **Compare** tiers: "Does Textbook Delta consider this T1 (established) while Textbook Gamma considers it T2 (supported hypothesis)?"
- **Track** evolution: "When did this claim get promoted from T2 to T1, and in which edition?"
- **Detect** convergence: "Three books describe the same mechanism in three different vocabularies with zero bibliographic overlap."
- **Verify** integrity: "Has this claim been modified since the last alignment?"

CKP makes all of this computable.

### 1.3 What CKP is not

- Not a citation manager. Citations are evidence metadata on claims, not the primary concern.
- Not a document format. CKP does not store the book's prose, layout, or figures.
- Not a replacement for peer review. CKP structures what books already assert; it does not evaluate truth.

---

## 2. Design Principles

### 2.1 The claim is the atomic unit

Every piece of knowledge in a CKP package is a single falsifiable statement. Not a paragraph. Not a chapter summary. One claim, one assertion, one hash.

This mirrors how science actually works: hypotheses are tested one at a time. A textbook chapter may contain hundreds of distinct claims at different evidence levels, but traditional formats collapse them into undifferentiated prose.

### 2.2 Content-addressable integrity

Every claim is hashed (SHA-256 over the statement text). If a claim's statement changes, its hash changes, and every downstream alignment that references it breaks explicitly. There are no silent edits. This is not optional -- it is a structural rule (S1, S2) enforced by the validator.

### 2.3 Tiered epistemics

Claims are not all equal. A conservation law (T0) and a speculative cross-domain hypothesis (T3) should not be stored with the same metadata. The tier system (T0-T4) classifies claims by their evidence strength, and the tier history tracks how that classification evolves across editions.

### 2.4 Cross-book alignment

The same phenomenon described in two books with different vocabulary is a connection, not a coincidence. CKP's alignment structure makes these connections explicit and machine-queryable. A tier mismatch between aligned claims is a signal, not noise.

### 2.5 Field-agnostic design

CKP is not specific to medicine, orthodontics, or any single discipline. The tier system, evidence types, and alignment model apply to any field where textbooks make falsifiable claims backed by evidence. Domain-specific vocabularies are externalized into configurable JSON files, not hard-coded.

---

## 3. Archive Structure

A `.ckp` file is a standard ZIP archive with the following layout:

```
book.ckp
├── manifest.json
├── claims/
│   ├── claims.json
│   ├── by-tier/
│   │   ├── t1.json
│   │   ├── t2.json
│   │   ├── t3.json
│   │   └── t4.json
│   └── by-domain/
│       ├── {domain-name}.json
│       └── ...
├── evidence/
│   ├── citations.json
│   └── axiom-refs.json
├── structure/
│   ├── chapters.json
│   ├── domains.json
│   └── glossary.json
├── history/
│   ├── editions.json
│   └── tier-changes.json
├── enrichment/                          (optional, written only when non-empty)
│   ├── mechanisms.json
│   ├── phenomena.json
│   └── commentary/
│       ├── publisher.json
│       └── community.json
└── alignment/
    └── external/
        ├── {target-book-key}.json
        └── ...
```

> **Note:** Internal cross-references between claims are stored inline on each claim as `EvidenceReference` entries with `type = "InternalRef"`, not in a separate file. This keeps claim evidence self-contained and avoids duplication.

### 3.1 File descriptions

| File | Contents |
|------|----------|
| `manifest.json` | Book metadata, content fingerprint, Ed25519 signature, T0 registry reference, alignment summaries |
| `claims/claims.json` | Flat array of all `PackageClaim` objects |
| `claims/by-tier/t{N}.json` | Subset of claims at tier N (precomputed for fast lookup) |
| `claims/by-domain/{name}.json` | Subset of claims in the named domain |
| `evidence/citations.json` | All PMIDs/DOIs cited by claims, with metadata |
| `evidence/axiom-refs.json` | T0 axiom references used as constraints by claims |
| `structure/chapters.json` | Chapter index with claim counts and domains |
| `structure/domains.json` | Domain taxonomy used by this book |
| `structure/glossary.json` | Book terminology mapped to standard terms and cross-book equivalents |
| `history/editions.json` | Edition metadata (date, editor, ISBN) |
| `history/tier-changes.json` | All tier promotions/demotions across editions |
| `alignment/external/{key}.json` | Claim alignments to other books |

---

## 4. Manifest Schema

The manifest is the root document of a CKP package. It identifies the book, summarizes the content, and carries the cryptographic signature.

```json
{
  "formatVersion": "1.0",
  "packageId": "550e8400-e29b-41d4-a716-446655440000",
  "createdAt": "2026-04-08T14:30:00Z",
  "signature": {
    "algorithm": "Ed25519",
    "publicKey": "MCowBQYDK2VwAyEA...",
    "signature": "base64..."
  },
  "book": {
    "key": "delta-14e",
    "title": "Textbook Delta: Medical Physiology",
    "edition": 14,
    "authors": ["A. Delta"],
    "publisher": "Open Science Press",
    "year": 2020,
    "isbn": "978-0-000-00000-0",
    "language": "en-US",
    "domains": ["physiology", "autonomic-nervous-system", "cardiovascular"]
  },
  "contentFingerprint": {
    "algorithm": "SHA-256",
    "claimCount": 3847,
    "domainCount": 24,
    "t1Count": 1205,
    "t2Count": 1892,
    "t3Count": 614,
    "t4Count": 136,
    "citationCount": 8421
  },
  "t0Registry": {
    "version": "2026.1",
    "source": "https://t0-registry.org/v2026.1",
    "constraintsReferenced": 47
  },
  "alignments": [
    {
      "targetBook": "gamma-2e",
      "targetPackageId": "...",
      "alignedClaims": 342,
      "tierMismatches": 89
    }
  ]
}
```

### 4.1 Manifest field reference

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `formatVersion` | string | Yes | CKP format version. Currently `"1.0"`. |
| `packageId` | UUID | Yes | Unique identifier for this package instance. |
| `createdAt` | ISO 8601 | Yes | UTC timestamp of package creation. |
| `signature` | object | No | Ed25519 signature block. Null for unsigned/draft packages. |
| `signature.algorithm` | string | Yes | Must be `"Ed25519"`. |
| `signature.publicKey` | string | Yes | Base64-encoded public key. |
| `signature.signature` | string | Yes | Base64-encoded signature over the package content. |
| `book` | object | Yes | Book metadata. |
| `book.key` | string | Yes | Unique book key: `{short-name}-{edition}` (e.g., `"delta-14e"`). |
| `book.title` | string | Yes | Full book title. |
| `book.edition` | int | Yes | Edition number. |
| `book.authors` | string[] | Yes | Author names. |
| `book.publisher` | string | Yes | Publisher name. |
| `book.year` | int | Yes | Publication year. |
| `book.isbn` | string | No | ISBN. |
| `book.language` | string | No | BCP 47 language tag (e.g., `"en-US"`). |
| `book.domains` | string[] | Yes | Primary knowledge domains covered. |
| `contentFingerprint` | object | Yes | Statistical summary of package contents. |
| `t0Registry` | object | No | Reference to the T0 axiom registry version used. |
| `alignments` | array | Yes | Summaries of cross-book alignments (may be empty). |

---

## 5. The Claim Schema

The `PackageClaim` is the atomic unit of CKP. Each claim is a single falsifiable assertion with its evidence, observables, and audit trail.

### 5.1 JSON representation

```json
{
  "id": "delta-14e.ANS.047",
  "statement": "Baroreceptor activation via carotid sinus stretch increases parasympathetic outflow to the heart, reducing heart rate within one cardiac cycle.",
  "tier": "T1",
  "domain": "autonomic-nervous-system",
  "subDomain": "baroreceptor-reflex",
  "chapter": 18,
  "section": "Arterial Baroreceptor Reflex",
  "pageRange": "225-227",
  "keywords": ["baroreceptor", "parasympathetic", "vagal tone", "heart rate"],
  "meshTerms": ["D017704", "D001340"],
  "evidence": [
    {
      "type": "Citation",
      "ref": "PMID:19834602",
      "relationship": "Supports",
      "strength": "Primary",
      "note": null
    },
    {
      "type": "Axiom",
      "ref": "T0:BIO.002",
      "relationship": "ConstrainedBy",
      "strength": null,
      "note": "Conservation of electrochemical gradient"
    }
  ],
  "observables": [
    {
      "measurement": "Heart rate decrease",
      "unit": "bpm",
      "direction": "decrease",
      "latency": "<1 cardiac cycle",
      "instrument": "ECG"
    }
  ],
  "sinceEdition": 8,
  "tierHistory": [
    { "edition": 8,  "tier": "T2", "note": "Introduced as supported hypothesis" },
    { "edition": 10, "tier": "T1", "note": "Promoted after consensus review" }
  ],
  "hash": "sha256:a3f2e1b4c5d6..."
}
```

### 5.2 Field reference

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Book-scoped identifier. Format: `{book-key}.{DOMAIN-CODE}.{NNN}`. Must match `^[a-z0-9]+-[a-z0-9]+\.[A-Z]{2,4}\.\d{3}$`. |
| `statement` | string | Yes | One falsifiable sentence. Must be non-empty, non-whitespace. |
| `tier` | string | Yes | Current tier: `"T1"`, `"T2"`, `"T3"`, or `"T4"`. T0 is never assigned by a book. |
| `domain` | string | Yes | Primary knowledge domain (kebab-case). |
| `subDomain` | string | No | Specific sub-classification within the domain. |
| `chapter` | int | No | Chapter number in the source book. |
| `section` | string | No | Section title within the chapter. |
| `pageRange` | string | No | Page range in the source (e.g., `"225-227"`). |
| `keywords` | string[] | Yes | Free-text search keywords. May be empty. |
| `meshTerms` | string[] | Yes | MeSH descriptor identifiers. May be empty. |
| `evidence` | EvidenceReference[] | Yes | Citations, axiom constraints, and internal references. May be empty. |
| `observables` | Observable[] | Yes | Measurable predictions. May be empty. |
| `sinceEdition` | int | No | Edition in which this claim first appeared. |
| `tierHistory` | TierHistoryEntry[] | Yes | Edition-by-edition tier assignment trail. May be empty. |
| `hash` | string | Yes | SHA-256 content hash. Format: `sha256:{64 hex chars}`. |

### 5.3 Claim ID format

The claim ID encodes provenance directly:

```
delta-14e.ANS.047
└─────┬────┘ └┬┘ └┬┘
  book key  domain sequence
             code  number
```

- **Book key**: lowercase, hyphenated, includes edition suffix (e.g., `delta-14e`, `gamma-2e`).
- **Domain code**: 2-4 uppercase letters identifying the knowledge domain (e.g., `ANS`, `BIO`, `FAS`, `OCC`).
- **Sequence number**: 3-digit zero-padded integer, unique within the book-domain pair.

---

## 6. The Tier System

The tier system classifies claims by the strength and nature of their supporting evidence. Tiers are not quality judgments -- they are epistemic classifications.

### 6.1 Tier definitions

| Tier | Name | Definition | Assigned by |
|------|------|------------|-------------|
| **T0** | Axiom | Non-negotiable physical, chemical, or mathematical laws. Conservation of energy. Second law of thermodynamics. These are constraints, not claims. | Shared T0 registry only. Never assigned by a book. |
| **T1** | Established Mechanism | Reproducible, instrument-verified, published in multiple independent studies. The mechanism is understood and the evidence is strong enough that contradicting it requires extraordinary proof. | Book author/extractor |
| **T2** | Supported Hypothesis | Partial evidence from one or more studies. Plausible mechanism proposed. Awaiting independent confirmation or broader replication. The scientific community treats this as likely but not settled. | Book author/extractor |
| **T3** | Speculative Bridge | Cross-domain hypothesis connecting observations from different fields. Plausible, internally consistent, but not yet validated by direct experiment. Often the most interesting claims in a book. | Book author/extractor |
| **T4** | Ancient/Traditional Observation | Empirical practices from traditional systems (e.g., TCM, Ayurveda, osteopathic tradition) reframed as testable hypotheses. The observation may be centuries old; the framing as a falsifiable claim is new. | Book author/extractor |

### 6.2 Tier examples

| Tier | Example |
|------|---------|
| T0 | "Energy cannot be created or destroyed in an isolated system." (Conservation of energy -- referenced as axiom constraint, never assigned) |
| T1 | "Baroreceptor activation via carotid sinus stretch increases parasympathetic outflow to the heart, reducing heart rate within one cardiac cycle." |
| T2 | "Fascial fibroblast contractility modulates local tissue stiffness in response to sustained mechanical load over periods of 20-60 minutes." |
| T3 | "Trigeminal afferent activation through masticatory biomechanics may modulate autonomic tone via a trigeminal-vagal reflex arc." |
| T4 | "Stimulation of ST-6 (jiache) and ST-7 (xiaguan) points on the jaw reduces systemic tension and calms the heart." |

### 6.3 Tier assignment rules

- T0 is never assigned by a book. Books reference T0 axioms via `evidence[].type = "Axiom"` with `relationship = "ConstrainedBy"`.
- T1 and T2 claims must have at least one citation in their evidence array (validation rule PC1).
- A claim's tier may change across editions. The `tierHistory` array records every change with the edition number and an explanatory note.
- Tier assignment is the responsibility of the extractor (human or AI-assisted). The validator flags inconsistencies (e.g., T1 claims with hedging language) but does not override.

### 6.4 Tier history

The `tierHistory` array is the forensic record of how a claim's epistemic status has evolved:

```json
"tierHistory": [
  { "edition": 8,  "tier": "T2", "note": "Introduced as supported hypothesis" },
  { "edition": 10, "tier": "T1", "note": "Promoted after consensus review" },
  { "edition": 13, "tier": "T1", "note": "Reconfirmed with updated citations" }
]
```

A claim that bounces between tiers across editions (e.g., T2 -> T1 -> T2 -> T1) is a **turbulent** claim -- an epistemic signal that the field has not settled. CKP 2.0 field packages detect and flag this automatically.

---

## 7. Evidence References

Each claim carries an array of evidence references linking it to external citations, T0 axioms, or other claims in the same package.

### 7.1 Evidence reference schema

```json
{
  "type": "Citation",
  "ref": "PMID:19834602",
  "relationship": "Supports",
  "strength": "Primary",
  "note": null
}
```

### 7.2 Reference types

| Type | Ref format | Description |
|------|------------|-------------|
| `Citation` | PMID or DOI | A bibliographic citation to a published study. |
| `Axiom` | `T0:{DOMAIN}.{NNN}` | A reference to a T0 axiom in the shared registry. |
| `InternalRef` | Claim ID | A cross-reference to another claim in the same package. |

### 7.3 Relationships

| Relationship | Meaning |
|-------------|---------|
| `Supports` | The referenced evidence supports this claim. |
| `Contradicts` | The referenced evidence contradicts this claim. |
| `ConstrainedBy` | This claim is constrained by a T0 axiom. Used only with `type = "Axiom"`. |

### 7.4 Strength levels

Strength applies to `Citation` and `InternalRef` types. It is null for `Axiom` references.

| Strength | Meaning |
|----------|---------|
| `Primary` | Direct evidence demonstrating the claim. |
| `Confirmatory` | Independent replication or confirmation. |
| `Peripheral` | Indirect support. Related but not directly testing this claim. |

### 7.5 Why contradictory evidence is included

A claim may cite evidence that contradicts it. This is intentional. If a T2 claim has three supporting citations and one contradicting citation, that is information the alignment engine and human reviewers need. Omitting contradictory evidence would make the package dishonest.

---

## 8. Observables

An observable is a measurable prediction tied to a claim. It answers the question: "What would you measure, with what instrument, to test this claim?"

### 8.1 Observable schema

```json
{
  "measurement": "Heart rate decrease",
  "unit": "bpm",
  "direction": "decrease",
  "latency": "<1 cardiac cycle",
  "instrument": "ECG"
}
```

### 8.2 Field reference

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `measurement` | string | Yes | What is being measured. |
| `unit` | string | No | Unit of measurement (e.g., `"bpm"`, `"N/mm2"`, `"Hz"`). |
| `direction` | string | Yes | Expected direction of change: `"increase"`, `"decrease"`, `"stable"`, `"present"`, `"absent"`. |
| `latency` | string | No | Expected time to observe the effect (free-text, e.g., `"<1 cardiac cycle"`, `"20-60 minutes"`). |
| `instrument` | string | No | Measurement instrument (e.g., `"ECG"`, `"MRI"`, `"force gauge"`, `"histology"`). |

### 8.3 Why observables matter

Observables are what separate a claim from an opinion. A claim without an observable is unfalsifiable. The CKP validator flags mechanistic claims (priority P0) and quantitative claims (priority P1) that lack observables (validation rule PC2).

Observables also power cross-book alignment: two claims in different vocabularies that predict the same measurable outcome are likely describing the same phenomenon.

---

## 9. Cross-Book Alignment

Alignment is the mechanism by which CKP connects claims across books. It answers: "Does Book B say the same thing as Book A, in different words, at a different tier?"

### 9.1 Alignment file structure

Each alignment file in `alignment/external/` maps claims from the host book (source) to claims in another book (target):

```json
{
  "sourceBook": "delta-14e",
  "targetBook": "gamma-2e",
  "alignments": [
    {
      "sourceClaim": "delta-14e.ANS.047",
      "targetClaim": "gamma-2e.FAS.112",
      "type": "Equivalent",
      "confidence": 0.85,
      "mismatch": {
        "sourceTier": "T1",
        "targetTier": "T2",
        "direction": "SourceAhead"
      },
      "bridge": {
        "sourceTerms": ["baroreceptor stretch", "carotid sinus"],
        "targetTerms": ["fascial mechanoreceptor", "cervical fascia tension"],
        "sharedConcept": "mechanical deformation -> autonomic modulation"
      },
      "alignedBy": "consilience-auto",
      "reviewedBy": null,
      "note": null
    }
  ]
}
```

### 9.2 Alignment types

| Type | Definition | Signal |
|------|-----------|--------|
| `Equivalent` | Same phenomenon, same or different vocabulary. | Consensus (if tiers match) or disagreement (if tiers mismatch). |
| `Overlapping` | Partial overlap. One claim is broader or narrower than the other. | The broader claim may subsume the narrower, or one book is more granular. |
| `Contradictory` | Same phenomenon, opposite conclusions. | Active scientific disagreement. High-value for investigation. |
| `Complementary` | Different aspects of the same phenomenon, not contradictory. | Together they give a fuller picture. Neither alone is complete. |
| `Unmatched` | No equivalent claim exists in the target book. | A gap. The source book covers something the target does not. |

### 9.3 Tier mismatch as signal

When two aligned claims have different tiers, the mismatch is explicitly recorded:

| Direction | Meaning |
|-----------|---------|
| `Same` | Both books assign the same tier. Strong consensus. |
| `SourceAhead` | Source book considers the claim more established (higher tier). |
| `TargetAhead` | Target book considers the claim more established (higher tier). |

A claim at T1 in three books and T3 in one is not a data error. It is one of two things: (a) the outlier book is behind the field, or (b) the outlier book knows something the others do not. Both possibilities are worth investigating. The tier mismatch makes this investigation possible.

### 9.4 Vocabulary bridge

The `bridge` object captures how the two books name the same concept differently:

```json
{
  "sourceTerms": ["baroreceptor stretch", "carotid sinus"],
  "targetTerms": ["fascial mechanoreceptor", "cervical fascia tension"],
  "sharedConcept": "mechanical deformation -> autonomic modulation"
}
```

Vocabulary fragmentation -- the same phenomenon getting different names in different fields -- is one of the primary obstacles to cross-disciplinary knowledge synthesis. The vocabulary bridge makes fragmentation computable rather than invisible.

---

## 10. Content Integrity

### 10.1 Claim hashing (SHA-256)

Every claim has a `hash` field containing the SHA-256 hash of its `statement` text.

**Format:** `sha256:{64 lowercase hexadecimal characters}`

**Validation rules:**
- **S1 (Hash format):** The hash must match `^sha256:[a-f0-9]{64}$`. No placeholders, no truncation.
- **S2 (Hash integrity):** The SHA-256 recomputed from the `statement` field must match the stored `hash`.

If a statement is modified and the hash is not updated, S2 fails and the package is rejected. This guarantees that any claim referenced by an alignment is exactly the claim that was aligned -- not a silently edited version.

### 10.2 Package signing (Ed25519)

The manifest's `signature` block contains an Ed25519 signature over the package content. Signing is optional (draft packages may be unsigned), but signed packages carry higher trust.

**Trust hierarchy:**

| Source | Signature | Trust level |
|--------|-----------|-------------|
| Publisher | Publisher Ed25519 key | Authoritative |
| Author | Author Ed25519 key | Authoritative |
| Scholar | Institution key + personal key | Reviewed |
| Community | Personal key only | Contributory |
| AI-assisted | Unsigned until human review | Draft |

### 10.3 Content fingerprint

The `contentFingerprint` in the manifest provides a statistical summary for quick integrity checks without reading every claim:

| Field | Description |
|-------|-------------|
| `algorithm` | Hash algorithm used (`"SHA-256"`) |
| `claimCount` | Total number of claims |
| `domainCount` | Number of distinct domains |
| `t1Count` | Count of T1 claims |
| `t2Count` | Count of T2 claims |
| `t3Count` | Count of T3 claims |
| `t4Count` | Count of T4 claims |
| `citationCount` | Total citations across all claims |

Validation rule SET5 verifies that the fingerprint counts match the actual package content.

---

## 11. Validation Rules

The CKP validator enforces four categories of rules. A package with any Error-level diagnostic is rejected. Warnings require human review. Notices are advisory.

### 11.1 Structural rules (Error)

These enforce the physical integrity of each claim. Failure means broken data.

| Rule | Name | Check |
|------|------|-------|
| S1 | Hash format | `hash` matches `^sha256:[a-f0-9]{64}$` |
| S2 | Hash integrity | Recomputed SHA-256 of `statement` matches stored `hash` |
| S3 | Valid tier | `tier` is one of `T1`, `T2`, `T3`, `T4` |
| S4 | ID format | `id` matches `^[a-z0-9]+-[a-z0-9]+\.[A-Z]{2,4}\.\d{3}$` |
| S5 | Statement present | `statement` is non-null, non-empty, non-whitespace |
| S6 | Domain present | `domain` is non-null, non-empty, non-whitespace |

### 11.2 Set-level rules (Error / Warning)

These enforce invariants across the entire claims array.

| Rule | Name | Check | Severity |
|------|------|-------|----------|
| SET1 | Unique IDs | No duplicate `id` values in the package | Error |
| SET2 | Unique hashes | No duplicate `hash` values (implies duplicate statements) | Error |
| SET3 | Internal refs resolve | Every `InternalRef` evidence entry references an `id` that exists in the same package | Error |
| SET4 | Citation consistency | Every `Citation` evidence `ref` appears in the package's citations array | Warning |
| SET5 | Manifest counts | `contentFingerprint` counts match actual claim distribution | Error |

### 11.3 Priority-conditional rules (Error)

These fire only when extraction priority metadata is present on a claim.

| Rule | Name | Check |
|------|------|-------|
| PC1 | Citation required for T1/T2 | T1 or T2 claims must have at least one `Citation` evidence entry |
| PC2 | Observable required for P0/P1 | Mechanistic or Quantitative priority claims must have at least one observable |

### 11.4 Semantic rules (Warning / Notice)

These detect likely quality problems requiring human judgment.

| Rule | Name | Check | Severity |
|------|------|-------|----------|
| SEM1 | Tier-language mismatch | T1 claim contains hedging language ("appears to", "may", "preliminary evidence"). Suggests the claim is actually T2. | Warning |
| SEM2 | Compound statement | Statement contains patterns suggesting multiple independent assertions (semicolons, compound verb phrases). One claim, one assertion. | Warning |
| SEM3 | Unknown domain | Domain string not in the controlled vocabulary. | Warning |
| SEM4 | Empty observables on mechanistic claim | Claim uses molecular/pathway keywords but has zero observables. | Warning |
| SEM5 | Stale tier history | Latest tier history entry does not match the book's current edition. | Warning |
| SEM6 | Epistemic rigidity | T3/T4 claim with no hedging markers. A speculative or traditional claim stated as absolute fact. | Notice |

The hedging markers and domain vocabulary are externalized into JSON configuration files, making them field-configurable without code changes.

---

## 12. CKP 2.0: Field Packages

CKP 1.0 packages represent a single book's claims. CKP 2.0 introduces the **field package**: a compiled cross-book consensus for an entire scientific field.

### 12.1 What is a field package?

A field package is produced by the Alignment Engine from one or more CKP 1.0 source packages. It deduplicates claims across books, computes a weighted consensus tier, tracks attestation provenance, and flags turbulence where books disagree.

### 12.2 FieldPackage schema

```json
{
  "fieldId": "orthodontics",
  "version": "2026.4",
  "compiledAt": "2026-04-08T14:30:00Z",
  "sourcePackages": ["alpha-3e", "delta-14e", "gamma-2e"],
  "decayLambda": 0.058,
  "survivalAlpha": 0.1,
  "turbulenceTauBase": 1.5,
  "claims": [ ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `fieldId` | string | Field identifier (e.g., `"orthodontics"`, `"fascia-science"`). |
| `version` | string | Compilation version, incremented on each recompile. |
| `compiledAt` | ISO 8601 | UTC timestamp of this compilation. |
| `sourcePackages` | string[] | Book keys of all CKP 1.0 packages that contributed. |
| `decayLambda` | double | The decay constant used during compilation. |
| `survivalAlpha` | double | The survival bonus constant used during compilation. |
| `turbulenceTauBase` | double | The turbulence detection threshold used. |
| `claims` | CanonicalClaim[] | All canonical claims in this field package. |

### 12.3 Canonical claims

A canonical claim is the field-scoped, deduplicated version of a concept that may appear in multiple books.

```json
{
  "canonicalId": "ckp:ortho:biomech:fak-osteoclast-cascade",
  "status": "Converged",
  "statement": "FAK-mediated osteoclast activation cascade in response to orthodontic force.",
  "consensusTier": "T1",
  "confidence": {
    "finalValue": 0.82,
    "baseAuthoritySum": 2.0,
    "decayPenalty": 0.31,
    "survivalBonus": 0.08
  },
  "attestations": [
    {
      "bookId": "alpha-3e",
      "claimId": "alpha-3e.BIO.007",
      "tier": "T1",
      "publicationYear": 2019,
      "editionsSurvived": 3,
      "weight": 0.85,
      "note": null
    }
  ],
  "vocabularyMap": {
    "alpha-3e": "FAK-osteoclast pathway",
    "delta-14e": "mechanotransduction cascade"
  },
  "t0Constraints": ["T0:PHYS.001"],
  "turbulence": null,
  "branches": null
}
```

### 12.4 Canonical ID format

```
ckp:ortho:biomech:fak-osteoclast-cascade
└┬┘ └─┬─┘ └──┬──┘ └──────────┬──────────┘
 CKP  field  domain    phenomenon
```

The canonical ID is a semantic URN, not a book-scoped identifier. It identifies the concept across all books in the field.

### 12.5 Claim lifecycle (status)

| Status | Definition | Branches |
|--------|-----------|----------|
| `Frontier` | Single attestation. No cross-book corroboration yet. | null |
| `Converged` | Multiple attestations with general tier agreement. | null |
| `Divergent` | Multiple attestations with explicit contradiction. | Populated with `DivergentBranch` entries, one per contradictory position. |

### 12.6 Confidence scoring

The confidence score uses exponential decay with a survival bonus:

```
Weight = Authority_base * (1 + alpha * ln(editions_survived)) * e^(-lambda * age)
```

**Locked parameters:**

| Parameter | Default | Meaning |
|-----------|---------|---------|
| lambda | 0.058 | Decay rate. Half-life of approximately 12 years for medical/biological fields. |
| alpha | 0.1 | Survival bonus scaling. Rewards claims that persist across editions. |
| Authority_base | 1.0 | Base authority per book. Can be adjusted (e.g., Textbook Delta > a niche monograph). |

The `ConfidenceScore` record decomposes the final value into its components (base authority sum, decay penalty, survival bonus) so that reviewers can audit exactly why a claim scored what it did.

**Decay reference table** (Authority_base = 1.0, editions_survived = 1):

| Book age (years) | Weight |
|-------------------|--------|
| 0 | 1.000 |
| 4 | 0.793 |
| 7 | 0.666 |
| 12 (half-life) | 0.500 |
| 25 | 0.234 |
| 50 | 0.055 |

### 12.7 Turbulence detection

A `TurbulenceFlag` is raised when a recent, authoritative source diverges from the established consensus on a canonical claim. The flag records:

| Field | Description |
|-------|-------------|
| `triggeredByBookId` | The book that caused the divergence. |
| `direction` | Promotion, demotion, or contradiction. |
| `tierDelta` | Absolute tier gap (e.g., T1 to T3 = 2). |
| `note` | Human-readable explanation. |

Turbulence is a feature, not a bug. It means the field is moving. A canonical claim with zero turbulence across five books and ten years is settled science. A canonical claim with turbulence triggered by a 2025 publication is where the action is.

### 12.8 Divergent branches

When a canonical claim has `status = "Divergent"`, the `branches` array holds each contradictory position:

```json
{
  "branches": [
    {
      "position": "Osteoclast activation requires FAK signaling.",
      "tier": "T1",
      "attestations": [ /* books supporting this position */ ]
    },
    {
      "position": "Osteoclast activation can proceed via FAK-independent integrin signaling.",
      "tier": "T2",
      "attestations": [ /* books supporting this position */ ]
    }
  ]
}
```

Each branch carries its own tier and attestation list. The `consensusTier` on the parent claim is meaningless when status is `Divergent` -- the branches hold the real data.

---

## 13. T0 Axiom Registry

T0 axioms do not live in any book's CKP package. They are stored in a shared, versioned, cryptographically signed registry maintained by international standards bodies.

### 13.1 Registry entry format

```json
{
  "id": "T0:PHYS.001",
  "statement": "Energy cannot be created or destroyed in an isolated system.",
  "domain": "physics",
  "subDomain": "thermodynamics",
  "formalExpression": "dU = deltaQ - deltaW",
  "authority": "CODATA",
  "version": "2026.1",
  "signature": "Ed25519:base64...",
  "constrains": "Any claim asserting energy generation without a defined source"
}
```

### 13.2 How books reference T0

A claim references a T0 axiom through its evidence array:

```json
{ "type": "Axiom", "ref": "T0:PHYS.001", "relationship": "ConstrainedBy" }
```

This means: "This claim is consistent with conservation of energy." If a claim's observable contradicts a referenced axiom, it is a hard violation.

### 13.3 Registry governance

- **Versioned:** Yearly release with semver-style identifiers (`2026.1`, `2026.2`).
- **Immutable once released:** A published version is never modified. Corrections go into the next version.
- **Signed:** Each entry is cryptographically signed by the maintaining authority.
- **Referenced, not embedded:** CKP packages store the registry version in their manifest (`t0Registry.version`), not the axiom content.

---

## 14. Glossary and Vocabulary Mapping

### 14.1 Purpose

Different books use different terms for the same concept. A "fascial mechanoreceptor" in Textbook Gamma is a "stretch receptor" in Textbook Delta is a "periodontal mechanoreceptor" in Textbook Alpha. The glossary file makes this vocabulary fragmentation explicit and computable.

### 14.2 Glossary entry format

```json
{
  "bookTerm": "fascial mechanoreceptor",
  "standardTerm": "tissue mechanoreceptor",
  "meshTerm": "D008465",
  "equivalentsInOtherBooks": {
    "delta-14e": "stretch receptor",
    "alpha-4e": "periodontal mechanoreceptor"
  },
  "note": "Three books, three names, one transducer type."
}
```

The glossary is populated at packaging time by someone who understands both vocabularies. The fragmentation is declared, not discovered at query time.

---

## 15. Extensibility

### 15.1 Adding new domains

Domains are stored in `structure/domains.json` within the package and in an external controlled vocabulary file (`extraction-domains.json`). To add a new domain:

1. Add the domain string (kebab-case) to the controlled vocabulary JSON file.
2. Use the new domain string in claim extraction.
3. The validator (SEM3) will stop flagging it as unknown.

No schema changes or code changes required.

### 15.2 Adding new validation rules

Semantic validation rules implement the `IExtractionRule` interface (one method: `Validate(claim, priority) -> diagnostics`). To add a new rule:

1. Implement the interface.
2. Add the rule to the validator's rule array.

Structural (S*), set-level (SET*), and priority-conditional (PC*) rules require package-level context and live in the validator itself.

### 15.3 Adding new evidence types

The `EvidenceReferenceType` enum can be extended with new values. Existing consumers that do not recognize the new type will treat it as opaque metadata. The `ref` format for new types should be documented in this specification.

### 15.4 Format versioning

The `formatVersion` field in the manifest indicates the CKP format version. Readers should check this field and reject packages with unsupported versions. Minor version increments (e.g., 1.0 to 1.1) add optional fields. Major version increments (e.g., 1.x to 2.0) may change required fields or structure.

CKP 2.0 field packages use the `FieldPackage` schema, which is a distinct document type produced by compilation, not a replacement for CKP 1.0 book packages.

### 15.5 Enrichment directory

The `enrichment/` directory stores optional metadata that enhances claims without modifying them. All entries are written only when non-empty.

| File | Contents |
|------|----------|
| `enrichment/mechanisms.json` | Named mechanisms linking related claims with pathway terms and predicted measurements |
| `enrichment/phenomena.json` | Named phenomena clustering claims across domains |
| `enrichment/commentary/publisher.json` | Publisher annotations on claims |
| `enrichment/commentary/community.json` | Community annotations on claims |

Tools that do not understand enrichment data should ignore these entries.

---

## Appendix A: Complete Type Reference

### Enumerations

**AlignmentType**

| Value | Name | Description |
|-------|------|-------------|
| 0 | Equivalent | Same phenomenon, same or different vocabulary |
| 1 | Overlapping | Partial overlap |
| 2 | Contradictory | Same phenomenon, opposite conclusions |
| 3 | Complementary | Different aspects of the same phenomenon |
| 4 | Unmatched | No equivalent in the target book |

**TierMismatchDirection**

| Value | Name | Description |
|-------|------|-------------|
| 0 | Same | Both books agree on the tier |
| 1 | SourceAhead | Source book considers it more established |
| 2 | TargetAhead | Target book considers it more established |

**EvidenceReferenceType**

| Value | Name | Description |
|-------|------|-------------|
| 0 | Citation | Bibliographic citation (PMID, DOI) |
| 1 | Axiom | T0 axiom constraint from the shared registry |
| 2 | InternalRef | Cross-reference to another claim in the same package |

**EvidenceRelationship**

| Value | Name | Description |
|-------|------|-------------|
| 0 | Supports | Evidence supports the claim |
| 1 | Contradicts | Evidence contradicts the claim |
| 2 | ConstrainedBy | Claim is constrained by a T0 axiom |

**EvidenceStrength**

| Value | Name | Description |
|-------|------|-------------|
| 0 | Primary | Direct evidence demonstrating the claim |
| 1 | Confirmatory | Independent replication |
| 2 | Peripheral | Indirect support |

**ClaimStatus** (CKP 2.0)

| Value | Name | Description |
|-------|------|-------------|
| 0 | Frontier | Single attestation, no cross-book corroboration |
| 1 | Converged | Multiple attestations with general tier agreement |
| 2 | Divergent | Multiple attestations with explicit contradiction |

### Record types

**PackageClaim:** `Id, Statement, Tier, Domain, SubDomain?, Chapter?, Section?, PageRange?, Keywords[], MeshTerms[], Evidence[], Observables[], SinceEdition?, TierHistory[], Hash`

**EvidenceReference:** `Type, Ref, Relationship, Strength?, Note?`

**Observable:** `Measurement, Unit?, Direction, Latency?, Instrument?`

**TierHistoryEntry:** `Edition, Tier, Note?`

**PackageManifest:** `FormatVersion, PackageId, CreatedAt, Signature?, Book, ContentFingerprint, T0Registry?, Alignments[]`

**ContentFingerprint:** `Algorithm, ClaimCount, DomainCount, T1Count, T2Count, T3Count, T4Count, CitationCount`

**BookAlignment:** `SourceBook, TargetBook, Alignments[]`

**ClaimAlignment:** `SourceClaim, TargetClaim?, Type, Confidence?, Mismatch?, Bridge?, AlignedBy?, ReviewedBy?, Note?`

**MechanismEntry:** `Name, Description, ClaimIds[], PathwayTerms[], PredictedMeasurements[]`

**PhenomenonEntry:** `Name, Description, ClaimIds[], SharedConcept?`

**CommentaryEntry:** `ClaimId, Author, Text, CreatedAt`

**T0RegistryEntry:** `Id, Statement, Domain, SubDomain?, FormalExpression?, Authority, Version, Constrains?`

**FieldPackage:** `FieldId, Version, CompiledAt, SourcePackages[], Claims[], DecayLambda, SurvivalAlpha, TurbulenceTauBase`

**CanonicalClaim:** `CanonicalId, Status, Statement, ConsensusTier, Confidence, Attestations[], VocabularyMap{}, T0Constraints[], Turbulence?, Branches[]?`

**Attestation:** `BookId, ClaimId, Tier, PublicationYear, EditionsSurvived, Weight, Note?`

**ConfidenceScore:** `FinalValue, BaseAuthoritySum, DecayPenalty, SurvivalBonus`

**TurbulenceFlag:** `TriggeredByBookId, Direction, TierDelta, Note`

**DivergentBranch:** `Position, Tier, Attestations[]`
