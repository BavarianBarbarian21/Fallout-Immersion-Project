<#
.SYNOPSIS
    Generates a dummy English DefInjected translation mod for all FCP mods.
    Every translatable text node is copied verbatim (1:1 English source).
.NOTES
    Run from any directory. Output goes to FIP-Translation Part I.
#>

param(
    [string]$FcpRoot  = "C:\Users\Matthias\Desktop\Fallout Collaboration Project",
    [string]$OutputMod = "C:\Users\Matthias\Desktop\Fallout Immersion Project\FIP-Translation Part I"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Translatable leaf-node names (elements that contain a human-readable string
# and have NO child elements).  Case-insensitive lookup via hashtable.
# ---------------------------------------------------------------------------
$TranslatableLeafNames = @{}
@(
    # Common
    'label','labelShort','labelMale','labelFemale',
    'labelMalePlural','labelFemalePlural','labelPlural',
    'description','descriptionShort','baseDesc',
    # Titles / roles
    'title','titleShort','titleFemale','titleShortFemale',
    'leaderTitle','leaderTitleFemale',
    # Job / inspect strings
    'jobString','reportString','inspectString','baseInspectLine',
    # Verb / tool strings
    'verb','chargeNoun',
    # Letters / signals
    'letterLabel','letterText','letterDesc',
    'beginLetterLabel','beginLetterText',
    'arrivedLetterLabel','arrivedLetterText',
    'pawnsArrivalMessage','joinText',
    # Outcome messages
    'successMessage','successText','failMessage','failText',
    'rejectInputMessage',
    # Custom labels
    'customLetterLabel','customLetterText',
    'gerundLabel','customLabel',
    # Pawn labels
    'pawnLabel','pawnSingular','pawnPlural',
    # Skill
    'skillLabel','skillDescription',
    # Misc UI
    'headerTip','tip','helpText','summary',
    'text','note',
    # Name rules
    'nameNoun','nameSuffix','namePrefix',
    # Other known text fields
    'failTriggerText','pawnCannotEquipReason',
    'descriptionExtra','labelNounPretty',
    'customSummary','extraTooltip',
    'header',
    # Backstory specific
    'baseTitle','baseTitleFemale','baseDesc',
    # Faction / ideology
    'pawnsPlural','leaderPawnSingular',
    # Scenario
    'name',   # scenario name / faction name are translatable
    'summary' # scenario summary
) | ForEach-Object {
    $TranslatableLeafNames[$_.ToLowerInvariant()] = $true
}

# ---------------------------------------------------------------------------
# List-parent element names whose direct <li> children are plain strings
# (as opposed to object records).  Each <li> gets a numeric index path.
# ---------------------------------------------------------------------------
$StringListParents = @{}
@('rulesStrings') | ForEach-Object { $StringListParents[$_.ToLowerInvariant()] = $true }

# ---------------------------------------------------------------------------
# Recursive walker
# Returns a List of [PSCustomObject]@{Path; Value}
# PathPrefix: dot-path built so far, NOT including a trailing dot
# IsStringListParent: caller says my <li> children are direct strings
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

        $cName  = $child.LocalName
        $isLi   = ($cName -eq 'li')

        # Build this node's path
        if ($isLi) {
            $cPath = "${PathPrefix}.${liIndex}"
            $liIndex++
        } else {
            $cPath = if ($PathPrefix) { "${PathPrefix}.${cName}" } else { $cName }
        }

        # Does this node have any XML-element children?
        $childElems = @($child.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })
        $hasChildElems = $childElems.Count -gt 0

        if ($IsStringListParent -and $isLi) {
            # <li> directly inside rulesStrings (or other string-list parents):
            # the entire text content is one translatable string
            $text = $child.InnerText.Trim()
            if ($text) {
                $results.Add([PSCustomObject]@{ Path = $cPath; Value = $text }) | Out-Null
            }
        }
        elseif (-not $hasChildElems) {
            # Leaf node – translate only if the element name is a known translatable field
            $key = $cName.ToLowerInvariant()
            if ($TranslatableLeafNames.ContainsKey($key)) {
                $text = $child.InnerText.Trim()
                if ($text) {
                    $results.Add([PSCustomObject]@{ Path = $cPath; Value = $text }) | Out-Null
                }
            }
        }
        else {
            # Container node – recurse
            $childIsStringList = $StringListParents.ContainsKey($cName.ToLowerInvariant())
            $subResults = Get-TranslatablePairs -Element $child -PathPrefix $cPath -IsStringListParent $childIsStringList
            foreach ($p in $subResults) { $results.Add($p) | Out-Null }
        }
    }

    return , $results
}

# ---------------------------------------------------------------------------
# Escape a string for XML text content
# ---------------------------------------------------------------------------
function Escape-Xml ([string]$s) {
    $s = $s.Replace('&', '&amp;')
    $s = $s.Replace('<', '&lt;')
    $s = $s.Replace('>', '&gt;')
    $s = $s.Replace('"', '&quot;')
    return $s
}

