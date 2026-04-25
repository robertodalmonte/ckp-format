# Spec ↔ Code Drift Inventory

**Source spec:** `docs/CKP_FORMAT_SPEC.md` (version 1.1, 2026-04-22)
**Source code:** `src/Ckp.Core`, `src/Ckp.IO`, `src/Ckp.Signing` at master.
**Written:** 2026-04-22 (QualityRaisingPlan § S8), updated post-X-series 2026-04-22.
**Purpose:** Single source of truth for every place the specification and the
reference implementation currently disagree. Each row maps to a plan work item
that will close it.

Legend for the **Resolution** column — `code` = change the code, `spec` = change
the spec, `both` = change both, `doc` = just add a clarifying line somewhere.

---

## 1. Archive structure (spec §3 vs. writer/reader)

| # | Drift | In spec? | In code? | Resolution | Plan item |
|---|---|---|---|---|---|
| D1 | `claims/by-tier/t{1..4}.json` views | ~~Listed~~ ✅ closed by X1 — removed from §3 tree and §3.1 table; note added that consumers build the views in memory | **Not emitted, not read** | spec — delete from §3; add a note that readers may build these in memory | X1 |
| D2 | `claims/by-domain/{name}.json` views | ~~Listed~~ ✅ closed by X1 | **Not emitted, not read** | spec — delete from §3 | X1 |
| D3 | `history/tier-changes.json` | ~~Listed~~ ✅ re-closed by X2b (spec 1.2, 2026-04-25) — entry removed from §3 tree and §3.1 table; writer no longer emits it | **No longer emitted; readers still tolerate it in legacy packages** (data was redundant with `PackageClaim.TierHistory`) | code + spec — drop the entry; tier-history view derived on consumer side | X2b |
| D4 | `enrichment/commentary/{publisher,community}.json` | ~~Silent~~ ✅ closed by X9 — new §15.6 formalizes the emission rule | Emitted when non-empty, read on round-trip | spec — add §15.6 "emit only when non-empty" rule | X9 |
| D5 | Determinism guarantees (entry order, pinned LastWriteTime 2000-01-01, canonical manifest JSON) | ~~Silent~~ ✅ closed by X7 — new §3.2 "Determinism" subsection | Enforced | spec — add §3.2 "Determinism" subsection | X7 |

## 2. Manifest (spec §4 vs. `PackageManifest`)

| # | Drift | In spec? | In code? | Resolution | Plan item |
|---|---|---|---|---|---|
| D6 | Property order in §4 example | ~~`formatVersion, packageId, …`~~ ✅ closed by X3 — note added before the JSON example citing RFC 8785 canonical form | Canonical JSON emits lexicographic order (`alignments, book, …`) | spec — add "example is illustrative; canonical form sorts keys per RFC 8785" | X3 |
| D7 | `book.language` nullability | ~~"Optional"~~ ✅ closed by X4 — marked Required with a note about the `"en"` default | `string Language` (non-nullable, default `"en"`) | spec — mark as required (matches transpiler default) | X4 |

## 3. Integrity (spec §10 vs. `CkpSigner`)

| # | Drift | In spec? | In code? | Resolution | Plan item |
|---|---|---|---|---|---|
| D8 | Hash format (`sha256:…`) | Matches | Matches | — | — |
| **D9** | **§10.2 says "Ed25519 signature over the package content"** | Claims whole-content coverage | ~~**Signs manifest only**~~ ✅ fully closed by S1 + X5 — code path: `ContentFingerprint.Hash` is a sorted-leaf SHA-256 over every non-manifest entry, injected on write and transitively covered by the Ed25519 signature over the canonical manifest. Spec: §10.2 rewritten, new §10.2.1 formalizes the construction. | code + spec — add a `packageContentHash` in the manifest (covered by the existing signature) AND rewrite §10.2 | S1 + X5 |
| D10 | Signature `algorithm` field | Table includes "Ed25519" | ~~Hardcoded to Ed25519 regardless of what the manifest says (downgrade/confusion risk)~~ ✅ closed by T7+S2+S5 — `CkpSigner.Verify` now rejects any Algorithm != "Ed25519" (ordinal, case-insensitive). | code — verify `signature.Algorithm == "Ed25519"` (ordinal) before calling NSec | S2 |
| D11 | "AI-assisted" trust-tier row in §10.2 table | ~~Present~~ ✅ closed by X10 — row removed; a note clarifies that AI-drafted packages must be signed by the accountable human/organization | `SignatureSource` enum has no matching value | spec — remove row (recommended) OR code — add enum value | X10 |
| D12 | `formatVersion` support gate | §15.4: "readers should reject unsupported versions" | ~~`CkpPackageReader` accepts any string~~ ✅ closed by T3 — reader rejects versions outside `CkpPackageReader.SupportedFormatVersions = {"1.0"}`. | code — reject versions outside `{"1.0"}` | T3 |

