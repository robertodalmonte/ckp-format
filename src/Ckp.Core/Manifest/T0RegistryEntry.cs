namespace Ckp.Core;

/// <summary>
/// A single entry in the T0 axiom registry. T0 axioms are non-negotiable
/// physical, chemical, or mathematical laws that constrain claims.
/// </summary>
/// <param name="Id">Axiom identifier (e.g., "T0:PHYS.001").</param>
/// <param name="Statement">The axiom statement (e.g., "Energy cannot be created or destroyed in an isolated system.").</param>
/// <param name="Domain">Primary domain (e.g., "physics").</param>
/// <param name="SubDomain">Specific sub-domain (e.g., "thermodynamics").</param>
/// <param name="FormalExpression">Mathematical or formal expression (e.g., "dU = δQ − δW").</param>
/// <param name="Authority">Maintaining authority (e.g., "CODATA").</param>
/// <param name="Version">Registry version this entry belongs to (e.g., "2026.1").</param>
/// <param name="Constrains">Description of what this axiom constrains.</param>
public sealed record T0RegistryEntry(
    string Id,
    string Statement,
    string Domain,
    string? SubDomain,
    string? FormalExpression,
    string Authority,
    string Version,
    string? Constrains);
