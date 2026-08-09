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
        try {
            [xml]$loadFolders = [IO.File]::ReadAllText($loadFoldersPath)
            foreach ($node in $loadFolders.SelectNodes('/loadFolders/v1.6/li')) {
                if (Test-Condition $node $Active) {
                    $path = Join-Path $Mod.Path $node.InnerText.Trim().Replace('/', [IO.Path]::DirectorySeparatorChar)
                    if (Test-Path -LiteralPath $path -PathType Container) { $roots.Add([IO.Path]::GetFullPath($path)) }
                }
            }
        }
        catch { }
    }
    else {
        $version = Join-Path $Mod.Path '1.6'
        $common = Join-Path $Mod.Path 'Common'
        if (Test-Path -LiteralPath $version -PathType Container) {
            if (Test-Path -LiteralPath $common -PathType Container) { $roots.Add([IO.Path]::GetFullPath($common)) }
            $roots.Add([IO.Path]::GetFullPath($version))
            if ((Test-Path -LiteralPath (Join-Path $Mod.Path 'Defs')) -or (Test-Path -LiteralPath (Join-Path $Mod.Path 'Patches'))) {
                $roots.Add([IO.Path]::GetFullPath($Mod.Path))
            }
        }
        else { $roots.Add([IO.Path]::GetFullPath($Mod.Path)) }
    }
    return @($roots | Sort-Object -Unique)
}

[xml]$config = [IO.File]::ReadAllText($ConfigPath)
$activeIds = @($config.ModsConfigData.activeMods.li | ForEach-Object { $_.ToLowerInvariant() })
$active = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($id in $activeIds) { [void]$active.Add($id) }

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
            $id = $about.ModMetaData.packageId.Trim()
            if ($id) {
                $mods[$id.ToLowerInvariant()] = [pscustomobject]@{
                    Id = $id
                    Name = $about.ModMetaData.name.Trim()
                    Path = $directory.FullName
                }
            }
        }
        catch { }
    }
}

$tabs = [Collections.Generic.List[object]]::new()
$projects = [Collections.Generic.List[object]]::new()
foreach ($id in $activeIds) {
    if (-not $mods.ContainsKey($id)) { continue }
    $mod = $mods[$id]
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($root in Get-ContentRoots $mod $active) {
        $defs = Join-Path $root 'Defs'
        if (-not (Test-Path -LiteralPath $defs -PathType Container)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $defs -Recurse -Filter *.xml -File) {
            if (-not $seen.Add($file.FullName)) { continue }
            try {
                [xml]$xml = [IO.File]::ReadAllText($file.FullName)
                foreach ($node in $xml.SelectNodes('/Defs/ResearchTabDef')) {
                    $tabs.Add([pscustomobject]@{
                        Position = [array]::IndexOf($activeIds, $id) + 1
                        ModId = $mod.Id
                        ModName = $mod.Name
                        DefName = [string]$node.defName
                        Label = [string]$node.label
                        File = $file.FullName
                    })
                }
                foreach ($node in $xml.SelectNodes('/Defs/ResearchProjectDef')) {
                    $projects.Add([pscustomobject]@{
                        ModId = $mod.Id
                        DefName = [string]$node.defName
                        Label = [string]$node.label
                        Tab = [string]$node.tab
                        X = [string]$node.researchViewX
                        Y = [string]$node.researchViewY
                    })
                }
            }
            catch { }
        }
    }
}

'=== ResearchTabDefs ==='
$tabs | Sort-Object Position, DefName | Format-Table Position, ModId, DefName, Label -AutoSize | Out-String -Width 300
'=== Duplicate tab labels ==='
$tabs | Group-Object Label | Where-Object { $_.Count -gt 1 } | ForEach-Object {
    'LABEL=' + $_.Name
    $_.Group | Format-Table Position, ModId, DefName, Label -AutoSize | Out-String -Width 300
}
'=== Projects referencing missing tabs ==='
$known = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($tab in $tabs) { [void]$known.Add($tab.DefName) }
$projects | Where-Object { $_.Tab -and -not $known.Contains($_.Tab) } | Sort-Object Tab, ModId, DefName | Format-Table ModId, DefName, Label, Tab -AutoSize | Out-String -Width 300
'=== VanillaExpanded projects ==='
$projects | Where-Object { $_.Tab -ieq 'VanillaExpanded' } | Sort-Object {[double]($_.Y -as [double])}, {[double]($_.X -as [double])}, ModId, DefName | Format-Table ModId, DefName, Label, X, Y -AutoSize | Out-String -Width 300