# ---------------------------------------------------------------------------
# Process one Def XML file.
# Returns a hashtable: defType -> list of [PSCustomObject]@{DefName;Path;Value}
# ---------------------------------------------------------------------------
function Process-DefFile {
    param([string]$FilePath)

    $byType = @{}

    try {
        $raw = [System.IO.File]::ReadAllText($FilePath, [System.Text.Encoding]::UTF8)
        # Fix unescaped & that are not already part of a valid XML entity
        $raw = [regex]::Replace($raw, '&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;|\w+;)', '&amp;')
        $xml = [xml]$raw
    } catch {
        Write-Warning "Skipping (parse error): $FilePath`n  $_"
        return $byType
    }

    $defsNode = $xml.Defs
    if (-not $defsNode) { return $byType }

    foreach ($defNode in $defsNode.ChildNodes) {
        if ($defNode -isnot [System.Xml.XmlElement]) { continue }

        # Skip abstract defs – they don't exist as runtime objects
        $abstractAttr = $defNode.GetAttribute('Abstract')
        if ($abstractAttr -ieq 'True') { continue }

        $defType = $defNode.LocalName
        $defName = $defNode.defName
        if (-not $defName) { continue }

        # Walk the def for translatable pairs
        $pairs = Get-TranslatablePairs -Element $defNode -PathPrefix $defName

        if ($pairs.Count -eq 0) { continue }

        if (-not $byType.ContainsKey($defType)) {
            $byType[$defType] = [System.Collections.Generic.List[PSCustomObject]]::new()
        }
        foreach ($p in $pairs) {
            $byType[$defType].Add($p) | Out-Null
        }
    }

    return $byType
}

# ---------------------------------------------------------------------------
# Build the XML text for one DefInjected file
# ---------------------------------------------------------------------------
function Build-DefInjectedXml {
    param([System.Collections.Generic.List[PSCustomObject]]$Pairs)

    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
    [void]$sb.AppendLine('<LanguageData>')

    $lastDefName = $null
    foreach ($p in $Pairs) {
        # Extract defName prefix (everything before the first dot)
        $dot = $p.Path.IndexOf('.')
        $dn  = if ($dot -ge 0) { $p.Path.Substring(0, $dot) } else { $p.Path }
        if ($dn -ne $lastDefName) {
            if ($lastDefName -ne $null) { [void]$sb.AppendLine() }
            [void]$sb.AppendLine("  <!-- $dn -->")
            $lastDefName = $dn
        }

        $tag   = $p.Path
        $value = Escape-Xml $p.Value
        # Multi-line values get CDATA
        if ($p.Value -match "`n") {
            $inner = $p.Value.Replace(']]>', ']]&gt;')
            [void]$sb.AppendLine("  <$tag><![CDATA[$inner]]></$tag>")
        } else {
            [void]$sb.AppendLine("  <$tag>$value</$tag>")
        }
    }

    [void]$sb.AppendLine('</LanguageData>')
    return $sb.ToString()
}

# ===========================================================================
# MAIN
# ===========================================================================

Write-Host "=== FCP → FIP-Translation Part I generator ===" -ForegroundColor Cyan

# Collect FCP mod folders
$fcpMods = Get-ChildItem $FcpRoot -Directory -Filter "FCP-*" | Sort-Object Name
Write-Host "Found $($fcpMods.Count) FCP mods."

# Master accumulator: defType -> filename -> List<pair>
# We'll create one output file per (defType, sourceFileName) pair
$accumulator = @{}   # key = defType, value = hashtable( baseName -> List<pair> )

foreach ($mod in $fcpMods) {
    Write-Host "  Processing $($mod.Name)..." -NoNewline

    # Scan 1.6/Defs and Common/Defs
    $scanRoots = @()
    $r16   = Join-Path $mod.FullName "1.6\Defs"
    $rComm = Join-Path $mod.FullName "Common\Defs"
    if (Test-Path $r16)   { $scanRoots += $r16 }
    if (Test-Path $rComm) { $scanRoots += $rComm }

    $fileCount  = 0
    $pairCount  = 0

    foreach ($scanRoot in $scanRoots) {
        $xmlFiles = Get-ChildItem $scanRoot -Recurse -Filter "*.xml"
        foreach ($xf in $xmlFiles) {
            $byType = Process-DefFile -FilePath $xf.FullName
            foreach ($defType in $byType.Keys) {
                if (-not $accumulator.ContainsKey($defType)) {
                    $accumulator[$defType] = @{}
                }
                $baseName = $xf.BaseName
                # If the same filename exists in multiple mods, prefix with mod name
                $key = "$($mod.Name)__$baseName"
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
        $pairs    = $accumulator[$defType][$fileName]
        $xmlText  = Build-DefInjectedXml -Pairs $pairs
        $outPath  = Join-Path $typeDir "$fileName.xml"
        [System.IO.File]::WriteAllText($outPath, $xmlText, [System.Text.Encoding]::UTF8)
        $totalFiles++
        $totalPairs += $pairs.Count
    }
}

Write-Host ""
Write-Host "DefInjected: $totalFiles files, $totalPairs translation pairs" -ForegroundColor Green

# ---------------------------------------------------------------------------
# Copy existing Keyed files from FCP mods (verbatim – they ARE the English source)
# ---------------------------------------------------------------------------
$outKeyed    = Join-Path $OutputMod "Languages\English\Keyed"
$keyedCopied = 0

foreach ($mod in $fcpMods) {
    # Check both Common/Languages/English/Keyed and 1.6/Languages/English/Keyed
    @(
        (Join-Path $mod.FullName "Common\Languages\English\Keyed"),
        (Join-Path $mod.FullName "1.6\Languages\English\Keyed")
    ) | Where-Object { Test-Path $_ } | ForEach-Object {
        $srcDir = $_
        Get-ChildItem $srcDir -Filter "*.xml" | ForEach-Object {
            $destName = "$($mod.Name)__$($_.Name)"
            $destPath = Join-Path $outKeyed $destName
            New-Item -ItemType Directory -Path $outKeyed -Force | Out-Null
            Copy-Item $_.FullName $destPath -Force
            $keyedCopied++
        }
    }
}

Write-Host "Keyed: $keyedCopied files copied" -ForegroundColor Green
Write-Host ""
Write-Host "Done. Output: $OutputMod" -ForegroundColor Cyan
