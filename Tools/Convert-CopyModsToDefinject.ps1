param(
    [string]$SourceRoot = "c:\Users\km-fei-mat\source\repos\FIP-Mods",
    [string[]]$ModNames,
    [switch]$KeepOriginalFolderName,
    [switch]$WhatIf
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$translatableLeafNames = @{}
@(
    'label','labelShort','labelMale','labelFemale',
    'labelMalePlural','labelFemalePlural','labelPlural',
    'labelNoun','labelNounPretty',
    'description','descriptionShort','descriptionExtra','baseDesc',
    'title','titleShort','titleFemale','titleShortFemale',
    'baseTitle','baseTitleFemale',
    'leaderTitle','leaderTitleFemale',
    'jobString','reportString','inspectString','baseInspectLine',
    'verb','gerund','gerundLabel','chargeNoun',
    'letterLabel','letterText','letterDesc',
    'beginLetterLabel','beginLetterText',
    'arrivedLetterLabel','arrivedLetterText',
    'pawnsArrivalMessage','joinText',
    'successMessage','successText','failMessage','failText',
    'rejectInputMessage','calledOffMessage','finishedMessage',
    'customLetterLabel','customLetterText','customLabel','customSummary',
    'pawnLabel','pawnSingular','pawnsPlural','pawnPlural','leaderPawnSingular',
    'skillLabel','skillDescription',
    'headerTip','header','tip','helpText','summary','text','note',
    'name','nameNoun','nameSuffix','namePrefix',
    'tooltip','lockedReason','notWorkingKey',
    'failTriggerText','pawnCannotEquipReason'
) | ForEach-Object {
    $translatableLeafNames[$_.ToLowerInvariant()] = $true
}

$stringListParents = @{}
@('rulesStrings') | ForEach-Object {
    $stringListParents[$_.ToLowerInvariant()] = $true
}

$filePathHints = @{
    'backstor' = 'BackstoryDef'
    'trait' = 'TraitDef'
    'social' = 'ThoughtDef'
    'thought' = 'ThoughtDef'
    'psycast' = 'AbilityDef'
    'scenario' = 'ScenarioDef'
    'vehicle' = 'ThingDef'
    'factory' = 'ThingDef'
    'medical' = 'ThingDef'
    'spacer' = 'ThingDef'
    'power' = 'ThingDef'
    'production' = 'ThingDef'
    'security' = 'ThingDef'
    'vending' = 'ThingDef'
    'museum' = 'ThingDef'
}

function Escape-XmlText {
    param([string]$Value)

    $escaped = $Value.Replace('&', '&amp;')
    $escaped = $escaped.Replace('<', '&lt;')
    $escaped = $escaped.Replace('>', '&gt;')
    $escaped = $escaped.Replace('"', '&quot;')
    return $escaped
}

function Format-FriendlyFolderName {
    param([string]$Condition)

    if ([string]::IsNullOrWhiteSpace($Condition)) {
        return 'Base'
    }

    $segments = $Condition.Split(',') | ForEach-Object {
        $trimmed = $_.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            return
        }

        $pieces = $trimmed.Split('.') | Where-Object { $_ }
        if ($pieces.Count -gt 0) {
            $pieces[-1]
        }
        else {
            $trimmed
        }
    }

    $raw = ($segments -join '_')
    $sanitized = [regex]::Replace($raw, '[^A-Za-z0-9_\-]', '_')
    if ([string]::IsNullOrWhiteSpace($sanitized)) {
        return 'Base'
    }

    return $sanitized
}

function Get-UniqueFolderName {
    param(
        [hashtable]$KnownFolders,
        [string]$Condition
    )

    if ($KnownFolders.ContainsKey($Condition)) {
        return $KnownFolders[$Condition]
    }

    $baseName = Format-FriendlyFolderName -Condition $Condition
    $candidate = $baseName
    $counter = 2
    while ($KnownFolders.Values -contains $candidate) {
        $candidate = "$baseName`_$counter"
        $counter++
    }

    $KnownFolders[$Condition] = $candidate
    return $candidate
}

function Get-SingleElementChild {
    param([System.Xml.XmlElement]$Element)

    $children = @($Element.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })
    if ($children.Count -ne 1) {
        return $null
    }

    return $children[0]
}

