$ErrorActionPreference = 'Stop'

$stage = 'C:\Users\Matthias\Desktop\Fallout Immersion Project\Guidelines\1.0 Release'
$sourceRoot = Join-Path $stage 'Source'

function Assert-InStage([string]$Path) {
    $root = [IO.Path]::GetFullPath($stage).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside staging: $full"
    }
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    Assert-InStage $Path
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function Split-AtMarker(
    [string]$Path,
    [string]$Marker,
    [string]$PatchPath,
    [string]$PatchHeader,
    [scriptblock]$CoreTransform
) {
    $text = [IO.File]::ReadAllText($Path)
    $index = $text.IndexOf($Marker, [StringComparison]::Ordinal)
    if ($index -lt 0) { throw "Marker not found in ${Path}: $Marker" }
    $core = $text.Substring(0, $index).TrimEnd() + "`r`n"
    $patch = $text.Substring($index).TrimStart()
    $core = & $CoreTransform $core
    Write-Utf8NoBom $Path $core
    Write-Utf8NoBom $PatchPath ($PatchHeader.TrimEnd() + "`r`n`r`n" + $patch)
}

$robCo = Join-Path $sourceRoot 'FIP-RobCo'
$robCoFile = Join-Path $robCo 'SyntheticPawnMechanics.cs'
Split-AtMarker $robCoFile '[HarmonyPatch(typeof(PawnRenderNode_Body)' (Join-Path $robCo 'Harmony\SyntheticPawnPatches.cs') @'
using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.RobCo;
'@ {
    param($core)
    $core = $core.Replace("using HarmonyLib;`r`n", '')
    $core = $core.Replace('internal static class SyntheticPawnUtility', 'public static class SyntheticPawnUtility')
    $core = $core.Replace('        new Harmony("FIP.RobCo.SyntheticPawns").PatchAll();' + "`r`n", '')
    return $core
}
Write-Utf8NoBom (Join-Path $robCo 'Harmony\RobCoHarmonyBootstrap.cs') @'
using HarmonyLib;
using Verse;

namespace FIP.RobCo;

[StaticConstructorOnStartup]
internal static class RobCoHarmonyBootstrap
{
    static RobCoHarmonyBootstrap()
    {
        new Harmony("FIP.RobCo.SyntheticPawns").PatchAll();
    }
}
'@

$westTek = Join-Path $sourceRoot 'FIP-WestTek'
Split-AtMarker (Join-Path $westTek 'Gene_WestTekSuperMutantAppearance.cs') '[HarmonyPatch(typeof(PawnRenderNode_Body)' (Join-Path $westTek 'Harmony\SuperMutantRenderPatch.cs') @'
using HarmonyLib;
using RimWorld;
using Verse;

namespace FIP.WestTek;
'@ {
    param($core)
    return $core.Replace("using HarmonyLib;`r`n", '')
}

Split-AtMarker (Join-Path $westTek 'WestTekFloraGeneEffects.cs') '[HarmonyPatch(typeof(Pawn), nameof(Pawn.TickRare))]' (Join-Path $westTek 'Harmony\FloraGenePatches.cs') @'
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.WestTek;
'@ {
    param($core)
    $core = $core.Replace("using HarmonyLib;`r`n", '')
    $core = $core.Replace('internal static class WestTekFloraGeneUtility', 'public static class WestTekFloraGeneUtility')
    $core = $core.Replace('AccessTools.Field(typeof(SkillRecord), "pawn")', 'typeof(SkillRecord).GetField("pawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)')
    return $core
}

Split-AtMarker (Join-Path $westTek 'WestTekSpecialAssignment.cs') '[HarmonyPatch(typeof(PawnGenerator), "GenerateGenes")]' (Join-Path $westTek 'Harmony\SpecialAssignmentPatch.cs') @'
using HarmonyLib;
using RimWorld;
using Verse;

namespace FIP.WestTek;
'@ {
    param($core)
    $core = $core.Replace("using HarmonyLib;`r`n", '')
    return $core.Replace('internal static class WestTekSpecialUtility', 'public static class WestTekSpecialUtility')
}

$slavePath = Join-Path $westTek 'WestTekSlaveSuperMutant.cs'
$slave = [IO.File]::ReadAllText($slavePath).Replace("using HarmonyLib;`r`n", '')
Write-Utf8NoBom $slavePath $slave

foreach ($publicUtility in @(
    @{ Path = 'Patch_SporeCarrierDeathFertility.cs'; Old = 'internal static class WestTekSporeCarrierDeathUtility'; New = 'public static class WestTekSporeCarrierDeathUtility' },
    @{ Path = 'WestTekFaunaMutationUtility.cs'; Old = 'internal static class WestTekFaunaMutationUtility'; New = 'public static class WestTekFaunaMutationUtility' }
)) {
    $path = Join-Path $westTek $publicUtility.Path
    $text = [IO.File]::ReadAllText($path).Replace($publicUtility.Old, $publicUtility.New)
    Write-Utf8NoBom $path $text
}

$westTekMod = Join-Path $westTek 'WestTekMod.cs'
$westTekHarmonyMod = Join-Path $westTek 'Harmony\WestTekMod.cs'
Write-Utf8NoBom $westTekHarmonyMod ([IO.File]::ReadAllText($westTekMod))
Assert-InStage $westTekMod
Remove-Item -LiteralPath $westTekMod -Force

Write-Output 'Harmony source split completed.'
