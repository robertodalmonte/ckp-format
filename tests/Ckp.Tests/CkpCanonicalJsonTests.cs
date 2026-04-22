namespace Ckp.Tests;

using System.Text;
using System.Text.Json;
using Ckp.Core;
using Ckp.IO;

/// <summary>
/// Dedicated tests for <see cref="CkpCanonicalJson"/>. Covers items 19–23 in
/// <c>docs/Refactoring/QualityRaisingPlan.md</c> §3.1 — nested-key ordering,
/// signature stripping, array order preservation, no whitespace, null omission.
/// Until now these properties were only asserted indirectly via the signer tests.
/// </summary>
public sealed class CkpCanonicalJsonTests
{
    // Item 19 — deterministic for equal manifests.
    [Fact]
    public void Serialize_is_deterministic_for_equal_manifests()
    {
        var a = BuildManifest();
        var b = BuildManifest();

        var bytesA = CkpCanonicalJson.Serialize(a);
        var bytesB = CkpCanonicalJson.Serialize(b);

        bytesA.Should().Equal(bytesB);
    }

    // Item 20 — lexicographic key order at the root.
    [Fact]
    public void Serialize_orders_root_keys_lexicographically_ordinal()
    {
        var manifest = BuildManifest();

        var json = Utf8(CkpCanonicalJson.Serialize(manifest));

        var rootKeys = ReadKeyOrder(json, path: []);
        rootKeys.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }

