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
            $packageNode = $about.SelectSingleNode('/ModMetaData/packageId')
            if (-not $packageNode) { continue }
            $packageId = $packageNode.InnerText.Trim()
            if (-not $packageId) { continue }
            $nameNode = $about.SelectSingleNode('/ModMetaData/name')
            $mods[$packageId.ToLowerInvariant()] = [pscustomobject]@{
                Id = $packageId
                Name = if ($nameNode) { $nameNode.InnerText.Trim() } else { $packageId }
                Path = $directory.FullName
                Position = [array]::IndexOf($activeIds, $packageId.ToLowerInvariant()) + 1
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
            foreach ($node in $xml.SelectNodes('/Defs/XenotypeDef[not(@Abstract="True") and defName]')) {
                $defName = $node.SelectSingleNode('defName').InnerText.Trim()
                $labelNode = $node.SelectSingleNode('label')
                $hiddenNode = $node.SelectSingleNode('hidden')
                $records.Add([pscustomobject]@{
                    Group = if ($mod.Id -like 'FIP.*') { 'FIP' } elseif ($mod.Id -like 'Rick.FCP.*') { 'FCP' } else { 'Nicht-FIP' }
                    Position = $mod.Position
                    PackageId = $mod.Id
                    ModName = $mod.Name
                    DefName = $defName
                    Label = if ($labelNode) { $labelNode.InnerText.Trim() } else { '' }
                    Hidden = if ($hiddenNode) { $hiddenNode.InnerText.Trim() } else { 'false' }
                    File = $file.FullName
                })
            }
        }
    }
}

foreach ($group in @('FIP', 'FCP', 'Nicht-FIP')) {
    $items = @($records | Where-Object Group -CEQ $group | Sort-Object PackageId, DefName)
    Write-Output "[$group] $($items.Count) XenotypeDefs"
    $items | Format-Table PackageId, ModName, DefName, Label, Hidden -Wrap -AutoSize
}
Write-Output "TOTAL=$($records.Count)"