function Resolve-DefType {
    param(
        [string]$RawDefType,
        [string]$LeafName,
        [string]$SourcePath,
        [string]$RemainderPath,
        [string]$DefName
    )

    if ($RawDefType -and $RawDefType -ne '*') {
        return @{ DefType = $RawDefType; Resolution = 'xpath' }
    }

    $leaf = $LeafName.ToLowerInvariant()
    $remainder = $RemainderPath.ToLowerInvariant()
    $source = $SourcePath.ToLowerInvariant()

    if ($remainder -like 'degreedatas*') {
        return @{ DefType = 'TraitDef'; Resolution = 'heuristic-degreeDatas' }
    }

    if ($remainder -like 'stages*') {
        return @{ DefType = 'ThoughtDef'; Resolution = 'heuristic-stages' }
    }

    if ($remainder -like 'rulesstrings*') {
        return @{ DefType = 'RulePackDef'; Resolution = 'heuristic-rulesStrings' }
    }

    if ($leaf -in @('tooltip', 'lockedreason')) {
        return @{ DefType = 'AbilityDef'; Resolution = 'heuristic-ability' }
    }

    if ($leaf -in @('title', 'titleshort', 'titlefemale', 'titleshortfemale', 'basetitle', 'basetitlefemale', 'basedesc')) {
        return @{ DefType = 'BackstoryDef'; Resolution = 'heuristic-backstory' }
    }

    if ($leaf -in @('pawnsingular', 'pawnsplural', 'leaderpawnsingular', 'leadertitle', 'leadertitlefemale', 'maxconfigurableatworldcreation')) {
        return @{ DefType = 'FactionDef'; Resolution = 'heuristic-faction' }
    }

    if ($leaf -in @('reportstring', 'jobstring', 'gerund', 'gerundlabel', 'verb', 'labelshort')) {
        return @{ DefType = 'JobDef'; Resolution = 'heuristic-job' }
    }

    foreach ($hint in $filePathHints.Keys) {
        if ($source.Contains($hint)) {
            return @{ DefType = $filePathHints[$hint]; Resolution = "path-$hint" }
        }
    }

    if ($DefName -match '(_Blueprint|^Plant_|^Building_|Workbench|Dummy|Target|Table|Barrel|Lamp|Machine|Register|Traveller|Roadrunner|Roadkill|Highwayman|Prowler|Autofarmer|Conveyor|Spring|Sign|Draught)') {
        return @{ DefType = 'ThingDef'; Resolution = 'heuristic-thing' }
    }

    return @{ DefType = 'ThingDef'; Resolution = 'default-thing' }
}

function Convert-XPathToEntry {
    param(
        [string]$XPath,
        [string]$SourcePath
    )

    $normalized = $XPath.Trim()
    if ($normalized.StartsWith('/')) {
        $normalized = $normalized.Substring(1)
    }

    if (-not ($normalized.StartsWith('Defs/'))) {
        return $null
    }

    $normalized = $normalized.Substring(5)
    $segments = @($normalized.Split('/') | Where-Object { $_ -ne '' })
    if (@($segments).Count -lt 2) {
        return $null
    }

    $head = $segments[0]
    if ($head -notmatch '^(?<defType>[^\[]+)(\[defName="(?<defName>[^"]+)"\])?$') {
        return $null
    }

    $rawDefType = $matches['defType']
    $defName = $matches['defName']
    if ([string]::IsNullOrWhiteSpace($defName)) {
        return $null
    }

    $pathParts = New-Object 'System.Collections.Generic.List[string]'
    for ($index = 1; $index -lt $segments.Count; $index++) {
        $segment = $segments[$index]

        if ($segment -match '^(?<name>[^\[]+)\[(?<predicate>.+)\]$') {
            $name = $matches['name']
            $predicate = $matches['predicate']
            if ($name -eq 'li') {
                if ($predicate -match '^\d+$') {
                    $pathParts.Add(([int]$predicate - 1).ToString()) | Out-Null
                    continue
                }

                return $null
            }

            return $null
        }

        if ($segment -eq 'li') {
            $pathParts.Add('0') | Out-Null
            continue
        }

        $pathParts.Add($segment) | Out-Null
    }

    if ($pathParts.Count -eq 0) {
        return $null
    }

    $leafName = $pathParts[$pathParts.Count - 1]
    $leafKey = $leafName.ToLowerInvariant()
    $parentName = if ($pathParts.Count -gt 1) { $pathParts[$pathParts.Count - 2].ToLowerInvariant() } else { '' }

    if (-not $translatableLeafNames.ContainsKey($leafKey) -and -not $stringListParents.ContainsKey($leafKey) -and -not $stringListParents.ContainsKey($parentName)) {
        return $null
    }

    $resolved = Resolve-DefType -RawDefType $rawDefType -LeafName $leafName -SourcePath $SourcePath -RemainderPath (($pathParts -join '.')) -DefName $defName
    return @{
        DefType = $resolved.DefType
        DefName = $defName
        PathParts = @($pathParts)
        Resolution = $resolved.Resolution
    }
}

