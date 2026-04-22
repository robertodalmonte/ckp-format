namespace Ckp.Transpiler;

using Ckp.Core;
using Ckp.IO;

/// <summary>
/// Convenience façade over <see cref="KnowledgeBaseTranspiler"/> that bundles the
/// transpile + write steps behind one call. Exists so CLI projects can depend only on
/// <c>Ckp.Transpiler</c> (and <c>Ckp.Core</c> for types like <see cref="CkpPackage"/>)
/// without taking a direct reference on <c>Ckp.IO</c> — the architectural invariant
/// documented in <c>docs/Architecture.md</c> (A2/A3).
/// </summary>
/// <remarks>
/// <para>
/// Library consumers that want finer-grained control (e.g., injecting a signing step
/// between transpile and write) should still call <see cref="KnowledgeBaseTranspiler.TranspileAsync"/>
/// and <see cref="CkpPackageWriter.WriteAsync"/> themselves — this is a thin pass-through
/// for the 80% case, not a replacement.
/// </para>
/// </remarks>
public static class KnowledgeBaseTranspilerExtensions
{
    /// <summary>
    /// Transpiles the KnowledgeBase and writes the resulting package to the given
    /// destination stream. No signing, no hash injection — the caller gets the default
    /// content-hash-injection behaviour of <see cref="CkpPackageWriter"/>.
    /// </summary>
    public static async Task<CkpPackage> TranspileAndWriteAsync(
        this KnowledgeBaseTranspiler transpiler,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transpiler);
        ArgumentNullException.ThrowIfNull(destination);

        var package = await transpiler.TranspileAsync(cancellationToken);
        var writer = new CkpPackageWriter();
        await writer.WriteAsync(package, destination, cancellationToken);
        return package;
    }

    /// <summary>
    /// Overload that creates/truncates the file at <paramref name="outputPath"/> and
    /// writes the transpiled package into it. Equivalent to the CLI's happy path.
    /// </summary>
    public static async Task<CkpPackage> TranspileAndWriteAsync(
        this KnowledgeBaseTranspiler transpiler,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transpiler);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        await using var fileStream = File.Create(outputPath);
        return await transpiler.TranspileAndWriteAsync(fileStream, cancellationToken);
    }
}
