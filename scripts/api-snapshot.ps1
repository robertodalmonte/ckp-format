<#
.SYNOPSIS
    Produces deterministic public-API snapshots for Ckp.Core, Ckp.IO, Ckp.Signing.

.DESCRIPTION
    Loads the Release build of each library via reflection and writes
    api/<AssemblyName>.txt — one line per public member, sorted lexicographically
    by fully-qualified signature. The snapshot is intended to be committed; any
    diff in the pre-commit or CI check means the public API changed.

    Verify-only mode (-Verify) re-generates into a temp buffer and fails if the
    result differs from the committed snapshot; used as a CI guard.

.PARAMETER Configuration
    Build configuration to read assemblies from. Defaults to Release.

.PARAMETER Verify
    If set, fail (non-zero exit) when the regenerated snapshot differs from the
    committed version. Otherwise, overwrite the committed snapshot.

.EXAMPLE
    pwsh ./scripts/api-snapshot.ps1
    pwsh ./scripts/api-snapshot.ps1 -Verify
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$apiDir = Join-Path $repoRoot 'api'
if (-not (Test-Path $apiDir)) {
    New-Item -ItemType Directory -Path $apiDir | Out-Null
}

$assemblies = @(
    @{ Name = 'Ckp.Core';    Dll = "src/Ckp.Core/bin/$Configuration/net10.0/Ckp.Core.dll" }
    @{ Name = 'Ckp.IO';      Dll = "src/Ckp.IO/bin/$Configuration/net10.0/Ckp.IO.dll" }
    @{ Name = 'Ckp.Signing'; Dll = "src/Ckp.Signing/bin/$Configuration/net10.0/Ckp.Signing.dll" }
)

Write-Host "Ensuring $Configuration build exists..."
& dotnet build (Join-Path $repoRoot 'ckp-format.slnx') -c $Configuration --nologo -v quiet | Out-Host
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }

function Format-Type {
    param([Type]$T)
    if ($null -eq $T) { return 'void' }
    if ($T.IsGenericParameter) { return $T.Name }
    if ($T.IsArray) { return (Format-Type $T.GetElementType()) + '[]' }
    if ($T.IsByRef) { return (Format-Type $T.GetElementType()) }
    $ns = if ($T.Namespace) { "$($T.Namespace)." } else { '' }
    if ($T.IsGenericType) {
        $name = $T.Name -replace '`\d+$', ''
        $args = ($T.GetGenericArguments() | ForEach-Object { Format-Type $_ }) -join ', '
        return "$ns$name<$args>"
    }
    return "$ns$($T.Name)"
}

function Format-Parameters {
    param($Params)
    $out = @()
    foreach ($p in $Params) {
        $prefix = ''
        if ($p.IsOut) { $prefix = 'out ' }
        elseif ($p.ParameterType.IsByRef) { $prefix = 'ref ' }
        $out += "$prefix$((Format-Type $p.ParameterType)) $($p.Name)"
    }
    return ($out -join ', ')
}

