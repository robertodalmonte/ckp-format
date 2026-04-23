# Security Policy

## Reporting a Vulnerability

If you believe you've found a security vulnerability in CKP (Consilience Knowledge Package), please report it privately so we can address it before public disclosure.

**Please do not open a public GitHub issue for security vulnerabilities.**

### How to Report

Use GitHub's **[Private Vulnerability Reporting](https://github.com/robertodalmonte/ckp-format/security/advisories/new)** to submit a confidential report. This routes the disclosure directly to the maintainers.

Include, when possible:

- A description of the vulnerability and its impact
- Steps to reproduce, or a proof-of-concept
- The affected version / commit
- Any suggested mitigation

### What to Expect

- **Acknowledgement** within 7 days.
- **Initial assessment** within 14 days, including whether the report is accepted and a rough timeline to fix.
- **Coordinated disclosure**: once a fix is available, we will publish a GitHub Security Advisory crediting the reporter (unless anonymity is requested).

## Supported Versions

CKP is pre-1.0. Only the `master` branch receives security fixes.

## Scope

In scope:

- The CKP file format (signing, hashing, integrity checks)
- The transpiler and EPUB reader CLIs
- NuGet-published libraries (`Ckp.Core`, `Ckp.IO`, `Ckp.Signing`, `Ckp.Transpiler`, `Ckp.Epub`)

Out of scope:

- Issues in third-party dependencies — report those upstream; we track them via Dependabot.
- Bugs with no security impact — open a normal GitHub issue instead.
