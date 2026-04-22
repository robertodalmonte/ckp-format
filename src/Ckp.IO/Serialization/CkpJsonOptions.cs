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
/// <para>
/// T5 — <see cref="JsonSerializerOptions.WriteIndented"/> is pinned to <c>false</c>
/// to guarantee byte-deterministic output regardless of future
/// <c>System.Text.Json</c> default changes. Human-readability of individual entries
/// is a non-goal; callers can re-pretty-print after extracting if needed.
/// </para>
/// </summary>
internal static class CkpJsonOptions
{
    internal static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter<Tier>(),
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };
}