function Get-MemberLines {
    param([Reflection.Assembly]$Asm)

    $lines = [System.Collections.Generic.List[string]]::new()
    $types = $Asm.GetExportedTypes()
    $typeArr = [System.Array]::CreateInstance([Type], $types.Count)
    for ($i = 0; $i -lt $types.Count; $i++) { $typeArr[$i] = $types[$i] }
    [Array]::Sort($typeArr, [Comparison[Type]]{ param($a, $b) [StringComparer]::Ordinal.Compare($a.FullName, $b.FullName) })
    $types = $typeArr

    foreach ($t in $types) {
        $kind = 'class'
        if ($t.IsEnum) { $kind = 'enum' }
        elseif ($t.IsValueType) { $kind = 'struct' }
        elseif ($t.IsInterface) { $kind = 'interface' }
        elseif ($t.IsSealed -and $t.IsAbstract) { $kind = 'static class' }
        elseif ($t.IsSealed) { $kind = 'sealed class' }
        elseif ($t.IsAbstract) { $kind = 'abstract class' }

        $lines.Add("$kind $($t.FullName)")

        $flags = [Reflection.BindingFlags]::Public -bor `
                 [Reflection.BindingFlags]::Instance -bor `
                 [Reflection.BindingFlags]::Static -bor `
                 [Reflection.BindingFlags]::DeclaredOnly

        $memberLines = [System.Collections.Generic.List[string]]::new()

        foreach ($f in $t.GetFields($flags) | Where-Object { $_.IsPublic }) {
            $mods = ''
            if ($f.IsStatic) { $mods += 'static ' }
            if ($f.IsInitOnly) { $mods += 'readonly ' }
            if ($f.IsLiteral) { $mods = 'const ' }
            $memberLines.Add("  field: $mods$((Format-Type $f.FieldType)) $($f.Name)")
        }

        foreach ($p in $t.GetProperties($flags)) {
            $accessors = @()
            if ($p.GetMethod -and $p.GetMethod.IsPublic) { $accessors += 'get' }
            if ($p.SetMethod -and $p.SetMethod.IsPublic) {
                if ($p.SetMethod.ReturnParameter.GetRequiredCustomModifiers() |
                    Where-Object { $_.FullName -eq 'System.Runtime.CompilerServices.IsExternalInit' }) {
                    $accessors += 'init'
                } else {
                    $accessors += 'set'
                }
            }
            $idx = ''
            $idxParams = $p.GetIndexParameters()
            if ($idxParams.Count -gt 0) { $idx = "[$(Format-Parameters $idxParams)]" }
            $memberLines.Add("  property: $((Format-Type $p.PropertyType)) $($p.Name)$idx { $(($accessors) -join '; ') }")
        }

        foreach ($c in $t.GetConstructors($flags) | Where-Object { $_.IsPublic }) {
            $memberLines.Add("  ctor: ($(Format-Parameters $c.GetParameters()))")
        }

        foreach ($m in $t.GetMethods($flags) | Where-Object { $_.IsPublic -and -not $_.IsSpecialName }) {
            $mods = ''
            if ($m.IsStatic) { $mods += 'static ' }
            if ($m.IsVirtual -and -not $m.IsFinal) { $mods += 'virtual ' }
            $generic = ''
            if ($m.IsGenericMethodDefinition) {
                $generic = "<$((($m.GetGenericArguments() | ForEach-Object { $_.Name })) -join ', ')>"
            }
            $memberLines.Add("  method: $mods$((Format-Type $m.ReturnType)) $($m.Name)$generic($(Format-Parameters $m.GetParameters()))")
        }

        foreach ($evt in $t.GetEvents($flags)) {
            $memberLines.Add("  event: $((Format-Type $evt.EventHandlerType)) $($evt.Name)")
        }

        foreach ($nested in $t.GetNestedTypes($flags) | Where-Object { $_.IsNestedPublic }) {
            $memberLines.Add("  nested: $((Format-Type $nested))")
        }

        $sortedArr = $memberLines.ToArray()
        [Array]::Sort($sortedArr, [StringComparer]::Ordinal)
        foreach ($ml in $sortedArr) { $lines.Add($ml) }
        $lines.Add('')
    }

    return $lines
}

$exitCode = 0

foreach ($asm in $assemblies) {
    $dllPath = Join-Path $repoRoot $asm.Dll
    if (-not (Test-Path $dllPath)) { throw "Assembly not found: $dllPath" }

    $loaded = [Reflection.Assembly]::LoadFrom($dllPath)
    $lines = Get-MemberLines -Asm $loaded

    $outPath = Join-Path $apiDir "$($asm.Name).txt"
    $content = ($lines -join "`n") + "`n"

    if ($Verify) {
        if (-not (Test-Path $outPath)) {
            Write-Host "  MISSING: $outPath"
            $exitCode = 1
            continue
        }
        $existing = [IO.File]::ReadAllText($outPath) -replace "`r`n", "`n"
        if ($existing -ne $content) {
            Write-Host "  DRIFT: $($asm.Name) — public API has changed"
            $exitCode = 1
        } else {
            Write-Host "  OK: $($asm.Name)"
        }
    } else {
        [IO.File]::WriteAllText($outPath, $content)
        Write-Host "  wrote: api/$($asm.Name).txt ($($lines.Count) lines)"
    }
}

if ($Verify -and $exitCode -ne 0) {
    Write-Host ""
    Write-Host "Public API snapshot is out of date. Run: pwsh ./scripts/api-snapshot.ps1"
    exit $exitCode
}
