param(
    [string]$Root = "c:\Users\Matthias\Desktop\Fallout Immersion Project\.Finalized",
    [string]$AuditCsvPath
)

Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

function Escape-XmlText {
    param([string]$Text)

    if ($null -eq $Text) {
        return ''
    }

    return [System.Security.SecurityElement]::Escape($Text)
}

function Get-DefInjectedEntries {
    param([string]$FilePath)

    [xml]$xml = Get-Content $FilePath -Raw
    $entries = @{}

    if (-not $xml.LanguageData) {
        return $entries
    }

    foreach ($child in $xml.LanguageData.ChildNodes) {
        if ($child.NodeType -ne [System.Xml.XmlNodeType]::Element) {
            continue
        }

        $entries[$child.Name] = $child.InnerText
    }

    return $entries
}

function Convert-KeyToPatchData {
    param(
        [string]$Key,
        [string]$Value
    )

    $parts = $Key.Split('.')
    if ($parts.Length -lt 2) {
        return $null
    }

    $defName = $parts[0]
    $fieldParts = @($parts[1..($parts.Length - 1)])
    $valueTag = $fieldParts[$fieldParts.Length - 1]
    $xpath = $null

    if ($fieldParts[0] -eq 'nodes' -and $fieldParts.Length -eq 3) {
        $nodeKey = $fieldParts[1]
        $xpath = '/Defs/*[defName="{0}"]/nodes/li[key="{1}"]/{2}' -f $defName, $nodeKey, $valueTag
    }
    elseif ($fieldParts[0] -eq 'stages' -and $fieldParts.Length -eq 3) {
        $index = 1
        if ($fieldParts[1] -match '^li_(\d+)_$') {
            $index = [int]$Matches[1]
        }
        $xpath = '/Defs/*[defName="{0}"]/stages/li[{1}]/{2}' -f $defName, $index, $valueTag
    }
    else {
        $xpath = '/Defs/*[defName="{0}"]/{1}' -f $defName, ($fieldParts -join '/')
    }

    return [pscustomobject]@{
        XPath = $xpath
        ValueTag = $valueTag
        Value = $Value
    }
}

function Get-MayRequireForFile {
    param(
        [string]$ModName,
        [string]$RelativeFile,
        [string]$BaseName,
        [string]$ExistingPatchPath
    )

    if ($ModName -eq 'FIP-Lucky 38') {
        $map = @{
            'Lucky38_CashRegister' = 'Orion.CashRegister'
            'Lucky38_DBH' = 'Dubwise.DubsBadHygiene'
            'Lucky38_Gastronomy' = 'Orion.Gastronomy'
            'Lucky38_MechanoidWaiter' = 'GonDragon.MechanoidWaiter'
            'Lucky38_Museums' = 'Nightmare.Museums'
            'Lucky38_SmallHotSpring' = 'zal.smallhotspring'
            'Lucky38_Spa' = 'Adamas.HospitalitySpa'
            'Lucky38_Therapy' = 'Orion.Therapy'
            'Lucky38_Vending' = 'Adamas.VendingMachines'
        }

        if ($map.ContainsKey($BaseName)) {
            return $map[$BaseName]
        }
    }

    if ($ExistingPatchPath -and (Test-Path $ExistingPatchPath)) {
        $raw = Get-Content $ExistingPatchPath -Raw
        $match = [regex]::Match($raw, 'MayRequire="([^"]+)"')
        if ($match.Success) {
            return $match.Groups[1].Value
        }
    }

    return $null
}

function Get-TargetPatchInfo {
    param(
        [string]$ModRoot,
        [string]$ModName,
        [string]$RelativeFile
    )

    $leaf = Split-Path $RelativeFile -Leaf
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($leaf)
    $searchName = if ($baseName.StartsWith('Patch_')) { $baseName.Substring(6) + '.xml' } else { $leaf }

    $existingPatch = Get-ChildItem (Join-Path $ModRoot 'Patches') -Recurse -File -Filter $searchName -ErrorAction SilentlyContinue | Select-Object -First 1
    $localizationDir = Join-Path $ModRoot 'Patches\Localization'
    $targetLeaf = '{0}_Localization.xml' -f $baseName
    $targetPath = Join-Path $localizationDir $targetLeaf
    if ($baseName.StartsWith('Patch_')) {
        $lookupBaseName = $baseName.Substring(6)
    }
    else {
        $lookupBaseName = $baseName
    }
    $existingPatchPath = $null
    if ($existingPatch) {
        $existingPatchPath = $existingPatch.FullName
    }
    $mayRequire = Get-MayRequireForFile -ModName $ModName -RelativeFile $RelativeFile -BaseName $lookupBaseName -ExistingPatchPath $existingPatchPath

    return [pscustomobject]@{
        TargetPath = $targetPath
        MayRequire = $mayRequire
    }
}

