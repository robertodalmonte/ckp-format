namespace Ckp.Transpiler;

using System.Text.RegularExpressions;

/// <summary>
/// Parses KnowledgeBase claim IDs into CKP-format components.
/// Domain-agnostic — works with any KnowledgeBase regardless of topic.
/// </summary>
public static partial class DomainRegistry
{
    /// <summary>
    /// Extracts the domain code from a KB claim ID (e.g., "cl-ans-001" → "ANS").
    /// </summary>
    public static string ExtractDomainCode(string kbClaimId)
    {
        var match = ClaimIdPattern().Match(kbClaimId);
        if (!match.Success)
            throw new InvalidOperationException($"Cannot parse KB claim ID: '{kbClaimId}'");
        return match.Groups[1].Value.ToUpperInvariant();
    }

    /// <summary>
    /// Extracts the sequence number from a KB claim ID (e.g., "cl-ans-001" → 1).
    /// </summary>
    public static int ExtractSequence(string kbClaimId)
    {
        var match = ClaimIdPattern().Match(kbClaimId);
        if (!match.Success)
            throw new InvalidOperationException($"Cannot parse KB claim ID: '{kbClaimId}'");
        return int.Parse(match.Groups[2].Value);
    }

    /// <summary>
    /// Converts a KB claim ID to a CKP package claim ID.
    /// "cl-ans-001" → "consilience-v1.ANS.001"
    /// </summary>
    public static string ToCkpClaimId(string kbClaimId, string bookKey)
    {
        string code = ExtractDomainCode(kbClaimId);
        int seq = ExtractSequence(kbClaimId);
        return $"{bookKey}.{code}.{seq:D3}";
    }

    /// <summary>
    /// Returns the domain name as the lowercase domain code.
    /// The transpiler is domain-agnostic — it does not interpret what codes mean.
    /// </summary>
    public static string ToDomainName(string domainCode) => domainCode.ToLowerInvariant();

    [GeneratedRegex(@"^cl-([a-z]+)-(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ClaimIdPattern();
}
