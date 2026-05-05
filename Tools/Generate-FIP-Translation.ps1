<#
.SYNOPSIS
    Generates a dummy English DefInjected translation mod for all FIP mods.
    Every translatable text node is copied verbatim (1:1 English source).
.NOTES
    FIP mods use flat Defs/ layout (no 1.6/ versioning prefix).
    Output goes to FIP-Translation Part II.
#>

param(
    [string]$FipRoot  = "C:\Users\Matthias\Desktop\Fallout Immersion Project",
    [string]$OutputMod = "C:\Users\Matthias\Desktop\Fallout Immersion Project\FIP-Translation Part II"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Translatable leaf-node names (case-insensitive lookup via hashtable)
# ---------------------------------------------------------------------------
$TranslatableLeafNames = @{}
@(
    'label','labelShort','labelMale','labelFemale',
    'labelMalePlural','labelFemalePlural','labelPlural',
    'description','descriptionShort','baseDesc',
    'title','titleShort','titleFemale','titleShortFemale',
    'leaderTitle','leaderTitleFemale',
    'jobString','reportString','inspectString','baseInspectLine',
    'verb','chargeNoun',
    'letterLabel','letterText','letterDesc',
    'beginLetterLabel','beginLetterText',
    'arrivedLetterLabel','arrivedLetterText',
    'pawnsArrivalMessage','joinText',
    'successMessage','successText','failMessage','failText',
    'rejectInputMessage',
    'customLetterLabel','customLetterText',
    'gerundLabel','customLabel',
    'pawnLabel','pawnSingular','pawnPlural',
    'skillLabel','skillDescription',
    'headerTip','tip','helpText','summary',
    'text','note',
    'nameNoun','nameSuffix','namePrefix',
    'failTriggerText','pawnCannotEquipReason',
    'descriptionExtra','labelNounPretty',
    'customSummary','extraTooltip',
    'header',
    'baseTitle','baseTitleFemale',
    'pawnsPlural','leaderPawnSingular',
    'name',
    'summary'
) | ForEach-Object {
    $TranslatableLeafNames[$_.ToLowerInvariant()] = $true
}

# ---------------------------------------------------------------------------
# List-parent element names whose direct <li> children are plain strings
# ---------------------------------------------------------------------------
$StringListParents = @{}
@('rulesStrings') | ForEach-Object { $StringListParents[$_.ToLowerInvariant()] = $true }

# ---------------------------------------------------------------------------
# Recursive walker – returns List of [PSCustomObject]@{Path; Value}
# ---------------------------------------------------------------------------
function Get-TranslatablePairs {
    param(
        [System.Xml.XmlElement]$Element,
        [string]$PathPrefix,
        [bool]$IsStringListParent = $false
    )

    $results = [System.Collections.Generic.List[PSCustomObject]]::new()
    $liIndex = 0

    foreach ($child in $Element.ChildNodes) {
        if ($child -isnot [System.Xml.XmlElement]) { continue }

        $cName = $child.LocalName
        $isLi  = ($cName -eq 'li')

        $cPath = if ($isLi) {
            $idx = $liIndex; $liIndex++
            "${PathPrefix}.${idx}"
        } else {
            if ($PathPrefix) { "${PathPrefix}.${cName}" } else { $cName }
        }

        $childElems    = @($child.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })
        $hasChildElems = $childElems.Count -gt 0

        if ($IsStringListParent -and $isLi) {
            $text = $child.InnerText.Trim()
            if ($text) {
                $results.Add([PSCustomObject]@{ Path = $cPath; Value = $text }) | Out-Null
            }
        }
        elseif (-not $hasChildElems) {
            $key = $cName.ToLowerInvariant()
            if ($TranslatableLeafNames.ContainsKey($key)) {
                $text = $child.InnerText.Trim()
                if ($text) {
                    $results.Add([PSCustomObject]@{ Path = $cPath; Value = $text }) | Out-Null
                }
            }
        }
        else {
            $childIsStringList = $StringListParents.ContainsKey($cName.ToLowerInvariant())
            $subResults = Get-TranslatablePairs -Element $child -PathPrefix $cPath -IsStringListParent $childIsStringList
            foreach ($p in $subResults) { $results.Add($p) | Out-Null }
        }
    }

    return , $results
}

