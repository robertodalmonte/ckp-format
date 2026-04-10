namespace Ckp.Core;

/// <summary>
/// Domain taxonomy entry for the structure/ directory of a .ckp package.
/// </summary>
/// <param name="Name">Domain name (e.g., "autonomic-nervous-system").</param>
/// <param name="ClaimCount">Total claims in this domain.</param>
/// <param name="T1Count">T1 claims in this domain.</param>
/// <param name="T2Count">T2 claims in this domain.</param>
/// <param name="T3Count">T3 claims in this domain.</param>
/// <param name="T4Count">T4 claims in this domain.</param>
public sealed record DomainInfo(
    string Name,
    int ClaimCount,
    int T1Count,
    int T2Count,
    int T3Count,
    int T4Count);
