# CKP Signing — threat model

**Scope.** This document defines what the `.ckp` package signature protects, what it does
not, and the adversary capabilities we explicitly consider. Cross-reference:
`docs/CKP_FORMAT_SPEC.md` §10 (integrity), `src/Ckp.Signing/CkpSigner.cs`
(`SignManifest`, `VerifyManifest`), and `src/Ckp.IO/Serialization/CkpContentHash.cs`
(the S1 content hash that brings non-manifest entries inside the signed scope).

**Status:** written post-S1 (2026-04-22). S3 (strict-read mode) and S8 (spec alignment)
are still open; paragraphs that depend on them are marked **[Pending S3]** /
**[Pending S8]**.

## 1. Actors

| Actor | Capability | Trust |
|---|---|---|
| **Publisher** | Produces the `.ckp`, holds the Ed25519 private key, signs the manifest. | Trusted for the package they sign. Key compromise shifts trust. |
| **Distributor** | Mirrors, caches, or delivers the `.ckp` bytes over the network / filesystem. | Untrusted. Assume they can read, tamper, replay, or drop packages. |
| **Consumer** | Reads the `.ckp`, wants to know what they got. Holds only the publisher's public key (out-of-band). | The relying party this model protects. |
| **Attacker** | Any non-publisher party with write access to the bytes at any hop between publisher and consumer. Subsumes distributor. | Fully untrusted. |

CKP assumes consumers obtain the publisher's public key through a trustworthy channel
(direct exchange, a pinned identity provider, or a future key-distribution registry). The
format itself does not solve key distribution — see §4 **T-KD**.

## 2. Assets

| Asset | Protected by |
|---|---|
| **Manifest bytes** (`manifest.json`): `formatVersion`, `packageId`, `createdAt`, `book`, `contentFingerprint` (incl. the S1 hash), `t0Registry`, `alignments`, minus `signature` itself. | Ed25519 signature over canonical JSON (`CkpCanonicalJson.SerializeForSigning`). |
| **Non-manifest entries** (`claims/*`, `evidence/*`, `structure/*`, `history/*`, `enrichment/*`, `alignment/external/*`). | `ContentFingerprint.Hash` (sorted-leaf SHA-256). The hash sits inside the manifest, so the signature covers it transitively. |
| **Identity of the signing key.** | `PackageSignature.PublicKey` (base64 raw Ed25519). Consumers must compare against a known-good key; an unpinned public key proves nothing. |
| **Claim-level content identity.** | Each claim's own `hash` field (`sha256:…`) plus the signature transitively. Enables future partial verification without breaking the format. |

## 3. Threats in scope — what CKP defends

Numbering matches the test `CkpContentHashTests` + `CkpSignerSecurityTests` suite.

### T-BYTE. Byte-level tampering in any entry

*Attacker flips one byte anywhere in the archive after signing.*

- If the byte is inside `manifest.json` → canonical JSON changes → Ed25519 verification
  fails.
- If the byte is inside any non-manifest entry → that entry's leaf SHA-256 changes → the
  root content hash changes → the manifest now disagrees with the archive body.
  **[Pending S3]** strict-read mode detects this by recomputing and comparing. Until
  S3 lands, `VerifyManifest` still returns true because the manifest itself is intact —
  callers must invoke `CkpContentHash.ComputeForPackageAsync` and compare manually.

Covered by `CkpContentHashTests.Post_write_tampering_breaks_signature_verification` and
`Writer_throws_when_manifest_hash_mismatches_computed`.

### T-REORDER. Reordering entries without changing bytes

*Attacker repacks the ZIP with entries in a different order, preserving content.*

The content hash is defined over entries sorted by name (ordinal), so reorder alone does
not change the hash. ZIP central-directory order is cosmetic. Defended by construction
(the hash canonicalizes), not a threat to signature validity.

### T-ADD. Adding spurious entries

*Attacker appends e.g. `claims/evil.json` into the archive.*

- If the entry name falls inside a known prefix (`claims/claims.json`, etc.) the reader
  will either replace the expected entry (same name → rejected by the hash since the
  bytes differ) or ignore unknown siblings (unknown name → not counted in the hash →
  **undetected unless S3 strict mode cross-checks the archive entry set against the
  hashed set**). **[Pending S3]**: strict mode must enumerate the archive and hash the
  same set, rejecting extra unrecognized entries.
- If the entry is under `alignment/external/` it will be picked up by the reader; since
  it was not in the signed hash, the computed-vs-stored hash comparison (S3) fails.

### T-DOWNGRADE-ALGORITHM. Algorithm-field downgrade

*Attacker rewrites `signature.algorithm` to a different string ("RSA", "Ed448", etc.).*