function Get-EntryValues {
    param(
        [System.Xml.XmlElement]$ValueElement,
        [hashtable]$PathInfo
    )

    $pathParts = @($PathInfo.PathParts)
    $leafName = $pathParts[-1]
    $singleChild = Get-SingleElementChild -Element $ValueElement
    if ($null -eq $singleChild) {
        return $null
    }

    $entries = New-Object 'System.Collections.Generic.List[object]'
    if ($stringListParents.ContainsKey($leafName.ToLowerInvariant())) {
        $liIndex = 0
        foreach ($child in @($singleChild.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })) {
            if ($child.LocalName -ne 'li') {
                return $null
            }

            $listPath = @($pathParts[0..($pathParts.Count - 2)] + $liIndex.ToString())
            $entries.Add([pscustomobject]@{
                Key = "$($PathInfo.DefName).$($listPath -join '.')"
                Value = $child.InnerText.Trim()
            }) | Out-Null
            $liIndex++
        }

        return $entries.ToArray()
    }

    if ($singleChild.LocalName -ne $leafName) {
        return $null
    }

    $entries.Add([pscustomobject]@{
        Key = "$($PathInfo.DefName).$($pathParts -join '.')"
        Value = $singleChild.InnerText.Trim()
    }) | Out-Null
    return $entries.ToArray()
}

function Get-EffectiveMayRequire {
    param([System.Xml.XmlElement]$Node)

    $conditions = New-Object 'System.Collections.Generic.List[string]'
    $current = $Node
    while ($null -ne $current) {
        if ($current -is [System.Xml.XmlElement]) {
            $mayRequire = $current.GetAttribute('MayRequire')
            if (-not [string]::IsNullOrWhiteSpace($mayRequire)) {
                $conditions.Add($mayRequire.Trim()) | Out-Null
            }
        }
        $current = $current.ParentNode
    }

    if ($conditions.Count -eq 0) {
        return ''
    }

    return ($conditions | Select-Object -Unique) -join ','
}

function Collect-ConvertibleOperations {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$SourcePath,
        [System.Collections.Generic.List[object]]$Results
    )

    foreach ($child in @($Node.ChildNodes)) {
        if ($child -isnot [System.Xml.XmlElement]) {
            continue
        }

        $className = $child.GetAttribute('Class')
        if (($child.LocalName -eq 'Operation' -or $child.LocalName -eq 'li') -and $className -eq 'PatchOperationReplace') {
            $xpathElement = $child.SelectSingleNode('./xpath')
            $valueElement = $child.SelectSingleNode('./value')
            if ($null -ne $xpathElement -and $null -ne $valueElement) {
                $pathInfo = Convert-XPathToEntry -XPath $xpathElement.InnerText -SourcePath $SourcePath
                if ($null -ne $pathInfo) {
                    $entries = Get-EntryValues -ValueElement $valueElement -PathInfo $pathInfo
                    if ($null -ne $entries -and @($entries).Count -gt 0) {
                        $Results.Add([pscustomobject]@{
                            Node = $child
                            PathInfo = $pathInfo
                            Entries = @($entries)
                            MayRequire = Get-EffectiveMayRequire -Node $child
                        }) | Out-Null
                    }
                }
            }
        }

        Collect-ConvertibleOperations -Node $child -SourcePath $SourcePath -Results $Results
    }
}

function Remove-OperationNode {
    param([System.Xml.XmlElement]$Node)

    $parent = $Node.ParentNode
    if ($null -ne $parent) {
        [void]$parent.RemoveChild($Node)
    }
}

function Test-DocumentHasOperations {
    param([xml]$Document)

    return $null -ne $Document.SelectSingleNode('//*[@Class]')
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        [void](New-Item -Path $Path -ItemType Directory -Force)
    }
}

