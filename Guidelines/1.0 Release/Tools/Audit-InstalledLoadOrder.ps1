param(
    [string]$RimWorldRoot = 'D:\Steam\steamapps\common\RimWorld',
    [string]$ConfigPath = "$env:USERPROFILE\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\ModsConfig.xml",
    [string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'Reports\INSTALLED_LOAD_ORDER_AUDIT.md')
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
            if (Test-Path -LiteralPath (Join-Path $Mod.Path 'Defs')) { $roots.Add([IO.Path]::GetFullPath($Mod.Path)) }
            if (Test-Path -LiteralPath (Join-Path $Mod.Path 'Patches')) { $roots.Add([IO.Path]::GetFullPath($Mod.Path)) }
        }
        else {
            $roots.Add([IO.Path]::GetFullPath($Mod.Path))
        }
    }
    return @($roots | Sort-Object -Unique)
}

function Is-ProjectMod([string]$Id) {
    return $Id -like 'FIP.*' -or $Id -like 'Rick.FCP.*'
}

function Is-GameContent([string]$Id) {
    return $Id -like 'Ludeon.RimWorld*'
}

if (-not (Test-Path -LiteralPath $RimWorldRoot -PathType Container)) { throw "RimWorld root not found: $RimWorldRoot" }
if (-not (Test-Path -LiteralPath $ConfigPath -PathType Leaf)) { throw "ModsConfig not found: $ConfigPath" }

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
            $key = $packageId.ToLowerInvariant()
            $nameNode = $about.SelectSingleNode('/ModMetaData/name')
            $mods[$key] = [pscustomobject]@{
                Id = $packageId
                Name = if ($nameNode) { $nameNode.InnerText.Trim() } else { $packageId }
                Path = $directory.FullName
                Position = [array]::IndexOf($activeIds, $key) + 1
                Dependencies = @($about.SelectNodes('/ModMetaData/modDependencies/li/packageId') | ForEach-Object { $_.InnerText.Trim() })
                LoadAfter = @($about.SelectNodes('/ModMetaData/loadAfter/li | /ModMetaData/forceLoadAfter/li') | ForEach-Object { $_.InnerText.Trim() })
                LoadBefore = @($about.SelectNodes('/ModMetaData/loadBefore/li | /ModMetaData/forceLoadBefore/li') | ForEach-Object { $_.InnerText.Trim() })
                Roots = @()
            }
        }
        catch { }
    }
}

$activeMods = [Collections.Generic.List[object]]::new()
foreach ($id in $activeIds) {
    if (-not $mods.ContainsKey($id)) { continue }
    [void]$activeMods.Add($mods[$id])
}
foreach ($mod in $activeMods) {
    $mod.Roots = @(Get-ContentRoots $mod $activeSet)
}

function Has-DirectOrderEdge {
    param([object]$Consumer, [object]$Owner)
    if (@($Consumer.Dependencies | Where-Object { $_ -ieq $Owner.Id }).Count) { return $true }
    if (@($Consumer.LoadAfter | Where-Object { $_ -ieq $Owner.Id }).Count) { return $true }
    if (@($Owner.LoadBefore | Where-Object { $_ -ieq $Consumer.Id }).Count) { return $true }
    return $false
}

$orderGraph = @{}
function Add-OrderGraphEdge([string]$Earlier, [string]$Later) {
    $earlierKey = $Earlier.ToLowerInvariant()
    $laterKey = $Later.ToLowerInvariant()
    if (-not $orderGraph.ContainsKey($earlierKey)) { $orderGraph[$earlierKey] = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase) }
    [void]$orderGraph[$earlierKey].Add($laterKey)
}
foreach ($consumer in $activeMods) {
    foreach ($ownerId in @($consumer.Dependencies + $consumer.LoadAfter)) {
        if ($activeSet.Contains($ownerId)) { Add-OrderGraphEdge $ownerId $consumer.Id }
    }
    foreach ($laterId in $consumer.LoadBefore) {
        if ($activeSet.Contains($laterId)) { Add-OrderGraphEdge $consumer.Id $laterId }
    }
}
function Has-OrderPath([string]$Earlier, [string]$Later) {
    $target = $Later.ToLowerInvariant()
    $queue = [Collections.Generic.Queue[string]]::new()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $queue.Enqueue($Earlier.ToLowerInvariant())
    while ($queue.Count) {
        $current = $queue.Dequeue()
        if (-not $seen.Add($current)) { continue }
        if ($current -eq $target) { return $true }
        if ($orderGraph.ContainsKey($current)) {
            foreach ($next in $orderGraph[$current]) { $queue.Enqueue($next) }
        }
    }
    return $false
}