function Write-PatchFile {
    param(
        [string]$TargetPath,
        [string]$MayRequire,
        [System.Collections.IEnumerable]$Operations
    )

    $dir = Split-Path $TargetPath -Parent
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('<?xml version="1.0" encoding="utf-8"?>')
    $lines.Add('<Patch>')

    if ($MayRequire) {
        $lines.Add(('  <Operation Class="PatchOperationSequence" MayRequire="{0}">' -f $MayRequire))
        $lines.Add('    <success>Always</success>')
        $lines.Add('    <operations>')
        foreach ($operation in $Operations) {
            $lines.Add('      <li Class="PatchOperationReplace">')
            $lines.Add(('        <xpath>{0}</xpath>' -f $operation.XPath))
            $lines.Add('        <value>')
            $lines.Add(('          <{0}>{1}</{0}>' -f $operation.ValueTag, (Escape-XmlText $operation.Value)))
            $lines.Add('        </value>')
            $lines.Add('      </li>')
        }
        $lines.Add('    </operations>')
        $lines.Add('  </Operation>')
    }
    else {
        foreach ($operation in $Operations) {
            $lines.Add('  <Operation Class="PatchOperationReplace">')
            $lines.Add(('    <xpath>{0}</xpath>' -f $operation.XPath))
            $lines.Add('    <value>')
            $lines.Add(('      <{0}>{1}</{0}>' -f $operation.ValueTag, (Escape-XmlText $operation.Value)))
            $lines.Add('    </value>')
            $lines.Add('  </Operation>')
        }
    }

    $lines.Add('</Patch>')
    [System.IO.File]::WriteAllLines($TargetPath, $lines)
}

if (-not $AuditCsvPath) {
    throw 'AuditCsvPath is required.'
}

$auditRows = Import-Csv $AuditCsvPath | Where-Object { $_.InDefs -ne 'True' -and $_.InKeyed -ne 'True' }
$generatedFiles = @()

$rowsByModAndFile = $auditRows | Group-Object Mod, File
foreach ($group in $rowsByModAndFile) {
    $sample = $group.Group[0]
    $modName = $sample.Mod
    $relativeFile = $sample.File
    $modRoot = Join-Path $Root $modName
    $fullDefInjectedPath = Join-Path $modRoot $relativeFile

    if (-not (Test-Path $fullDefInjectedPath)) {
        continue
    }

    $entryMap = Get-DefInjectedEntries -FilePath $fullDefInjectedPath
    $patchInfo = Get-TargetPatchInfo -ModRoot $modRoot -ModName $modName -RelativeFile $relativeFile
    $operations = New-Object System.Collections.Generic.List[object]

    foreach ($row in $group.Group) {
        if (-not $entryMap.ContainsKey($row.Key)) {
            continue
        }

        $patchData = Convert-KeyToPatchData -Key $row.Key -Value $entryMap[$row.Key]
        if ($null -ne $patchData) {
            $operations.Add($patchData)
        }
    }

    if ($operations.Count -gt 0) {
        Write-PatchFile -TargetPath $patchInfo.TargetPath -MayRequire $patchInfo.MayRequire -Operations $operations
        $generatedFiles += $patchInfo.TargetPath
    }
}

$languageDirs = Get-ChildItem $Root -Directory | Where-Object { $_.Name -ne 'FIP-Translation Part 1' -and $_.Name -ne 'Tools' } | ForEach-Object {
    Join-Path $_.FullName 'Languages\English\DefInjected'
}

foreach ($dir in $languageDirs) {
    if (Test-Path $dir) {
        Remove-Item $dir -Recurse -Force
    }
}

Write-Output ('Generated patch files: {0}' -f $generatedFiles.Count)
$generatedFiles | Sort-Object | ForEach-Object { Write-Output $_ }