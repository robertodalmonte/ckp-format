namespace Ckp.Core;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Factory methods for creating <see cref="PackageClaim"/> instances.
/// </summary>
public static class PackageClaimConstruction
{
    extension(PackageClaim)
    {
        /// <summary>
        /// Creates a new <see cref="PackageClaim"/> with an auto-computed SHA-256 hash.
        /// </summary>
        public static PackageClaim CreateNew(
            string id,
            string statement,
            string tier,
            string domain,
            string? subDomain = null,
            int? chapter = null,
            string? section = null,
            string? pageRange = null,
            IReadOnlyList<string>? keywords = null,
            IReadOnlyList<string>? meshTerms = null,
            IReadOnlyList<EvidenceReference>? evidence = null,
            IReadOnlyList<Observable>? observables = null,
            int? sinceEdition = null,
            IReadOnlyList<TierHistoryEntry>? tierHistory = null) => new(
                Id: id,
                Statement: statement,
                Tier: tier,
                Domain: domain,
                SubDomain: subDomain,
                Chapter: chapter,
                Section: section,
                PageRange: pageRange,
                Keywords: keywords ?? [],
                MeshTerms: meshTerms ?? [],
                Evidence: evidence ?? [],
                Observables: observables ?? [],
                SinceEdition: sinceEdition,
                TierHistory: tierHistory ?? [],
                Hash: ComputeHash(statement));

        /// <summary>
        /// Restores a <see cref="PackageClaim"/> from serialized data with all fields explicit.
        /// </summary>
        public static PackageClaim Restore(
            string id,
            string statement,
            string tier,
            string domain,
            string? subDomain,
            int? chapter,
            string? section,
            string? pageRange,
            IReadOnlyList<string> keywords,
            IReadOnlyList<string> meshTerms,
            IReadOnlyList<EvidenceReference> evidence,
            IReadOnlyList<Observable> observables,
            int? sinceEdition,
            IReadOnlyList<TierHistoryEntry> tierHistory,
            string hash) => new(
                Id: id,
                Statement: statement,
                Tier: tier,
                Domain: domain,
                SubDomain: subDomain,
                Chapter: chapter,
                Section: section,
                PageRange: pageRange,
                Keywords: keywords,
                MeshTerms: meshTerms,
                Evidence: evidence,
                Observables: observables,
                SinceEdition: sinceEdition,
                TierHistory: tierHistory,
                Hash: hash);
    }

    internal static string ComputeHash(string statement)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(statement));
        return $"sha256:{Convert.ToHexStringLower(bytes)}";
    }
}