$declaredViolations = [Collections.Generic.List[object]]::new()
foreach ($consumer in $activeMods) {
    foreach ($ownerId in @($consumer.Dependencies + $consumer.LoadAfter)) {
        $ownerKey = $ownerId.ToLowerInvariant()
        if ($mods.ContainsKey($ownerKey) -and $mods[$ownerKey].Position -gt 0 -and $mods[$ownerKey].Position -gt $consumer.Position) {
            $declaredViolations.Add([pscustomobject]@{ Consumer = $consumer; Owner = $mods[$ownerKey]; Reason = 'dependency/loadAfter is ordered later' })
        }
    }
    foreach ($laterId in $consumer.LoadBefore) {
        $laterKey = $laterId.ToLowerInvariant()
        if ($mods.ContainsKey($laterKey) -and $mods[$laterKey].Position -gt 0 -and $laterKey -in $activeIds -and $consumer.Position -gt $mods[$laterKey].Position) {
            $declaredViolations.Add([pscustomobject]@{ Consumer = $mods[$laterKey]; Owner = $consumer; Reason = 'loadBefore is ordered earlier' })
        }
    }
}

$defOwners = @{}
$abstractOwners = @{}
$defFiles = [Collections.Generic.List[object]]::new()
$patchFiles = [Collections.Generic.List[object]]::new()
$assemblyFiles = [Collections.Generic.List[object]]::new()
foreach ($mod in $activeMods) {
    $seenFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($root in $mod.Roots) {
        foreach ($kind in @('Defs', 'Patches')) {
            $folder = Join-Path $root $kind
            if (-not (Test-Path -LiteralPath $folder -PathType Container)) { continue }
            foreach ($file in Get-ChildItem -LiteralPath $folder -File -Recurse -Filter *.xml) {
                if (-not $seenFiles.Add($file.FullName)) { continue }
                $record = [pscustomobject]@{ Mod = $mod; File = $file }
                if ($kind -eq 'Defs') { $defFiles.Add($record) } else { $patchFiles.Add($record) }
            }
        }
        $assemblyFolder = Join-Path $root 'Assemblies'
        if (Test-Path -LiteralPath $assemblyFolder -PathType Container) {
            foreach ($file in Get-ChildItem -LiteralPath $assemblyFolder -File -Filter *.dll) {
                if ($seenFiles.Add($file.FullName)) { $assemblyFiles.Add([pscustomobject]@{ Mod = $mod; File = $file }) }
            }
        }
    }
}

foreach ($record in $defFiles) {
    try {
        [xml]$xml = [IO.File]::ReadAllText($record.File.FullName)
        foreach ($node in $xml.SelectNodes('/Defs/*')) {
            $defName = $node.SelectSingleNode('defName')
            if ($defName) {
                $signature = "$($node.LocalName)|$($defName.InnerText.Trim())"
                if (-not $defOwners.ContainsKey($signature)) { $defOwners[$signature] = [Collections.Generic.List[object]]::new() }
                $defOwners[$signature].Add($record.Mod)
            }
            if ($node.Attributes['Name']) {
                $name = $node.Attributes['Name'].Value
                if (-not $abstractOwners.ContainsKey($name)) { $abstractOwners[$name] = [Collections.Generic.List[object]]::new() }
                $abstractOwners[$name].Add($record.Mod)
            }
        }
    }
    catch { }
}

