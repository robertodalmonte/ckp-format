using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Ckp.IO;
using Ckp.Signing;

namespace Ckp.Tests;

/// <summary>
/// Pins the public API surface of <c>Ckp.Core</c>, <c>Ckp.IO</c>, and <c>Ckp.Signing</c>.
/// If any public member is added, removed, or changed, these tests fail and the
/// committed <c>api/*.txt</c> file must be regenerated with
/// <c>pwsh ./scripts/api-snapshot.ps1</c>. This gate prevents accidental public-API
/// changes from slipping through code review.
/// </summary>
public sealed class PublicApiSnapshotTests
{
    [Theory]
    [InlineData("Ckp.Core", typeof(PackageManifest))]
    [InlineData("Ckp.IO", typeof(CkpCanonicalJson))]
    [InlineData("Ckp.Signing", typeof(CkpSigner))]
    public void PublicApi_matches_committed_snapshot(string assemblyName, Type anchor)
    {
        var asm = anchor.Assembly;
        asm.GetName().Name.Should().Be(assemblyName);

        var regenerated = GenerateSnapshot(asm);
        var committed = ReadCommittedSnapshot(assemblyName);

        if (regenerated != committed)
        {
            throw new Xunit.Sdk.XunitException(
                $"Public API of {assemblyName} has drifted from api/{assemblyName}.txt.\n" +
                "Regenerate with: pwsh ./scripts/api-snapshot.ps1\n" +
                "First differing line (expected → actual):\n" +
                FirstDiff(committed, regenerated));
        }
    }

    private static string ReadCommittedSnapshot(string assemblyName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "api", $"{assemblyName}.txt"));
        File.Exists(path).Should().BeTrue($"api/{assemblyName}.txt must exist at {path}");
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    private static string FirstDiff(string expected, string actual)
    {
        var e = expected.Split('\n');
        var a = actual.Split('\n');
        for (int i = 0; i < Math.Max(e.Length, a.Length); i++)
        {
            var el = i < e.Length ? e[i] : "<eof>";
            var al = i < a.Length ? a[i] : "<eof>";
            if (el != al) return $"  line {i + 1}:\n    expected: {el}\n    actual:   {al}";
        }
        return "(identical but byte-diff present — check line endings)";
    }

    private static string GenerateSnapshot(Assembly asm)
    {
        var sb = new StringBuilder();
        var types = asm.GetExportedTypes().OrderBy(t => t.FullName, StringComparer.Ordinal).ToList();
        foreach (var t in types)
        {
            sb.Append(TypeKind(t)).Append(' ').Append(t.FullName).Append('\n');

            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance |
                                       BindingFlags.Static | BindingFlags.DeclaredOnly;

            var memberLines = new List<string>();

            foreach (var f in t.GetFields(flags).Where(f => f.IsPublic))
            {
                var mods = "";
                if (f.IsLiteral) mods = "const ";
                else
                {
                    if (f.IsStatic) mods += "static ";
                    if (f.IsInitOnly) mods += "readonly ";
                }
                memberLines.Add($"  field: {mods}{FormatType(f.FieldType)} {f.Name}");
            }

            foreach (var p in t.GetProperties(flags))
            {
                var accessors = new List<string>();
                if (p.GetMethod is { IsPublic: true }) accessors.Add("get");
                if (p.SetMethod is { IsPublic: true })
                {
                    var mods = p.SetMethod.ReturnParameter.GetRequiredCustomModifiers();
                    accessors.Add(mods.Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit")
                        ? "init" : "set");
                }
                var idx = "";
                var idxParams = p.GetIndexParameters();
                if (idxParams.Length > 0) idx = $"[{FormatParameters(idxParams)}]";
                memberLines.Add($"  property: {FormatType(p.PropertyType)} {p.Name}{idx} {{ {string.Join("; ", accessors)} }}");
            }

            foreach (var c in t.GetConstructors(flags).Where(c => c.IsPublic))
            {
                memberLines.Add($"  ctor: ({FormatParameters(c.GetParameters())})");
            }

            foreach (var m in t.GetMethods(flags).Where(m => m.IsPublic && !m.IsSpecialName))
            {
                var mods = "";
                if (m.IsStatic) mods += "static ";
                if (m.IsVirtual && !m.IsFinal) mods += "virtual ";
                var generic = "";
                if (m.IsGenericMethodDefinition)
                    generic = $"<{string.Join(", ", m.GetGenericArguments().Select(g => g.Name))}>";
                memberLines.Add($"  method: {mods}{FormatType(m.ReturnType)} {m.Name}{generic}({FormatParameters(m.GetParameters())})");
            }

            foreach (var e in t.GetEvents(flags))
            {
                memberLines.Add($"  event: {FormatType(e.EventHandlerType!)} {e.Name}");
            }

            foreach (var n in t.GetNestedTypes(flags).Where(n => n.IsNestedPublic))
            {
                memberLines.Add($"  nested: {FormatType(n)}");
            }

            memberLines.Sort(StringComparer.Ordinal);
            foreach (var line in memberLines) sb.Append(line).Append('\n');
            sb.Append('\n');
        }
        return sb.ToString();
    }

    private static string TypeKind(Type t)
    {
        if (t.IsEnum) return "enum";
        if (t.IsValueType) return "struct";
        if (t.IsInterface) return "interface";
        if (t is { IsSealed: true, IsAbstract: true }) return "static class";
        if (t.IsSealed) return "sealed class";
        if (t.IsAbstract) return "abstract class";
        return "class";
    }

    private static string FormatType(Type? t)
    {
        if (t is null) return "void";
        if (t.IsGenericParameter) return t.Name;
        if (t.IsArray) return FormatType(t.GetElementType()) + "[]";
        if (t.IsByRef) return FormatType(t.GetElementType());
        var ns = t.Namespace is null ? "" : t.Namespace + ".";
        if (t.IsGenericType)
        {
            var name = t.Name.Split('`')[0];
            var args = string.Join(", ", t.GetGenericArguments().Select(FormatType));
            return $"{ns}{name}<{args}>";
        }
        return ns + t.Name;
    }

    private static string FormatParameters(ParameterInfo[] ps)
    {
        var parts = new List<string>(ps.Length);
        foreach (var p in ps)
        {
            var prefix = "";
            if (p.IsOut) prefix = "out ";
            else if (p.ParameterType.IsByRef) prefix = "ref ";
            parts.Add($"{prefix}{FormatType(p.ParameterType)} {p.Name}");
        }
        return string.Join(", ", parts);
    }
}