function Write-LanguageFile {
    param(
        [string]$Path,
        [System.Collections.Generic.List[object]]$Entries
    )

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    [void]$builder.AppendLine('<LanguageData>')

    $lastDefName = $null
    foreach ($entry in $Entries) {
        $dotIndex = $entry.Key.IndexOf('.')
        $defName = if ($dotIndex -gt 0) { $entry.Key.Substring(0, $dotIndex) } else { $entry.Key }
        if ($defName -ne $lastDefName) {
            if ($null -ne $lastDefName) {
                [void]$builder.AppendLine()
            }

            [void]$builder.AppendLine("  <!-- $defName -->")
            $lastDefName = $defName
        }

        $escapedValue = Escape-XmlText -Value $entry.Value
        [void]$builder.AppendLine("  <$($entry.Key)>$escapedValue</$($entry.Key)>")
    }

    [void]$builder.AppendLine('</LanguageData>')
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($Path, $builder.ToString(), $utf8NoBom)
}

function Update-AboutFile {
    param([string]$AboutPath)

    [xml]$aboutXml = Get-Content -LiteralPath $AboutPath -Raw -Encoding UTF8
    if ($null -eq $aboutXml.ModMetaData) {
        return
    }

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($AboutPath, $aboutXml.OuterXml, $utf8NoBom)
}