$inferred = [Collections.Generic.List[object]]::new()
$patchRelations = [Collections.Generic.List[object]]::new()
function Add-InferredEdge {
    param([object]$Consumer, [object]$Owner, [string]$Kind, [string]$Target, [string]$File)
    if ($Consumer.Id -ieq $Owner.Id -or (Is-GameContent $Owner.Id)) { return }
    $script:inferred.Add([pscustomobject]@{ Consumer = $Consumer; Owner = $Owner; Kind = $Kind; Target = $Target; File = $File })
}
function Add-PatchRelation {
    param([object]$Consumer, [object]$Owner, [string]$Target, [string]$File)
    if ($Consumer.Id -ieq $Owner.Id -or (Is-GameContent $Owner.Id)) { return }
    $script:patchRelations.Add([pscustomobject]@{ Consumer = $Consumer; Owner = $Owner; Kind = 'Patch XPath'; Target = $Target; File = $File })
}

foreach ($record in $patchFiles) {
    try {
        [xml]$xml = [IO.File]::ReadAllText($record.File.FullName)
        foreach ($xpath in $xml.SelectNodes('//xpath[not(*)]')) {
            $value = $xpath.InnerText.Trim()
            foreach ($match in [regex]::Matches($value, '(?:^|/)Defs/(?<type>[A-Za-z0-9_+.]+)\s*\[[^\]]*?defName\s*=\s*["''](?<name>[^"'']+)["'']')) {
                $signature = "$($match.Groups['type'].Value)|$($match.Groups['name'].Value)"
                if ($defOwners.ContainsKey($signature)) {
                    foreach ($owner in $defOwners[$signature]) { Add-PatchRelation $record.Mod $owner $signature $record.File.FullName }
                }
            }
        }
    }
    catch { }
}

foreach ($record in $defFiles) {
    try {
        [xml]$xml = [IO.File]::ReadAllText($record.File.FullName)
        foreach ($node in $xml.SelectNodes('/Defs/*[@ParentName]')) {
            $parentName = $node.Attributes['ParentName'].Value
            if ($abstractOwners.ContainsKey($parentName)) {
                $owners = @($abstractOwners[$parentName] | Sort-Object Id -Unique)
                $availableLocally = @($owners | Where-Object { $_.Id -ieq $record.Mod.Id -or (Is-GameContent $_.Id) }).Count -gt 0
                if (-not $availableLocally) {
                    $externalOwners = @($owners | Where-Object { $_.Id -ine $record.Mod.Id -and -not (Is-GameContent $_.Id) })
                    if ($externalOwners.Count -eq 1) { Add-InferredEdge $record.Mod $externalOwners[0] 'XML ParentName' $parentName $record.File.FullName }
                }
            }
        }
    }
    catch { }
}

$assemblyOwners = @{}
foreach ($record in $assemblyFiles) {
    try {
        $name = [Reflection.AssemblyName]::GetAssemblyName($record.File.FullName).Name
        if (-not $assemblyOwners.ContainsKey($name)) { $assemblyOwners[$name] = [Collections.Generic.List[object]]::new() }
        $assemblyOwners[$name].Add($record.Mod)
    }
    catch { }
}
foreach ($record in $assemblyFiles) {
    try {
        $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($record.File.FullName)
        foreach ($reference in $assembly.GetReferencedAssemblies()) {
            $owners = @()
            if ($reference.Name -ieq '0Harmony' -and $mods.ContainsKey('brrainz.harmony')) {
                $allHarmonyOwners = @($assemblyOwners[$reference.Name] | Sort-Object Id -Unique)
                if (@($allHarmonyOwners | Where-Object { $_.Id -ieq $record.Mod.Id }).Count) { continue }
                $owners = @($mods['brrainz.harmony'])
            }
            elseif ($assemblyOwners.ContainsKey($reference.Name)) {
                $allOwners = @($assemblyOwners[$reference.Name] | Sort-Object Id -Unique)
                if (@($allOwners | Where-Object { $_.Id -ieq $record.Mod.Id }).Count) { continue }
                $owners = @($allOwners | Where-Object { $_.Id -ine $record.Mod.Id })
            }
            if ($owners.Count -eq 1) { Add-InferredEdge $record.Mod $owners[0] 'Assembly reference' $reference.Name $record.File.FullName }
        }
    }
    catch { }
}

