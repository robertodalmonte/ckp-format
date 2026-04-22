namespace Ckp.IO;

/// <summary>
/// Raised by <see cref="CkpPackageReader"/> when a .ckp archive violates the format
/// specification in a way that cannot be represented as an <see cref="InvalidDataException"/>
/// (which is reserved for ZIP-level corruption by .NET). Instances carry the offending
/// entry name in <see cref="EntryName"/> and, where available, the underlying parse
/// failure in <see cref="Exception.InnerException"/>.
/// <para>
/// Triggering conditions — see <c>docs/Refactoring/QualityRaisingPlan.md</c> §3 T3:
/// </para>
/// <list type="bullet">
///   <item>Manifest <c>formatVersion</c> is outside the supported set.</item>
///   <item>An <c>alignment/external/...</c> entry has a path that, once normalized, escapes the directory.</item>
///   <item>A required JSON entry (<c>manifest.json</c>, etc.) fails to parse.</item>
/// </list>
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users catching it around read/write calls. Raised
/// by <see cref="CkpPackageReader"/> and <see cref="CkpPackageWriter"/>; carries the
/// entry name that failed in <see cref="EntryName"/>.
/// </remarks>
public sealed class CkpFormatException : Exception
{
    public CkpFormatException(string message, string? entryName = null, Exception? innerException = null)
        : base(message, innerException)
    {
        EntryName = entryName;
    }

    /// <summary>ZIP entry name the failure is attributed to, or <c>null</c> if archive-wide.</summary>
    public string? EntryName { get; }
}
