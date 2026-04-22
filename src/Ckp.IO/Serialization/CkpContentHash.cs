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
/// <para>
/// P2: <see cref="ComputeForPackageAsync"/> uses the streaming
/// <see cref="PackageEntrySerializer.PlanEntries"/> path and reuses a single pooled
/// <see cref="MemoryStream"/> across entries. Previously the full set of non-manifest
/// entry byte arrays was materialized into a <c>List&lt;(string, byte[])&gt;</c> before
/// hashing — peak memory at 10k claims held every serialized entry in the heap at once.
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
    /// Computes the content hash for a <see cref="CkpPackage"/> by walking the writer's
    /// entry plan, serializing each entry into a reusable scratch buffer, folding the
    /// leaf, and releasing — O(1) retained buffers independent of claim count.
    /// Use this to pre-compute the hash before signing so the signature covers the
    /// final manifest bytes (see "hash-then-sign" workflow on <see cref="CkpPackageWriter"/>).
    /// </summary>
    public static async Task<string> ComputeForPackageAsync(
        CkpPackage package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        var plan = PackageEntrySerializer.PlanEntries(package);
        return await ComputeForPlanAsync(plan, cancellationToken);
    }

    /// <summary>
    /// Computes the content hash for a pre-built entry plan. Internal helper shared
    /// between <see cref="ComputeForPackageAsync"/> and <see cref="CkpPackageWriter"/>
    /// so both agree byte-for-byte on what was hashed vs. what ended up in the archive.
    /// </summary>
    internal static async Task<string> ComputeForPlanAsync(
        IReadOnlyList<PackageEntryPlan> plan, CancellationToken cancellationToken)
    {
        // Plan is already ordinally sorted by PackageEntrySerializer.PlanEntries.
        using var root = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var scratch = new MemoryStream();
        // Stack-allocated Span<byte> cannot cross an await boundary, so this method
        // allocates two small heap buffers up front and reuses them across entries.
        // One pair per Compute call, independent of entry count.
        byte[] leafBuffer = new byte[32];
        byte[] separator = [0x00];
        byte[] terminator = [0x0A];

        foreach (var entry in plan)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Reuse the same backing buffer across entries. SetLength(0) zeroes the
            // logical length but keeps the internal array — repeated writes reuse it.
            scratch.SetLength(0);
            scratch.Position = 0;
            await entry.WriteToAsync(scratch, cancellationToken);

            if (!scratch.TryGetBuffer(out var segment))
                throw new InvalidOperationException("Scratch MemoryStream refused to expose its buffer.");

            if (!SHA256.TryHashData(segment.AsSpan(), leafBuffer, out var written) || written != 32)
                throw new InvalidOperationException("SHA-256 leaf hash produced unexpected length.");

            byte[] nameBytes = Encoding.UTF8.GetBytes(entry.Name);
            root.AppendData(nameBytes);
            root.AppendData(separator);
            root.AppendData(leafBuffer);
            root.AppendData(terminator);
        }

        byte[] rootBuffer = new byte[32];
        if (!root.TryGetHashAndReset(rootBuffer, out var rootWritten) || rootWritten != 32)
            throw new InvalidOperationException("SHA-256 root hash produced unexpected length.");

        return Prefix + Convert.ToHexStringLower(rootBuffer);
    }

    /// <summary>
    /// Computes the content hash for a sequence of non-manifest ZIP entries supplied as
    /// pre-materialized byte arrays. The input does not need to be pre-sorted — this
    /// method sorts ordinally before hashing so callers cannot accidentally produce a
    /// different digest by changing enumeration order.
    /// </summary>
    /// <remarks>
    /// Preserved as a public API for callers that already hold entry bytes (e.g., test
    /// fixtures verifying hash stability against known byte sequences). The plan-based
    /// overload used by <see cref="CkpPackageWriter"/> produces identical digests from
    /// equivalent inputs — verified by the round-trip tests in <c>CkpContentHashTests</c>.
    /// </remarks>
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