$edgeGroups = @($inferred | Group-Object { "$($_.Consumer.Id.ToLowerInvariant())|$($_.Owner.Id.ToLowerInvariant())" })
$missingEdges = [Collections.Generic.List[object]]::new()
$coveredEdges = [Collections.Generic.List[object]]::new()
foreach ($group in $edgeGroups) {
    $sample = $group.Group[0]
    $entry = [pscustomobject]@{
        Consumer = $sample.Consumer
        Owner = $sample.Owner
        Evidence = @($group.Group | ForEach-Object { "$($_.Kind): $($_.Target) [$([IO.Path]::GetFileName($_.File))]" } | Sort-Object -Unique)
    }
    if (Has-OrderPath $sample.Owner.Id $sample.Consumer.Id) { $coveredEdges.Add($entry) } else { $missingEdges.Add($entry) }
}

$patchGroups = @($patchRelations | Group-Object { "$($_.Consumer.Id.ToLowerInvariant())|$($_.Owner.Id.ToLowerInvariant())" })
$unorderedPatchRelations = [Collections.Generic.List[object]]::new()
foreach ($group in $patchGroups) {
    $sample = $group.Group[0]
    if (Has-OrderPath $sample.Owner.Id $sample.Consumer.Id) { continue }
    $unorderedPatchRelations.Add([pscustomobject]@{
        Consumer = $sample.Consumer
        Owner = $sample.Owner
        Evidence = @($group.Group | ForEach-Object { "$($_.Target) [$([IO.Path]::GetFileName($_.File))]" } | Sort-Object -Unique)
    })
}

$duplicateDefs = @($defOwners.GetEnumerator() | Where-Object { @($_.Value.Id | Sort-Object -Unique).Count -gt 1 })
$missingThirdParty = @($missingEdges | Where-Object { -not (Is-ProjectMod $_.Consumer.Id) })
$missingProject = @($missingEdges | Where-Object { Is-ProjectMod $_.Consumer.Id })
$riskyPatchRelations = @($unorderedPatchRelations | Where-Object { $_.Owner.Position -gt $_.Consumer.Position })
$privateHarmonyProviders = @()
if ($assemblyOwners.ContainsKey('0Harmony')) {
    $privateHarmonyProviders = @($assemblyOwners['0Harmony'] | Where-Object { $_.Id -ine 'brrainz.harmony' } | Sort-Object Id -Unique)
}

