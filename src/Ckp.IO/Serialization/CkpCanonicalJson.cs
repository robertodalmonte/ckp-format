namespace Ckp.IO;

using System.Text.Json;
using System.Text.Json.Serialization;
using Ckp.Core;

/// <summary>
/// Canonical JSON serializer for <see cref="PackageManifest"/> per RFC 8785 (JCS).
/// <para>
/// Produces deterministic bytes for a given manifest value: keys sorted lexicographically
/// by UTF-16 code point (<see cref="StringComparer.Ordinal"/>), no whitespace, UTF-8 output,
/// numbers emitted verbatim from <see cref="JsonElement.GetRawText"/>.
/// </para>
/// <para>
/// Scoped strictly to the manifest (the signed payload). Other .ckp entries are
/// emitted with the human-readable options in <see cref="CkpJsonOptions.Instance"/>.
/// </para>
/// <para>
/// The manifest's value subset contains only strings, ints, bools, nulls, arrays, and
/// objects — no floating-point numbers — so the simplified number handling is
/// RFC 8785-compliant for this payload.
/// </para>
/// </summary>
/// <remarks>
/// <b>Intended consumer:</b> library users and the Ckp.Signing package. Produces
/// RFC 8785 canonical JSON for the manifest — the exact byte form the signer signs
/// and the verifier rehashes.
/// </remarks>
public static class CkpCanonicalJson
{
    private static readonly JsonSerializerOptions TreeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter<Tier>(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    /// <summary>
    /// Canonicalizes the manifest to its signed byte form. The <see cref="PackageManifest.Signature"/>
    /// field is always stripped before serialization — the signature is computed over the
    /// manifest minus itself, then attached.
    /// </summary>
    public static byte[] SerializeForSigning(PackageManifest manifest)
    {
        var unsigned = manifest with { Signature = null };
        return Serialize(unsigned);
    }

    /// <summary>
    /// Canonicalizes any <see cref="PackageManifest"/> value verbatim (keeps the signature field
    /// if present). Useful for tests asserting byte-determinism.
    /// </summary>
    public static byte[] Serialize(PackageManifest manifest)
    {
        using var doc = JsonSerializer.SerializeToDocument(manifest, TreeOptions);
        using var ms = new MemoryStream();
        using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
        {
            WriteCanonical(doc.RootElement, writer);
        }
        return ms.ToArray();
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject()
                             .OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteCanonical(prop.Value, writer);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(item, writer);
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                throw new InvalidOperationException($"Unsupported JSON kind in manifest: {element.ValueKind}");
        }
    }
}
