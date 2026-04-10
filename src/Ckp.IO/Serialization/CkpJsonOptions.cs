namespace Ckp.IO;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Shared JSON serializer options for .ckp package serialization.
/// Uses camelCase to match the CKP JSON schema specification.
/// </summary>
internal static class CkpJsonOptions
{
    internal static readonly JsonSerializerOptions Instance = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
