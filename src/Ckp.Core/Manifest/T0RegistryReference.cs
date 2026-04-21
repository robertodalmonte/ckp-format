namespace Ckp.Core.Manifest;

/// <summary>
/// Reference to the version of the T0 axiom registry used by claims in this package.
/// T0 axioms live in a shared, versioned, cryptographically signed registry — not in
/// individual packages.
/// </summary>
/// <param name="Version">Registry version (e.g., "2026.1").</param>
/// <param name="Source">URI of the registry source.</param>
/// <param name="ConstraintsReferenced">Number of T0 constraints referenced by claims in this package.</param>
public sealed record T0RegistryReference(
    string Version,
    string? Source,
    int ConstraintsReferenced);
