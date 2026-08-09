param(
    [string]$RimWorldRoot = 'D:\Steam\steamapps\common\RimWorld',
    [string]$ConfigPath = "$env:USERPROFILE\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModsConfig.xml"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-Condition {
    param([Xml.XmlElement]$Node, [Collections.Generic.HashSet[string]]$Active)
    if ($Node.HasAttribute('IfModActive') -and -not $Active.Contains($Node.GetAttribute('IfModActive'))) { return $false }
    if ($Node.HasAttribute('IfModActiveAll')) {
        foreach ($id in $Node.GetAttribute('IfModActiveAll').Split(',')) {
            if (-not $Active.Contains($id.Trim())) { return $false }
        }
    }
    if ($Node.HasAttribute('IfModNotActive') -and $Active.Contains($Node.GetAttribute('IfModNotActive'))) { return $false }
    if ($Node.HasAttribute('IfModNotActiveAll')) {
        $allActive = $true
        foreach ($id in $Node.GetAttribute('IfModNotActiveAll').Split(',')) {
            if (-not $Active.Contains($id.Trim())) { $allActive = $false }
        }
        if ($allActive) { return $false }
    }
    return $true
}

function Get-ContentRoots {
    param([object]$Mod, [Collections.Generic.HashSet[string]]$Active)
    $roots = [Collections.Generic.List[string]]::new()
    $loadFoldersPath = Join-Path $Mod.Path 'LoadFolders.xml'
    if (Test-Path -LiteralPath $loadFoldersPath) {
        [xml]$loadFolders = [IO.File]::ReadAllText($loadFoldersPath)
        foreach ($node in $loadFolders.SelectNodes('/loadFolders/v1.6/li')) {
            if (Test-Condition $node $Active) {
                $path = Join-Path $Mod.Path $node.InnerText.Trim().Replace('/', [IO.Path]::DirectorySeparatorChar)
                if (Test-Path -LiteralPath $path -PathType Container) { $roots.Add([IO.Path]::GetFullPath($path)) }
            }
        }
    }
    else {
        $version = Join-Path $Mod.Path '1.6'
        $common = Join-Path $Mod.Path 'Common'
        if (Test-Path -LiteralPath $version -PathType Container) {
            if (Test-Path -LiteralPath $common -PathType Container) { $roots.Add([IO.Path]::GetFullPath($common)) }
            $roots.Add([IO.Path]::GetFullPath($version))
            if (Test-Path -LiteralPath (Join-Path $Mod.Path 'Defs')) { $roots.Add([IO.Path]::GetFullPath($Mod.Path)) }
        }
        else {
            $roots.Add([IO.Path]::GetFullPath($Mod.Path))
        }
    }
    return @($roots | Sort-Object -Unique)
}

[xml]$config = [IO.File]::ReadAllText($ConfigPath)
$activeIds = @($config.ModsConfigData.activeMods.li | ForEach-Object { $_.ToLowerInvariant() })
$activeSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($id in $activeIds) { [void]$activeSet.Add($id) }

$searchRoots = @(
    (Join-Path $RimWorldRoot 'Data'),
    (Join-Path $RimWorldRoot 'Mods'),
    (Join-Path (Split-Path -Parent (Split-Path -Parent $RimWorldRoot)) 'workshop\content\294100')
)
$mods = @{}
foreach ($root in $searchRoots) {
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
    foreach ($directory in Get-ChildItem -LiteralPath $root -Directory) {
        $aboutPath = Join-Path $directory.FullName 'About\About.xml'
        if (-not (Test-Path -LiteralPath $aboutPath -PathType Leaf)) { continue }
        try {
            [xml]$about = [IO.File]::ReadAllText($aboutPath)
            $packageId = $about.ModMetaData.packageId.Trim()
            if (-not $packageId) { continue }
            $mods[$packageId.ToLowerInvariant()] = [pscustomobject]@{
                Id = $packageId
                Name = $about.ModMetaData.name.Trim()
                Path = $directory.FullName
            }
        }
        catch { }
    }
}

$records = [Collections.Generic.List[object]]::new()
foreach ($id in $activeIds) {
    if (-not $mods.ContainsKey($id)) { continue }
    $mod = $mods[$id]
    foreach ($contentRoot in Get-ContentRoots $mod $activeSet) {
        $defsRoot = Join-Path $contentRoot 'Defs'
        if (-not (Test-Path -LiteralPath $defsRoot -PathType Container)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $defsRoot -File -Recurse -Filter *.xml) {
            try { [xml]$xml = [IO.File]::ReadAllText($file.FullName) } catch { continue }
            foreach ($node in $xml.SelectNodes('/Defs/StorytellerDef[not(@Abstract="True")]')) {
                $defName = if ($node.defName) { $node.defName.Trim() } else { '' }
                if (-not $defName) { continue }
                $visibilityNode = $node.SelectSingleNode('listVisible')
                $records.Add([pscustomobject]@{
                    Position = [array]::IndexOf($activeIds, $id) + 1
                    PackageId = $mod.Id
                    ModName = $mod.Name
                    DefName = $defName
                    Label = if ($node.label) { $node.label.Trim() } else { '' }
                    ListVisible = if ($visibilityNode) { $visibilityNode.InnerText.Trim() -cne 'false' } else { $true }
                    File = $file.FullName
                    IsFCP = $mod.Id -like 'Rick.FCP.*'
                })
            }
        }
    }
}

$records | Sort-Object Position, DefName | Format-Table Position, PackageId, ModName, DefName, Label -Wrap -AutoSize
Write-Output "TOTAL=$($records.Count)"
Write-Output "FCP=$(@($records | Where-Object IsFCP).Count)"
Write-Output "NONFCP=$(@($records | Where-Object { -not $_.IsFCP }).Count)"

$visibilityPatches = [Collections.Generic.List[object]]::new()
foreach ($id in $activeIds) {
    if (-not $mods.ContainsKey($id)) { continue }
    $mod = $mods[$id]
    foreach ($contentRoot in Get-ContentRoots $mod $activeSet) {
        $patchRoot = Join-Path $contentRoot 'Patches'
        if (-not (Test-Path -LiteralPath $patchRoot -PathType Container)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $patchRoot -File -Recurse -Filter *.xml) {
            try { [xml]$xml = [IO.File]::ReadAllText($file.FullName) } catch { continue }
            foreach ($xpath in $xml.SelectNodes('/Patch/Operation[@Class="PatchOperationConditional"]/xpath[contains(., "StorytellerDef") and contains(., "/listVisible")]')) {
                $value = $xpath.InnerText.Trim()
                if ($value -match '^/Defs/StorytellerDef\[defName="([^"]+)"\]/listVisible$') {
                    $visibilityPatches.Add([pscustomobject]@{
                        Position = [array]::IndexOf($activeIds, $id) + 1
                        PackageId = $mod.Id
                        DefName = $Matches[1]
                        XPath = $value
                        File = $file.FullName
                    })
                }
            }
        }
    }
}

foreach ($patch in $visibilityPatches | Sort-Object Position) {
    foreach ($record in $records | Where-Object DefName -CEQ $patch.DefName) { $record.ListVisible = $false }
}
$visible = @($records | Where-Object ListVisible)

Write-Output '--- ACTIVE STORYTELLER VISIBILITY PATCHES ---'
$visibilityPatches | Sort-Object Position, DefName | Format-Table Position, PackageId, DefName -AutoSize
Write-Output '--- SIMULATED VISIBLE STORYTELLERS ---'
$visible | Sort-Object Position, DefName | Format-Table Position, PackageId, DefName, Label -Wrap -AutoSize
Write-Output "VISIBILITY_PATCHES=$($visibilityPatches.Count)"
Write-Output "VISIBLE=$($visible.Count)"
Write-Output "VISIBLE_NONFCP=$(@($visible | Where-Object { -not $_.IsFCP }).Count)"
Write-Output "VISIBLE_FCP=$(@($visible | Where-Object IsFCP).Count)"
