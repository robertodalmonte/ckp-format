namespace Ckp.IO;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Computes the S1 package content hash: a sorted-leaf, folded SHA-256 digest that binds
/// every non-manifest ZIP entry's name and bytes into a single fixed-length string.
/// <para>
/// The result lives inside <see cref="ContentFingerprint.Hash"/>, which sits inside the
/// manifest, which Ed25519 signs (see <see cref="CkpCanonicalJson"/>). A valid signature
/// therefore covers the full package content by transitivity — flipping a single byte
/// anywhere invalidates the hash, and the hash change invalidates the signature.
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// Algorithm (canonical, spec §10.1 once S8 lands):
/// </para>
/// <list type="number">
///   <item>Per entry, compute <c>leaf = SHA-256(content-bytes)</c> — a 32-byte value.</item>
///   <item>Sort the entries by name using <see cref="StringComparer.Ordinal"/> on the UTF-16 form.
///         The writer already emits entries in this order, so the hash stays stable across
///         writer+reader implementations that follow the same ordering rule.</item>
///   <item>
///     Fold: for each sorted entry emit <c>name-utf8 || 0x00 || leaf || 0x0A</c> into a running
///     SHA-256, and return <c>"sha256:" + lowercase-hex(root)</c>.
///   </item>
/// </list>
/// <para>
/// The 0x00 separator prevents name/content confusion between neighbouring entries; the
/// trailing 0x0A terminates each record so an attacker cannot merge two entries into one.
/// Leaves are pre-hashed rather than concatenated raw so an attacker cannot shift bytes
/// between neighbouring entries while preserving the root.
/// </para>
/// <para>
/// The manifest itself is excluded: it contains this very hash, so including it would be
/// self-referential. The manifest is instead protected by the Ed25519 signature.
/// </para>
/// </remarks>
public static class CkpContentHash
{
    /// <summary>
    /// Prefix appended to the lowercase-hex digest to produce the canonical string form.
    /// Matches the spec's claim-hash format (<c>"sha256:&lt;64-hex&gt;"</c>) so a single
    /// validation regex covers both surfaces.
    /// </summary>
    public const string Prefix = "sha256:";

    /// <summary>
    /// Computes the content hash for a sequence of non-manifest ZIP entries. The input does
    /// not need to be pre-sorted — this method sorts ordinally before hashing so callers
    /// cannot accidentally produce a different digest by changing enumeration order.
    /// </summary>
    /// <summary>
    /// Computes the content hash for a <see cref="CkpPackage"/> by serializing every
    /// non-manifest entry through the exact same path the writer uses, then folding.
    /// Use this to pre-compute the hash before signing so the signature covers the
    /// final manifest bytes (see "hash-then-sign" workflow on <see cref="CkpPackageWriter"/>).
    /// </summary>
    public static async Task<string> ComputeForPackageAsync(
        CkpPackage package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var entries = await PackageEntrySerializer.SerializeAsync(package, cancellationToken);
        return Compute(entries);
    }

    public static string Compute(IEnumerable<(string Name, byte[] Bytes)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var sorted = entries
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ToList();

        using var root = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> leafBuffer = stackalloc byte[32];
        byte[] separator = [0x00];
        byte[] terminator = [0x0A];

        foreach (var (name, bytes) in sorted)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(bytes);

            if (!SHA256.TryHashData(bytes, leafBuffer, out var written) || written != 32)
                throw new InvalidOperationException("SHA-256 leaf hash produced unexpected length.");

            var nameBytes = Encoding.UTF8.GetBytes(name);
            root.AppendData(nameBytes);
            root.AppendData(separator);
            root.AppendData(leafBuffer);
            root.AppendData(terminator);
        }

        Span<byte> rootBuffer = stackalloc byte[32];
        if (!root.TryGetHashAndReset(rootBuffer, out var rootWritten) || rootWritten != 32)
            throw new InvalidOperationException("SHA-256 root hash produced unexpected length.");

        return Prefix + Convert.ToHexStringLower(rootBuffer);
    }
}
