# CKP Architecture

**Applies to:** every project under `src/` in this repository.
**Status:** load-bearing — CI enforces the allowed-edges check defined at the bottom
of this document. Any PR that adds a `ProjectReference` not listed here must update
this document in the same commit.

---

## Layering

The repository ships seven projects plus one benchmark harness. They stack into three
tiers:

```
┌─────────────────────────────────────────────────────────────┐
│  CLI tier                                                   │
│  ┌─────────────────────┐      ┌─────────────────────┐       │
│  │ Ckp.Transpiler.Cli  │      │ Ckp.Epub.Cli        │       │
│  └──────────┬──────────┘      └──────────┬──────────┘       │
└─────────────┼────────────────────────────┼──────────────────┘
              │                            │
┌─────────────┼────────────────────────────┼──────────────────┐
│  Library tier                            │                  │
│  ┌──────────▼──────────┐      ┌──────────▼──────────┐       │
│  │ Ckp.Transpiler      │      │ Ckp.Epub            │       │
│  └──────────┬──────────┘      └──────────┬──────────┘       │
│             │                            │                  │
│             │   ┌────────────────────┐   │                  │
│             └──►│ Ckp.IO             │◄──┘                  │
│                 └─────────┬──────────┘                      │
│                           │                                 │
│                           │   ┌────────────────────┐        │
│                           └──►│ Ckp.Signing        │        │
│                               └─────────┬──────────┘        │
└─────────────────────────────────────────┼───────────────────┘
                                          │
┌─────────────────────────────────────────┼───────────────────┐
│  Core tier                              │                   │
│                        ┌────────────────▼────────────┐      │
│                        │ Ckp.Core                    │      │
│                        │ (no references)             │      │
│                        └─────────────────────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

`Ckp.Benchmarks` sits outside the solution (`ckp-format.slnx`) and references
`Ckp.Core`, `Ckp.IO`, `Ckp.Signing` for measurement. It is not part of the shipping
artefact set and does not appear in the edge check.

---

## Allowed edges

The complete, closed set of allowed `ProjectReference` edges (as of 2026-04-22):

| From                    | Allowed targets                                    |
|-------------------------|----------------------------------------------------|
| `Ckp.Core`              | _(none)_                                           |
| `Ckp.IO`                | `Ckp.Core`                                         |
| `Ckp.Signing`           | `Ckp.Core`, `Ckp.IO`                               |
| `Ckp.Transpiler`        | `Ckp.Core`, `Ckp.IO`                               |
| `Ckp.Epub`              | `Ckp.Core`, `Ckp.IO`                               |
| `Ckp.Transpiler.Cli`    | `Ckp.Transpiler`                                   |
| `Ckp.Epub.Cli`          | `Ckp.Epub`                                         |
| `Ckp.Benchmarks`        | `Ckp.Core`, `Ckp.IO`, `Ckp.Signing` (non-shipping) |

## Forbidden edges (and why)

- **CLI → `Ckp.IO`.** A2 moved the `CkpPackageWriter` usage from each CLI's `Program.cs`
  into a thin façade extension on the corresponding library. Result: CLIs only see the
  library's surface and cannot accidentally depend on internal IO types. If you find
  yourself wanting `Ckp.IO` in a CLI, add the method you need to the library-side
  extension class instead.
- **`Ckp.Core` → anything.** Core must stay a standalone domain-model assembly with no
  transitive IO, crypto, or file-format dependencies. Consumers like the Consilience
  repo (at `W:\source\robertodalmonte\Consilience`) reference Core alone when they
  only need to inspect a hydrated `CkpPackage`.
- **`Ckp.IO` → `Ckp.Signing`.** Signing depends on IO (for the canonical-JSON serializer
  and the `CkpPackageReader`/`Writer` types), never the other way around. A delegate-based
  inversion (`SignatureVerifier` passed into the reader) is how IO consumes verification
  without a reference cycle.
- **Cross-library (e.g., `Ckp.Transpiler` → `Ckp.Epub`).** The two transpilers are
  independent ingestion paths for different source formats (custom JSON KB vs. ePub).
  They share nothing except the `CkpPackage` output shape defined in `Ckp.Core`.

## The "IO reference cycle" anti-pattern

Several types in `Ckp.IO` want signature verification semantics (strict-read mode,
round-trip hash check). Adding `ProjectReference Ckp.Signing` inside `Ckp.IO` would
create the cycle `IO → Signing → IO`. The codebase instead passes a
`Func<PackageManifest, bool> verifier` delegate into `CkpReadOptions`, resolved by the
caller who already has both references. See `src/Ckp.IO/Serialization/CkpReadOptions.cs`.

---

## `CkpHash` — why it lives in `Ckp.Core` (A4)

`CkpHash.OfStatement(...)` computes the per-claim SHA-256 digest that every claim's
`Hash` field must match (rule S2 in the extraction validator). The value is
semantically part of the claim; tampering with it produces a different hash and the
claim no longer validates.

Two placement options were considered:

1. **Keep in `Ckp.Core`** (chosen). Hashing is a pure function of the claim's
   statement string — no IO, no serialization. Factoring it out into a separate
   `IHashFunction` interface would let consumers pick a different algorithm, but
   the CKP spec (§6.1 and §10.1) pins SHA-256 by design: claim hashes are content-
   addressable identifiers and stability across readers is non-negotiable. An
   interface would create a configuration surface with exactly one valid setting.
2. **Move to `Ckp.IO`.** Only consumers that write packages care about computing
   hashes… except that `PackageClaim.CreateNew` computes and stores the hash at
   factory time, and that factory lives in Core. Moving the hash would force
   `Ckp.Core` → `Ckp.IO`, violating the Core-has-no-references rule.

**Decision:** keep `CkpHash` in `Ckp.Core`. It is correctly placed next to
`PackageClaim` and the other claim-domain types. The spec-mandated SHA-256 algorithm
means this is not a point of variation worth abstracting.

---

## Determinism and the architecture

Several architectural choices are motivated by byte-determinism (identical input →
identical output archive):

- Canonical JSON (`Ckp.IO.CkpCanonicalJson`) lives with the writer so only one path
  produces the bytes that get signed. Attempts to canonicalize in `Ckp.Core` were
  rejected because Core must stay allocation-light and serialization-agnostic.
- The `PackageEntrySerializer.PlanEntries` closure list is the single source of
  truth for "what goes in the archive, in what order." Both the content-hash fold
  and the ZIP writer walk the same list. No second sort key exists anywhere.
- `TimeProvider` and `Func<Guid>` are injectable into `PackageManifest.CreateNew`
  (item A5 in the Quality-Raising Pass). Test fixtures and benchmarks pin them to
  constants; production code defaults to `TimeProvider.System` and
  `Guid.CreateVersion7`.

---

## Enforcing the edges in CI

A simple allowed-edges check can be run locally or in CI:

```pwsh
# Verify that no project under src/ has an unexpected ProjectReference.
$allowed = @{
    'Ckp.Core'             = @()
    'Ckp.IO'               = @('Ckp.Core')
    'Ckp.Signing'          = @('Ckp.Core', 'Ckp.IO')
    'Ckp.Transpiler'       = @('Ckp.Core', 'Ckp.IO')
    'Ckp.Epub'             = @('Ckp.Core', 'Ckp.IO')
    'Ckp.Transpiler.Cli'   = @('Ckp.Transpiler')
    'Ckp.Epub.Cli'         = @('Ckp.Epub')
}

$fail = $false
Get-ChildItem src -Recurse -Filter *.csproj |
    Where-Object { $_.BaseName -ne 'Ckp.Benchmarks' } |
    ForEach-Object {
        $name = $_.BaseName
        $refs = Select-Xml -Path $_.FullName -XPath "//ProjectReference" |
            ForEach-Object { ([IO.Path]::GetFileNameWithoutExtension($_.Node.Include)) } |
            Sort-Object -Unique
        $expected = $allowed[$name] | Sort-Object -Unique
        $extra   = @($refs | Where-Object { $_ -notin $expected })
        $missing = @($expected | Where-Object { $_ -notin $refs })
        if ($extra -or $missing) {
            Write-Host "[$name]"
            if ($extra)   { Write-Host "  Unexpected references: $($extra -join ', ')" }
            if ($missing) { Write-Host "  Missing references: $($missing -join ', ')" }
            $fail = $true
        }
    }

if ($fail) { exit 1 } else { Write-Host "OK: all ProjectReference edges match docs/Architecture.md" }
```

Save this as `scripts/check-edges.ps1` if you want it on disk; run it manually before
a merge that touches `.csproj` files.