    // Item 21 — nested objects (book, contentFingerprint, each alignment) must also be sorted.
    [Fact]
    public void Serialize_orders_nested_object_keys_lexicographically_ordinal()
    {
        var manifest = BuildManifest();

        var json = Utf8(CkpCanonicalJson.Serialize(manifest));
        using var doc = JsonDocument.Parse(json);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            AssertObjectKeysOrdinal(prop.Value, $"/{prop.Name}");
        }
    }

    // Item 22 — SerializeForSigning strips any pre-existing signature.
    [Fact]
    public void SerializeForSigning_strips_existing_signature()
    {
        var unsigned = BuildManifest();
        var signed = unsigned with
        {
            Signature = new PackageSignature(
                Algorithm: "Ed25519",
                PublicKey: "AAAA",
                Signature: "BBBB",
                Source: SignatureSource.Publisher),
        };

        var bytesFromUnsigned = CkpCanonicalJson.SerializeForSigning(unsigned);
        var bytesFromSigned = CkpCanonicalJson.SerializeForSigning(signed);

        bytesFromSigned.Should().Equal(bytesFromUnsigned,
            "the signature block must not contribute to its own signed payload");
    }

    // Item 22b — round-trip: serializing an already-unsigned manifest twice via the two
    // entry points should produce the same bytes (SerializeForSigning ≡ Serialize when
    // Signature is already null).
    [Fact]
    public void SerializeForSigning_equals_Serialize_when_signature_is_null()
    {
        var unsigned = BuildManifest();

        var viaSerialize = CkpCanonicalJson.Serialize(unsigned);
        var viaForSigning = CkpCanonicalJson.SerializeForSigning(unsigned);

        viaForSigning.Should().Equal(viaSerialize);
    }

    // Item 23 — array element order is preserved (arrays are not sorted).
    [Fact]
    public void Serialize_preserves_array_element_order()
    {
        // "zzz" comes before "aaa" in the input list. Canonicalization must preserve that order.
        var alignmentsForward = new List<AlignmentSummary>
        {
            new("zzz-book", null, 1, 0),
            new("aaa-book", null, 1, 0),
        };
        var alignmentsReverse = new List<AlignmentSummary>
        {
            new("aaa-book", null, 1, 0),
            new("zzz-book", null, 1, 0),
        };

        var manifestA = BuildManifest() with { Alignments = alignmentsForward };
        var manifestB = BuildManifest() with { Alignments = alignmentsReverse };

        var jsonA = Utf8(CkpCanonicalJson.Serialize(manifestA));
        var jsonB = Utf8(CkpCanonicalJson.Serialize(manifestB));

        jsonA.Should().NotBe(jsonB,
            "arrays preserve element order — different input orderings are observably different outputs");
        jsonA.IndexOf("zzz-book", StringComparison.Ordinal)
            .Should().BeLessThan(jsonA.IndexOf("aaa-book", StringComparison.Ordinal));
    }

    [Fact]
    public void Serialize_emits_no_whitespace_between_tokens()
    {
        var manifest = BuildManifest();

        var json = Utf8(CkpCanonicalJson.Serialize(manifest));

        // Canonical JSON has no newlines and no space outside string values.
        json.Should().NotContain("\n").And.NotContain("\r");
        // A quick separator-pattern check. In canonical JCS, `":"` and `,"` never contain a space.
        json.Should().NotContain(": ").And.NotContain(", ");
    }

    [Fact]
    public void Serialize_omits_optional_null_fields()
    {
        // Book.Isbn is null; T0Registry is null. Neither key should appear at all.
        var manifest = BuildManifest();

        var json = Utf8(CkpCanonicalJson.Serialize(manifest));

        json.Should().NotContain("\"isbn\"", "optional null fields are omitted, not emitted as null");
        json.Should().NotContain("\"t0Registry\"");
        json.Should().NotContain("\"signature\"",
            "null signature is omitted even outside SerializeForSigning");
    }

    [Fact]
    public void Serialize_produces_utf8_without_bom()
    {
        var manifest = BuildManifest();

        var bytes = CkpCanonicalJson.Serialize(manifest);

        bytes.Should().NotBeEmpty();
        // First byte must be the opening brace, not a BOM (0xEF 0xBB 0xBF).
        bytes[0].Should().Be((byte)'{');
    }

    private static PackageManifest BuildManifest()
    {
        var book = new BookMetadata(
            Key: "canon-1e",
            Title: "Canonical Fixture",
            Edition: 1,
            Authors: ["Author One", "Author Two"],
            Publisher: "Pub",
            Year: 2026,
            Isbn: null,
            Language: "en-US",
            Domains: ["alpha-domain", "beta-domain"]);
        var fp = new ContentFingerprint("SHA-256", 10, 5, 3, 2, 1, 0, 1);
        // Use a fixed package id/timestamp rather than CreateNew's wall-clock form, to keep
        // the fixture deterministic without relying on factory internals.
        return PackageManifest.Restore(
            formatVersion: "1.0",
            packageId: Guid.Parse("01950000-0000-7000-8000-000000000001"),
            createdAt: DateTimeOffset.Parse("2026-04-22T12:00:00Z"),
            signature: null,
            book: book,
            contentFingerprint: fp,
            t0Registry: null,
            alignments: [new AlignmentSummary("zeta-3e", null, 42, 5)]);
    }

    private static string Utf8(byte[] bytes) => Encoding.UTF8.GetString(bytes);

    private static List<string> ReadKeyOrder(string json, string[] path)
    {
        using var doc = JsonDocument.Parse(json);
        var element = doc.RootElement;
        foreach (var segment in path)
            element = element.GetProperty(segment);
        return element.EnumerateObject().Select(p => p.Name).ToList();
    }

    private static void AssertObjectKeysOrdinal(JsonElement element, string pointer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = element.EnumerateObject().Select(p => p.Name).ToList();
            keys.Should().BeInAscendingOrder(StringComparer.Ordinal,
                $"keys at {pointer} must be ordinal-sorted");
            foreach (var prop in element.EnumerateObject())
                AssertObjectKeysOrdinal(prop.Value, $"{pointer}/{prop.Name}");
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int i = 0;
            foreach (var item in element.EnumerateArray())
                AssertObjectKeysOrdinal(item, $"{pointer}/[{i++}]");
        }
    }
}
