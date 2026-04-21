namespace Ckp.IO;

using System.Text.Json;
using System.Text.Json.Serialization;
using Ckp.Core;

/// <summary>
/// Shared JSON serializer options for .ckp package serialization.
/// Uses camelCase to match the CKP JSON schema specification.
/// <para>
/// <see cref="Tier"/> has its own converter registered ahead of the general
/// camelCase enum converter so it emits the spec-form <c>"T1"</c>..<c>"T4"</c>
/// rather than being lowered to <c>"t1"</c>..<c>"t4"</c>.
/// </para>
/// </summary>
internal static class CkpJsonOptions
{
    internal static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter<Tier>(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };
}
