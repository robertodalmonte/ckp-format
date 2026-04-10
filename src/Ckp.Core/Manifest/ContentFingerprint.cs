namespace Ckp.Core;

/// <summary>
/// Statistical fingerprint of a .ckp package's content for quick integrity checks
/// and summary display without reading every claim.
/// </summary>
/// <param name="Algorithm">Hash algorithm used (e.g., "SHA-256").</param>
/// <param name="ClaimCount">Total number of claims in the package.</param>
/// <param name="DomainCount">Number of distinct domains.</param>
/// <param name="T1Count">Count of T1 (established mechanism) claims.</param>
/// <param name="T2Count">Count of T2 (supported hypothesis) claims.</param>
/// <param name="T3Count">Count of T3 (speculative bridge) claims.</param>
/// <param name="T4Count">Count of T4 (ancient observation) claims.</param>
/// <param name="CitationCount">Total number of citations across all claims.</param>
public sealed record ContentFingerprint(
    string Algorithm,
    int ClaimCount,
    int DomainCount,
    int T1Count,
    int T2Count,
    int T3Count,
    int T4Count,
    int CitationCount);