$lines = [Collections.Generic.List[string]]::new()
$lines.Add('# Installed load-order audit')
$lines.Add('')
$lines.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')")
$lines.Add('')
$lines.Add("Active mods: $($activeIds.Count); mapped About.xml files: $($activeMods.Count).")
$lines.Add('')
$lines.Add('The audit checks the current active 1.6 content roots, declared dependency/loadAfter/loadBefore order, patch XPath ownership, XML ParentName inheritance and managed assembly references. Ordinary deferred Def cross-references are intentionally not treated as load-order requirements.')
$lines.Add('')
$lines.Add('## Declared order violations')
$lines.Add('')
if (-not $declaredViolations.Count) { $lines.Add('- None in the current auto-sorted ModsConfig.') }
else {
    foreach ($item in $declaredViolations) { $lines.Add(('- `{0}` must load after `{1}`: {2}.' -f $item.Consumer.Id, $item.Owner.Id, $item.Reason)) }
}
$lines.Add('')
$lines.Add('## Missing direct metadata edges in third-party mods')
$lines.Add('')
if (-not $missingThirdParty.Count) { $lines.Add('- None detected from active patch, inheritance or assembly references.') }
else {
    foreach ($item in $missingThirdParty | Sort-Object { $_.Consumer.Position }, { $_.Owner.Position }) {
        $lines.Add(('- **{0}** (`{1}`) should declare `loadAfter` **{2}** (`{3}`). Current positions: {4} -> {5}. Evidence: {6}.' -f $item.Consumer.Name, $item.Consumer.Id, $item.Owner.Name, $item.Owner.Id, $item.Owner.Position, $item.Consumer.Position, ($item.Evidence -join '; ')))
    }
}
$lines.Add('')
$lines.Add('## Missing direct metadata edges in FIP/FCP')
$lines.Add('')
if (-not $missingProject.Count) { $lines.Add('- None detected from active patch, inheritance or assembly references.') }
else {
    foreach ($item in $missingProject | Sort-Object { $_.Consumer.Position }, { $_.Owner.Position }) {
        $lines.Add(('- **{0}** (`{1}`) should declare `loadAfter` **{2}** (`{3}`). Evidence: {4}.' -f $item.Consumer.Name, $item.Consumer.Id, $item.Owner.Name, $item.Owner.Id, ($item.Evidence -join '; ')))
    }
}
$lines.Add('')
$lines.Add('## Undeclared optional patch relations with reversed current order')
$lines.Add('')
$lines.Add('These are compatibility Patch XPath relations, not proven loader requirements. The supplied runtime log contains no PatchOperation failure, so they are kept separate from the high-confidence assembly/inheritance edges above.')
$lines.Add('')
if (-not $riskyPatchRelations.Count) { $lines.Add('- None.') }
else {
    foreach ($item in $riskyPatchRelations | Sort-Object { $_.Consumer.Position }, { $_.Owner.Position }) {
        $lines.Add(('- **{0}** (`{1}`, position {2}`) patches **{3}** (`{4}`, position {5}`) without an explicit order path. Evidence: {6}.' -f $item.Consumer.Name, $item.Consumer.Id, $item.Consumer.Position, $item.Owner.Name, $item.Owner.Id, $item.Owner.Position, ($item.Evidence -join '; ')))
    }
}
$lines.Add('')
$lines.Add('## Privately bundled Harmony copies')
$lines.Add('')
if (-not $privateHarmonyProviders.Count) { $lines.Add('- None.') }
else {
    foreach ($item in $privateHarmonyProviders) { $lines.Add(('- **{0}** (`{1}`) ships its own `0Harmony.dll`. The official Harmony mod currently loads first, but the private copy remains a library-conflict risk.' -f $item.Name, $item.Id)) }
}
$lines.Add('')
$lines.Add('## Duplicate direct Def identities across active mods')
$lines.Add('')
if (-not $duplicateDefs.Count) { $lines.Add('- None.') }
else {
    foreach ($item in $duplicateDefs | Sort-Object Key) { $lines.Add(('- `{0}`: {1}.' -f $item.Key, (@($item.Value.Id | Sort-Object -Unique) -join ', '))) }
}
$lines.Add('')
$lines.Add('## Notes')
$lines.Add('')
$lines.Add('- A missing direct edge is a metadata defect even if the current numeric positions happen to be safe. Auto-sort may choose a different valid order later unless the relation is declared.')
$lines.Add('- Workshop metadata edits are overwritten by Steam updates. Prefer reporting third-party defects upstream or maintaining a documented local metadata patch.')

$directory = Split-Path -Parent $OutputPath
if (-not (Test-Path -LiteralPath $directory)) { [void](New-Item -ItemType Directory -Path $directory) }
[IO.File]::WriteAllText($OutputPath, ($lines -join [Environment]::NewLine) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

Write-Host "Active mods: $($activeIds.Count)"
Write-Host "Discovered package IDs: $($mods.Count)"
Write-Host "Mapped active mods: $($activeMods.Count)"
foreach ($debugMod in @($activeMods | Select-Object -First 10)) { Write-Host "Root $($debugMod.Id): $($debugMod.Roots -join '; ')" }
Write-Host "Selected Def XML files: $($defFiles.Count)"
Write-Host "Selected Patch XML files: $($patchFiles.Count)"
Write-Host "Inferred raw order references: $($inferred.Count)"
Write-Host "Declared order violations: $($declaredViolations.Count)"
Write-Host "Missing third-party edges: $($missingThirdParty.Count)"
Write-Host "Missing FIP/FCP edges: $($missingProject.Count)"
Write-Host "Undeclared reversed patch relations: $($riskyPatchRelations.Count)"
Write-Host "Duplicate direct Def identities: $($duplicateDefs.Count)"
Write-Host "Report: $OutputPath"
