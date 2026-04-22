namespace Ckp.Tests.TestSupport;

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Ckp.Core;
using Ckp.IO;

/// <summary>
/// Builds in-memory ZIP archives that match (or deliberately violate) the .ckp
/// archive layout, for adversarial reader tests.
/// <para>
/// Default <see cref="Build"/> produces a minimally well-formed .ckp — a
/// <c>manifest.json</c> entry with the canonical manifest bytes. Every mutation
/// method returns a new <see cref="MalformedZipBuilder"/> so tests can compose
/// scenarios fluently (e.g. <c>Default().WithTruncatedStream(16)</c>).
/// </para>
/// </summary>
internal sealed class MalformedZipBuilder
{
    private readonly List<ZipEntrySpec> _entries = [];
    private int _truncateAfter = -1;
    private int _duplicateManifestCount = 1;

    public static MalformedZipBuilder Default()
    {
        var builder = new MalformedZipBuilder();
        builder.AddManifest();
        return builder;
    }

    public MalformedZipBuilder AddManifest(string? formatVersionOverride = null, bool valid = true)
    {
        var bytes = valid
            ? CanonicalManifestBytes(formatVersionOverride)
            : "{ this is not valid json"u8.ToArray();
        _entries.Add(new ZipEntrySpec("manifest.json", bytes));
        return this;
    }

    public MalformedZipBuilder AddEntry(string name, byte[] bytes)
    {
        _entries.Add(new ZipEntrySpec(name, bytes));
        return this;
    }

    public MalformedZipBuilder AddEntry(string name, string utf8)
    {
        _entries.Add(new ZipEntrySpec(name, Encoding.UTF8.GetBytes(utf8)));
        return this;
    }

    /// <summary>
    /// Replaces the manifest entry's bytes with literal <c>"null"</c> — System.Text.Json
    /// deserializes that to a null <see cref="PackageManifest"/>.
    /// </summary>
    public MalformedZipBuilder WithNullManifest()
    {
        _entries.RemoveAll(e => e.Name == "manifest.json");
        _entries.Add(new ZipEntrySpec("manifest.json", "null"u8.ToArray()));
        return this;
    }

    /// <summary>
    /// Appends an alignment entry whose path, once normalized, escapes <c>alignment/external/</c>.
    /// Used to verify reader path-traversal hardening (T3).
    /// </summary>
    public MalformedZipBuilder WithTraversalAlignment()
    {
        _entries.Add(new ZipEntrySpec(
            "alignment/external/../../evil.json",
            "{\"sourceBook\":\"evil\",\"targetBook\":\"evil-2e\",\"alignments\":[]}"u8.ToArray()));
        return this;
    }

    /// <summary>Adds a second manifest.json entry with altered content. .NET returns the first.</summary>
    public MalformedZipBuilder WithDuplicateManifest()
    {
        _duplicateManifestCount = 2;
        return this;
    }

    /// <summary>Cuts the generated archive bytes after the specified offset to simulate a truncated stream.</summary>
    public MalformedZipBuilder TruncateAfter(int byteCount)
    {
        _truncateAfter = byteCount;
        return this;
    }

    public byte[] Build()
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var entry in _entries)
            {
                int copies = entry.Name == "manifest.json" ? _duplicateManifestCount : 1;
                for (int i = 0; i < copies; i++)
                {
                    var ze = archive.CreateEntry(entry.Name, CompressionLevel.NoCompression);
                    using var s = ze.Open();
                    // Vary duplicate bytes so reader behaviour is observable if it picks the "wrong" copy.
                    var entryBytes = (copies == 2 && i == 1)
                        ? CorruptManifestBytes()
                        : entry.Bytes;
                    s.Write(entryBytes, 0, entryBytes.Length);
                }
            }
        }

        var bytes = ms.ToArray();
        if (_truncateAfter >= 0 && _truncateAfter < bytes.Length)
        {
            Array.Resize(ref bytes, _truncateAfter);
        }
        return bytes;
    }

    /// <summary>Produces bytes that do not parse as a ZIP at all.</summary>
    public static byte[] NonZipBytes() =>
        "this is definitely not a zip archive"u8.ToArray();

    /// <summary>Produces bytes that are ZIP-like but whose central directory is clobbered.</summary>
    public static byte[] CorruptCentralDirectory()
    {
        // Build a valid zip, then zero out the last 22 bytes (End Of Central Directory record).
        var ok = Default().Build();
        for (int i = ok.Length - 22; i < ok.Length && i >= 0; i++)
            ok[i] = 0xFF;
        return ok;
    }

    private static byte[] CanonicalManifestBytes(string? formatVersionOverride)
    {
        var book = new BookMetadata(
            Key: "fixture-1e",
            Title: "Fixture",
            Edition: 1,
            Authors: ["Nobody"],
            Publisher: "None",
            Year: 2026,
            Isbn: null,
            Language: "en-US",
            Domains: []);
        var fp = new ContentFingerprint("SHA-256", 0, 0, 0, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        if (formatVersionOverride is not null)
            manifest = manifest with { FormatVersion = formatVersionOverride };
        return CkpCanonicalJson.Serialize(manifest);
    }

    private static byte[] CorruptManifestBytes()
    {
        // A *valid* JSON manifest with the formatVersion wiped — reader would use this
        // if it picked the duplicate. Verifies that the reader picks the first entry.
        var book = new BookMetadata("duplicate-9e", "Duplicate", 9, ["Impostor"], "Bad", 2099, null, "en-US", []);
        var fp = new ContentFingerprint("SHA-256", 999, 999, 0, 0, 0, 0, 0);
        var manifest = PackageManifest.CreateNew(book, fp);
        return CkpCanonicalJson.Serialize(manifest);
    }

    private sealed record ZipEntrySpec(string Name, byte[] Bytes);
}