function Write-LoadFoldersFile {
    param(
        [string]$ModRoot,
        [hashtable]$FolderMap
    )

    $builder = New-Object System.Text.StringBuilder
    [void]$builder.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    [void]$builder.AppendLine('<loadFolders>')
    [void]$builder.AppendLine('  <v1.6>')
    [void]$builder.AppendLine('    <li>LoadFolders/Base</li>')

    foreach ($condition in ($FolderMap.Keys | Where-Object { $_ } | Sort-Object)) {
        $folderName = $FolderMap[$condition]
        [void]$builder.AppendLine("    <li IfModActive=`"$condition`">LoadFolders/$folderName</li>")
    }

    [void]$builder.AppendLine('  </v1.6>')
    [void]$builder.AppendLine('</loadFolders>')

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText((Join-Path $ModRoot 'LoadFolders.xml'), $builder.ToString(), $utf8NoBom)
}

function Move-BaseContent {
    param([string]$ModRoot)

    $loadFoldersRoot = Join-Path $ModRoot 'LoadFolders'
    $baseRoot = Join-Path $loadFoldersRoot 'Base'
    Ensure-Directory -Path $baseRoot

    $items = Get-ChildItem -LiteralPath $ModRoot | Where-Object {
        $_.Name -notin @('About', 'LoadFolders', 'LoadFolders.xml')
    }

    foreach ($item in $items) {
        $destination = Join-Path $baseRoot $item.Name
        if ($WhatIf) {
            Write-Host "[WhatIf] Move $($item.FullName) -> $destination"
            continue
        }

        Move-Item -LiteralPath $item.FullName -Destination $destination -Force
    }

    return $baseRoot
}

function Convert-PatchFiles {
    param(
        [string]$ModRoot,
        [hashtable]$FolderMap,
        [System.Collections.Generic.List[object]]$ReportRows
    )

    $baseRoot = Join-Path $ModRoot 'LoadFolders\Base'
    $patchRoot = Join-Path $baseRoot 'Patches'
    if (-not (Test-Path -LiteralPath $patchRoot)) {
        return
    }

    $languageAccumulator = @{}
    $patchFiles = Get-ChildItem -LiteralPath $patchRoot -Recurse -Filter '*.xml' | Sort-Object FullName
    foreach ($patchFile in $patchFiles) {
        [xml]$patchXml = Get-Content -LiteralPath $patchFile.FullName -Raw -Encoding UTF8
        $convertible = New-Object 'System.Collections.Generic.List[object]'
        Collect-ConvertibleOperations -Node $patchXml -SourcePath $patchFile.FullName -Results $convertible

        if ($convertible.Count -eq 0) {
            continue
        }

        foreach ($item in $convertible) {
            $folderName = Get-UniqueFolderName -KnownFolders $FolderMap -Condition $item.MayRequire
            if (-not $languageAccumulator.ContainsKey($folderName)) {
                $languageAccumulator[$folderName] = @{}
            }

            $defType = $item.PathInfo.DefType
            if (-not $languageAccumulator[$folderName].ContainsKey($defType)) {
                $languageAccumulator[$folderName][$defType] = @{}
            }

            $targetStem = [System.IO.Path]::GetFileNameWithoutExtension($patchFile.Name)
            $targetStem = $targetStem -replace 'Patch$',''
            if (-not $languageAccumulator[$folderName][$defType].ContainsKey($targetStem)) {
                $languageAccumulator[$folderName][$defType][$targetStem] = New-Object 'System.Collections.Generic.List[object]'
            }

            foreach ($entry in $item.Entries) {
                if (-not [string]::IsNullOrWhiteSpace($entry.Value)) {
                    $languageAccumulator[$folderName][$defType][$targetStem].Add($entry) | Out-Null
                }
            }

            $ReportRows.Add([pscustomobject]@{
                PatchFile = $patchFile.FullName
                DefType = $defType
                Resolution = $item.PathInfo.Resolution
                MayRequire = $item.MayRequire
                EntryCount = $item.Entries.Count
            }) | Out-Null

            if (-not $WhatIf) {
                Remove-OperationNode -Node $item.Node
            }
        }

        if (-not $WhatIf) {
            if (Test-DocumentHasOperations -Document $patchXml) {
                $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
                [System.IO.File]::WriteAllText($patchFile.FullName, $patchXml.OuterXml, $utf8NoBom)
            }
            else {
                Remove-Item -LiteralPath $patchFile.FullName -Force
            }
        }
    }

    if ($WhatIf) {
        return
    }

    foreach ($folderName in $languageAccumulator.Keys) {
        foreach ($defType in $languageAccumulator[$folderName].Keys) {
            $languageDir = Join-Path $ModRoot "LoadFolders\$folderName\Languages\English\DefInjected\$defType"
            Ensure-Directory -Path $languageDir

            foreach ($stem in $languageAccumulator[$folderName][$defType].Keys) {
                $entries = $languageAccumulator[$folderName][$defType][$stem] |
                    Sort-Object Key -Unique
                Write-LanguageFile -Path (Join-Path $languageDir "$stem.xml") -Entries ([System.Collections.Generic.List[object]]$entries)
            }
        }
    }
}

function Rename-ModFolder {
    param([string]$ModRoot)

    if ($KeepOriginalFolderName) {
        return $ModRoot
    }

    $parent = Split-Path -Parent $ModRoot
    $leaf = Split-Path -Leaf $ModRoot
    if ($leaf -notlike '* copy') {
        return $ModRoot
    }

    $newLeaf = ($leaf -replace ' copy$', ' Definject')
    $newPath = Join-Path $parent $newLeaf
    if ($WhatIf) {
        Write-Host "[WhatIf] Rename $ModRoot -> $newPath"
        return $newPath
    }

    Rename-Item -LiteralPath $ModRoot -NewName $newLeaf
    return $newPath
}

$candidateMods = Get-ChildItem -LiteralPath $SourceRoot -Directory | Where-Object {
    $_.Name -like 'FIP-* copy'
}

if ($ModNames -and $ModNames.Count -gt 0) {
    $candidateMods = $candidateMods | Where-Object { $ModNames -contains $_.Name }
}

if (-not $candidateMods) {
    throw 'No matching copy mods found.'
}

$summaryRows = New-Object 'System.Collections.Generic.List[object]'
foreach ($mod in ($candidateMods | Sort-Object Name)) {
    $workingRoot = $mod.FullName
    $aboutPath = Join-Path $workingRoot 'About\About.xml'
    if (-not (Test-Path -LiteralPath $aboutPath)) {
        Write-Warning "Skipping $($mod.Name): missing About\\About.xml"
        continue
    }

    $folderMap = @{}
    $folderMap[''] = 'Base'
    $reportRows = New-Object 'System.Collections.Generic.List[object]'

    if (-not $WhatIf) {
        Update-AboutFile -AboutPath $aboutPath
        [void](Move-BaseContent -ModRoot $workingRoot)
        Convert-PatchFiles -ModRoot $workingRoot -FolderMap $folderMap -ReportRows $reportRows
        Write-LoadFoldersFile -ModRoot $workingRoot -FolderMap $folderMap
    }

    $newRoot = Rename-ModFolder -ModRoot $workingRoot
    $convertedEntries = ($reportRows | Measure-Object EntryCount -Sum).Sum
    if ($null -eq $convertedEntries) {
        $convertedEntries = 0
    }

    $summaryRows.Add([pscustomobject]@{
        Original = $mod.Name
        Current = Split-Path -Leaf $newRoot
        ConvertedEntries = $convertedEntries
        PatchFilesTouched = @($reportRows | Select-Object -ExpandProperty PatchFile -Unique).Count
        ConditionalFolders = @($folderMap.Keys | Where-Object { $_ }).Count
    }) | Out-Null
}

$reportPath = Join-Path $SourceRoot 'Tools\copy-to-definject-report.json'
if (-not $WhatIf) {
    $summaryRows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $reportPath -Encoding UTF8
}

$summaryRows | Format-Table -AutoSize | Out-String | Write-Host