## 4. Field packages (spec §12 vs. `FieldPackage` / `TurbulenceDetector`)

| # | Drift | In spec? | In code? | Resolution | Plan item |
|---|---|---|---|---|---|
| D13 | `TurbulenceDetector` `tauBase` default | ~~§12.2 example shows `1.5`~~ ✅ closed by X6 — example and field table both show `0.7` | `TurbulenceDetector.DefaultTauBase = 0.7` | spec — update example to `0.7` (code is load-bearing for tests) | X6 |

## 5. Canonical JSON (spec vs. `CkpCanonicalJson`)

| # | Drift | In spec? | In code? | Resolution | Plan item |
|---|---|---|---|---|---|
| D14 | Canonical-JSON algorithm (RFC 8785-like, ordinal key sort, recursive, null-omit) | ~~Silent~~ ✅ closed by X5 + X7 — §3.2 Determinism cites RFC 8785, §10.2 notes the signed bytes come from `SerializeForSigning`, §10.2.1 documents the leaf fold | Fully implemented; documented only in a code comment | spec — cite RFC 8785 in §10.2.1 (to be added) | X5 / X7 |

## 6. Error contract (spec vs. reader)

| # | Drift | In spec? | In code? | Resolution | Plan item |
|---|---|---|---|---|---|
| D15 | Exception types raised by conformant readers | ~~Silent~~ ✅ closed by X8 + T3 — new §16 enumerates every default-read and strict-read error; reader already raises `CkpFormatException` uniformly | `CkpFormatException` uniformly (post-T3) | spec — add §16 "Error handling contract"; code — align reader to it | X8 + T3 |
| D16 | Alignment-path traversal guard (`alignment/external/../../evil.json`) | Silent | ~~**No normalization**~~ ✅ closed by T3 — `CkpPackageReader.IsAlignmentEntry` normalizes `..`/`.` segments and rejects entries that escape `alignment/external/`. | code — normalize and reject escaping paths; spec — note the guard | T3 |

## 7. Non-drift (spec and code agree)

Recorded here so future audits do not rediscover the same rows:

- Claim ID regex `^[a-z0-9]+-[a-z0-9]+\.[A-Z]{2,4}\.\d{3}$` — matches §5 and validator.
- Claim hash regex `^sha256:[a-f0-9]{64}$` — matches §5 and validator.
- `book.isbn` optional — matches.
- `t0Registry.constraintsReferenced` — matches.
- Evidence model (`EvidenceReferenceType`, `EvidenceRelationship`, `EvidenceStrength`) — matches §7.
- `book.domains` array of domain names — matches.
- Content fingerprint field set (claim count, domain count, tier counts, citation count) — matches.

---

## Cross-reference: plan items that close drift

- **S1** (content-hash over non-manifest entries) — closes D9 (largest drift).
- **S2** (verifier hardening: algorithm check + base64 try) — closes D10.
- **T3** (reader hardening: format-version + path-traversal + error wrapping) — closes D12, D15, D16.
- **X1** — closes D1, D2.
- **X2** — closes D3.
- **X3** — closes D6.
- **X4** — closes D7.
- **X5** — closes D9 (spec side) and D14.
- **X6** — closes D13.
- **X7** — closes D5 (and partially D14).
- **X8** — closes D15 (spec side).
- **X9** — closes D4.
- **X10** — closes D11.
- **X11** — bumps spec to 1.1 once the above have landed.

Once every drift row above has an "✅ closed by commit SHA" line, bump the spec
header to 1.1 per X11.