Rejected by `CkpSigner.Verify`: an ordinal-case-insensitive equality check against
`"Ed25519"` runs before any crypto. Covered by
`CkpSignerSecurityTests.Verify_rejects_non_Ed25519_algorithm` (Theory over RSA, Ed448,
empty, padded variants).

### T-DOWNGRADE-UNSIGNED. Signature strip

*Attacker removes the `signature` block entirely; consumer forgets to check.*

`VerifyManifest` returns `false` for null signature — correct behaviour. But a
consumer that reads the package and never calls verify still holds unsigned data.
**[Pending S3]**: the strict-read option `RequireSignature = true` fails the read
at the reader level, so callers cannot accidentally skip verification.

### T-FORGE-KEY. Forged signature with attacker-controlled key

*Attacker re-signs the (tampered) manifest with their own key, rewrites `signature.publicKey`.*

The Ed25519 math accepts the forged signature against the forged key. **This attack
succeeds at the format level** — the defence is out-of-band: the consumer must compare
the `publicKey` field against a pinned expected key before trusting the verification
result. **[Pending S3]**: `CkpReadOptions.ExpectedPublicKey` makes this check mandatory
at the reader layer.

### T-MALFORMED-BASE64. Base64 corruption panic

*Attacker rewrites `signature.publicKey` or `signature.signature` to invalid base64 in
hopes of crashing the verification path.*

`CkpSigner.Verify` uses `Convert.TryFromBase64String` (S2) → returns `false`, never
throws. Covered by `CkpSignerSecurityTests.Verify_rejects_bad_base64_*`.

### T-WRONG-LENGTH. Key / signature length confusion

*Attacker supplies valid base64 but with wrong length (e.g. 31-byte key, 65-byte signature).*

Rejected pre-crypto with explicit length checks in `Verify` (32-byte public key, 64-byte
signature). Covered by `CkpSignerSecurityTests.Verify_rejects_wrong_public_key_length`
and `_wrong_signature_length`.

### T-STALE-REPLAY. Replay of a superseded package

*Attacker redistributes an old, validly-signed package in place of a newer one.*

CKP carries `createdAt` and `packageId` in the signed scope but has no revocation list.
Consumers who care about freshness must maintain their own last-seen-packageId or
last-seen-createdAt per publisher. **Out of scope for CKP itself.**

## 4. Threats out of scope — what CKP does **not** defend

### T-KD. Key distribution

CKP defines a format for signatures; it does not distribute public keys. A
consumer who downloads both the package and the key from the same compromised mirror
gets a valid-looking verification with no real security. Solution belongs to a
separate key-registry layer (not in this codebase).

### T-REVOKE. Key revocation

If a publisher's private key is compromised, CKP provides no mechanism to revoke
previously-signed packages. Mitigation: publish a revocation list out-of-band and have
consumers consult it; future CKP versions may add a `revokedAt` field to the manifest
schema.

### T-CONFIDENTIALITY. Encryption

CKP does not encrypt content. A `.ckp` is plaintext ZIP + JSON; anyone with the bytes
can read them. If a use case needs confidentiality, encrypt the bytes at a higher
layer.

### T-DOS. Resource exhaustion on read

Decompression bombs, adversarially-large JSON trees, deep recursion in nav fixtures —
all affect the reader's memory/CPU. CKP does not guarantee bounded read cost. Consumers
running on untrusted input should impose their own limits (max archive size,
`System.Text.Json` `MaxDepth`, etc.). Phase 4 P-series may add a `CkpReadOptions.MaxBytes`
defence.

### T-TIMING. Side channels in verification

NSec's `Verify` is constant-time; Ed25519 is not believed to leak via side channels at
the math level. CKP's own logic (base64 parse, length checks) runs before the crypto
and has no secret-dependent branches, so early returns are benign. We do not formally
prove timing-safety.

### T-PROVENANCE-SEMANTICS. "Publisher said so" ≠ "true"

A valid signature proves the publisher produced these bytes. It does not prove the
claims inside are correct, peer-reviewed, or non-malicious. The `SignatureSource` enum
(Publisher / Author / Scholar / Community) is advisory — consumers choose which sources
they trust.

## 5. Acceptance criteria cross-reference

QualityRaisingPlan §5.2 S4 requires "at least 4 named threats and their
mitigation-or-out-of-scope status." This document lists 10 in-scope and 5 out-of-scope,
each cross-linked to code or a pending work item.

## 6. Future work

- **S3** — wire the threats marked **[Pending S3]** into `CkpReadOptions` so consumers
  cannot forget to check. Target: verify-by-default, opt out explicitly.
- **S8** — reconcile spec §10 narrative with this document. Currently the spec predates
  S1 and still talks about "signature over the package content" without explaining the
  hash mechanism.
- **Key distribution draft** — separate doc, separate deliverable. Not in the current
  refactoring plan.