# ---------------------------------------------------------------------------
# XML escape
# ---------------------------------------------------------------------------
function Escape-Xml ([string]$s) {
    $s = $s.Replace('&', '&amp;')
    $s = $s.Replace('<', '&lt;')
    $s = $s.Replace('>', '&gt;')
    $s = $s.Replace('"', '&quot;')
    return $s
}

# ---------------------------------------------------------------------------
# Process one Def XML file -> hashtable: defType -> List<pair>
# ---------------------------------------------------------------------------
function Process-DefFile {
    param([string]$FilePath)

    $byType = @{}

    try {
        $raw = [System.IO.File]::ReadAllText($FilePath, [System.Text.Encoding]::UTF8)
        # Fix unescaped & that are not already part of a valid XML entity (&amp; &#nn; &name;)
        $raw = [regex]::Replace($raw, '&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;|\w+;)', '&amp;')
        $xml = [xml]$raw
    } catch {
        # Fallback: file might be missing the root <Defs> wrapper
        try {
            $raw2 = "<?xml version='1.0' encoding='UTF-8'?><Defs>" + $raw.TrimStart() + "</Defs>"
            $raw2 = [regex]::Replace($raw2, '<\?xml[^>]*\?>', '', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            $raw2 = "<?xml version='1.0' encoding='UTF-8'?><Defs>" + $raw2
            $xml  = [xml]$raw2
        } catch {
            Write-Warning "Skipping (parse error): $FilePath`n  $_"
            return $byType
        }
    }

    $defsNode = $xml.Defs
    if (-not $defsNode) { return $byType }

    foreach ($defNode in $defsNode.ChildNodes) {
        if ($defNode -isnot [System.Xml.XmlElement]) { continue }
        if ($defNode.GetAttribute('Abstract') -ieq 'True') { continue }

        $defType = $defNode.LocalName
        $defName = $defNode.defName
        if (-not $defName) { continue }

        $pairs = Get-TranslatablePairs -Element $defNode -PathPrefix $defName

        if ($pairs.Count -eq 0) { continue }

        if (-not $byType.ContainsKey($defType)) {
            $byType[$defType] = [System.Collections.Generic.List[PSCustomObject]]::new()
        }
        foreach ($p in $pairs) { $byType[$defType].Add($p) | Out-Null }
    }

    return $byType
}

# ---------------------------------------------------------------------------
# Build XML text for one DefInjected file
# ---------------------------------------------------------------------------
function Build-DefInjectedXml {
    param([System.Collections.Generic.List[PSCustomObject]]$Pairs)

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
    [void]$sb.AppendLine('<LanguageData>')

    $lastDefName = $null
    foreach ($p in $Pairs) {
        $dot = $p.Path.IndexOf('.')
        $dn  = if ($dot -ge 0) { $p.Path.Substring(0, $dot) } else { $p.Path }
        if ($dn -ne $lastDefName) {
            if ($lastDefName -ne $null) { [void]$sb.AppendLine() }
            [void]$sb.AppendLine("  <!-- $dn -->")
            $lastDefName = $dn
        }

        $tag   = $p.Path
        if ($p.Value -match "`n") {
            $inner = $p.Value.Replace(']]>', ']]&gt;')
            [void]$sb.AppendLine("  <$tag><![CDATA[$inner]]></$tag>")
        } else {
            [void]$sb.AppendLine("  <$tag>$(Escape-Xml $p.Value)</$tag>")
        }
    }

    [void]$sb.AppendLine('</LanguageData>')
    return $sb.ToString()
}

# ===========================================================================
# MAIN
# ===========================================================================

Write-Host "=== FIP → FIP-Translation Part II generator ===" -ForegroundColor Cyan

$fipMods = Get-ChildItem $FipRoot -Directory | Where-Object {
    $_.Name -like "FIP-*" -and $_.Name -notlike "FIP-Translation*"
} | Sort-Object Name

Write-Host "Found $($fipMods.Count) FIP mods."

# Master accumulator: defType -> fileName -> List<pair>
$accumulator = @{}

foreach ($mod in $fipMods) {
    Write-Host "  Processing $($mod.Name)..." -NoNewline

    $scanRoot = Join-Path $mod.FullName "Defs"
    if (-not (Test-Path $scanRoot)) {
        Write-Host " (no Defs folder, skipped)"
        continue
    }

    $fileCount = 0
    $pairCount = 0

    $xmlFiles = Get-ChildItem $scanRoot -Recurse -Filter "*.xml"
    foreach ($xf in $xmlFiles) {
        $byType = Process-DefFile -FilePath $xf.FullName
        foreach ($defType in $byType.Keys) {
            if (-not $accumulator.ContainsKey($defType)) {
                $accumulator[$defType] = @{}
            }
            $key = "$($mod.Name)__$($xf.BaseName)"
            if (-not $accumulator[$defType].ContainsKey($key)) {
                $accumulator[$defType][$key] = [System.Collections.Generic.List[PSCustomObject]]::new()
            }
            foreach ($p in $byType[$defType]) {
                $accumulator[$defType][$key].Add($p) | Out-Null
                $pairCount++
            }
        }
        $fileCount++
    }

    Write-Host " $fileCount files, $pairCount pairs"
}

# ---------------------------------------------------------------------------
# Write DefInjected output files
# ---------------------------------------------------------------------------
$outDefInjected = Join-Path $OutputMod "Languages\English\DefInjected"
$totalFiles     = 0
$totalPairs     = 0

foreach ($defType in ($accumulator.Keys | Sort-Object)) {
    $typeDir = Join-Path $outDefInjected $defType
    New-Item -ItemType Directory -Path $typeDir -Force | Out-Null

    foreach ($fileName in ($accumulator[$defType].Keys | Sort-Object)) {
        $pairs   = $accumulator[$defType][$fileName]
        $xmlText = Build-DefInjectedXml -Pairs $pairs
        $outPath = Join-Path $typeDir "$fileName.xml"
        [System.IO.File]::WriteAllText($outPath, $xmlText, [System.Text.Encoding]::UTF8)
        $totalFiles++
        $totalPairs += $pairs.Count
    }
}

Write-Host ""
Write-Host "DefInjected: $totalFiles files, $totalPairs translation pairs" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Copy existing Languages/English/Keyed and Strings files verbatim
# ---------------------------------------------------------------------------
$outKeyed   = Join-Path $OutputMod "Languages\English\Keyed"
$outStrings = Join-Path $OutputMod "Languages\English\Strings"
$keyedCopied   = 0
$stringsCopied = 0

foreach ($mod in $fipMods) {
    $engPath = Join-Path $mod.FullName "Languages\English"
    if (-not (Test-Path $engPath)) { continue }

    # Keyed
    $keyedSrc = Join-Path $engPath "Keyed"
    if (Test-Path $keyedSrc) {
        New-Item -ItemType Directory -Path $outKeyed -Force | Out-Null
        Get-ChildItem $keyedSrc -Filter "*.xml" | ForEach-Object {
            $dest = Join-Path $outKeyed "$($mod.Name)__$($_.Name)"
            Copy-Item $_.FullName $dest -Force
            $keyedCopied++
        }
    }

    # Strings (subfolder trees - preserve subdir structure with mod prefix)
    $stringsSrc = Join-Path $engPath "Strings"
    if (Test-Path $stringsSrc) {
        Get-ChildItem $stringsSrc -Recurse -Filter "*.txt" | ForEach-Object {
            $rel      = $_.FullName.Substring($stringsSrc.Length).TrimStart('\')
            $relDir   = Split-Path $rel -Parent
            $destDir  = if ($relDir) { Join-Path $outStrings $relDir } else { $outStrings }
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
            $destFile = Join-Path $destDir "$($mod.Name)__$($_.Name)"
            Copy-Item $_.FullName $destFile -Force
            $stringsCopied++
        }
    }
}

Write-Host "Keyed: $keyedCopied files copied" -ForegroundColor Green
Write-Host "Strings: $stringsCopied files copied" -ForegroundColor Green
Write-Host ""
Write-Host "Done. Output: $OutputMod" -ForegroundColor Cyan
