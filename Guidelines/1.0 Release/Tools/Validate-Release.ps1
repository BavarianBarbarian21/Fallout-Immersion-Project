param(
    [string]$ReleaseRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$ReleaseRoot = [IO.Path]::GetFullPath($ReleaseRoot)
$ReportPath = Join-Path $ReleaseRoot 'Reports\FINAL_VALIDATION.md'
$checks = [Collections.Generic.List[object]]::new()

function Add-Check {
    param([string]$Category, [string]$Name, [bool]$Passed, [string]$Details)
    $script:checks.Add([pscustomobject]@{
        Category = $Category
        Name = $Name
        Passed = $Passed
        Details = $Details
    })
}

function Same-Set {
    param([object[]]$Left, [object[]]$Right)
    return ((@($Left) | Sort-Object) -join "`n") -ceq ((@($Right) | Sort-Object) -join "`n")
}

function Get-ConditionIds {
    param([Xml.XmlElement]$Node)
    $ids = [Collections.Generic.List[string]]::new()
    foreach ($attribute in @('IfModActive', 'IfModActiveAll')) {
        if ($Node.HasAttribute($attribute)) {
            foreach ($id in $Node.GetAttribute($attribute).Split(',')) {
                if ($id.Trim()) { $ids.Add($id.Trim()) }
            }
        }
    }
    return $ids.ToArray()
}

function Test-LoadCondition {
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

$expected = [ordered]@{
    'FIP-Arktos'      = [pscustomobject]@{ Base = 'Arktos';      PackageId = 'FIP.Arktos';      Requirements = @() }
    'FIP-Big MT'      = [pscustomobject]@{ Base = 'BigMT';      PackageId = 'FIP.Sunset';      Requirements = @() }
    'FIP-Corvega'     = [pscustomobject]@{ Base = 'Corvega';    PackageId = 'FIP.Corvega';     Requirements = @() }
    'FIP-Donaustahl'  = [pscustomobject]@{ Base = 'Donaustahl'; PackageId = 'FIP.Donaustahl';  Requirements = @() }
    'FIP-FutureTec'   = [pscustomobject]@{ Base = 'FutureTec';  PackageId = 'FIP.FutureTec';   Requirements = @() }
    'FIP-Greenway'    = [pscustomobject]@{ Base = 'Greenway';   PackageId = 'FIP.Greenway';    Requirements = @('Ludeon.RimWorld.Ideology') }
    'FIP-H&HTools'    = [pscustomobject]@{ Base = 'HHTools';    PackageId = 'FIP.HHTools';     Requirements = @() }
    'FIP-Hubris'      = [pscustomobject]@{ Base = 'Hubris';     PackageId = 'FIP.Hubris';      Requirements = @() }
    'FIP-Lucky 38'    = [pscustomobject]@{ Base = 'Lucky38';    PackageId = 'FIP.Lucky38';     Requirements = @() }
    'FIP-Poseidon'    = [pscustomobject]@{ Base = 'Poseidon';   PackageId = 'FIP.Poseidon';    Requirements = @() }
    'FIP-Repconn'     = [pscustomobject]@{ Base = 'Repconn';    PackageId = 'FIP.Repconn';     Requirements = @('Ludeon.RimWorld.Odyssey') }
    'FIP-RobCo'       = [pscustomobject]@{ Base = 'RobCo';      PackageId = 'FIP.RobCo';       Requirements = @('Ludeon.RimWorld.Biotech') }
    'FIP-WestTek'     = [pscustomobject]@{ Base = 'WestTek';    PackageId = 'FIP.WestTek';     Requirements = @('Ludeon.RimWorld.Biotech') }
    'FIP-Whitespring' = [pscustomobject]@{ Base = 'Whitespring';PackageId = 'FIP.Whitespring'; Requirements = @('Ludeon.RimWorld.Royalty') }
}

$gameplay = @(Get-ChildItem -LiteralPath $ReleaseRoot -Directory -Filter 'FIP-*' |
    Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'LoadFolders.xml') })
$translations = @(Get-ChildItem -LiteralPath $ReleaseRoot -Directory -Filter 'FIP-Translation*')

# Build before inspecting assemblies: ReflectionOnlyLoadFrom keeps files locked for
# the lifetime of Windows PowerShell and would otherwise block staged output writes.
$buildPassed = $true
$buildDetails = 'Skipped by caller; no build result recorded in this run'
if (-not $SkipBuild) {
    $solution = Get-ChildItem -LiteralPath (Join-Path $ReleaseRoot 'Source') -File -Filter *.sln | Select-Object -First 1
    if (-not $solution) {
        $buildPassed = $false
        $buildDetails = 'No solution file found'
    }
    else {
        $buildOutput = @(& dotnet build $solution.FullName -c Release --nologo 2>&1)
        $buildExit = $LASTEXITCODE
        $buildPassed = $buildExit -eq 0
        $buildDetails = "exit $buildExit; $(@($buildOutput | Select-Object -Last 8) -join ' ')"
    }
}

Add-Check 'Release identity' 'Gameplay module count' ($gameplay.Count -eq 14) "$($gameplay.Count) found; 14 expected"
Add-Check 'Release identity' 'Translation module count' ($translations.Count -eq 4) "$($translations.Count) found; 4 expected"
Add-Check 'Release identity' 'No playable FIP-Sunset directory' (-not (Test-Path -LiteralPath (Join-Path $ReleaseRoot 'FIP-Sunset'))) 'Sunset content is integrated; Big MT owns its former identity'

# XML well-formedness across all release modules.
$moduleRoots = @($gameplay + $translations)
$xmlFiles = @($moduleRoots | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -File -Recurse -Filter *.xml })
$invalidXml = [Collections.Generic.List[string]]::new()
foreach ($file in $xmlFiles) {
    try { [void]([xml][IO.File]::ReadAllText($file.FullName)) }
    catch { $invalidXml.Add("$($file.FullName): $($_.Exception.Message)") }
}
Add-Check 'XML' 'All release XML is well formed' ($invalidXml.Count -eq 0) "$($xmlFiles.Count) files parsed; $($invalidXml.Count) invalid"

# About metadata and the exactly five allowed content requirements.
$packageIds = [Collections.Generic.List[string]]::new()
$requirementPairs = [Collections.Generic.List[string]]::new()
foreach ($folderName in $expected.Keys) {
    $spec = $expected[$folderName]
    $mod = $gameplay | Where-Object Name -CEQ $folderName | Select-Object -First 1
    if (-not $mod) {
        Add-Check 'Release identity' "$folderName exists" $false 'Missing gameplay module'
        continue
    }
    $aboutPath = Join-Path $mod.FullName 'About\About.xml'
    [xml]$about = [IO.File]::ReadAllText($aboutPath)
    $actualId = $about.SelectSingleNode('/ModMetaData/packageId').InnerText.Trim()
    $packageIds.Add($actualId)
    Add-Check 'Release identity' "$folderName package ID" ($actualId -ceq $spec.PackageId) "$actualId; expected $($spec.PackageId)"
    $actualRequirements = @($about.SelectNodes('/ModMetaData/modDependencies/li/packageId') | ForEach-Object { $_.InnerText.Trim() })
    foreach ($requirement in $actualRequirements) { $requirementPairs.Add("$actualId -> $requirement") }
    Add-Check 'Requirements' "$folderName hard requirements" (Same-Set $actualRequirements $spec.Requirements) $(if ($actualRequirements.Count) { $actualRequirements -join ', ' } else { 'none' })
}
$duplicates = @($packageIds | Group-Object | Where-Object Count -gt 1)
Add-Check 'Release identity' 'Package IDs are unique' ($duplicates.Count -eq 0) $(if ($duplicates.Count) { ($duplicates.Name -join ', ') } else { '14 unique gameplay package IDs' })
Add-Check 'Requirements' 'Exactly five content requirement edges' ($requirementPairs.Count -eq 5) ($requirementPairs -join '; ')
Add-Check 'Requirements' 'No hard Harmony requirement' (-not ($requirementPairs -match 'brrainz\.harmony')) 'Harmony is optional through LoadFolders'

$bigMt = Join-Path $ReleaseRoot 'FIP-Big MT'
$publishedId = (Get-Content -LiteralPath (Join-Path $bigMt 'About\PublishedFileId.txt') -Raw).Trim()
Add-Check 'Release identity' 'Big MT Workshop identity' ($publishedId -ceq '3760676309') "$publishedId; expected 3760676309"
[xml]$bigMtAbout = [IO.File]::ReadAllText((Join-Path $bigMt 'About\About.xml'))
Add-Check 'Release identity' 'Big MT display name' ($bigMtAbout.ModMetaData.name -ceq 'FIP - Big MT') $bigMtAbout.ModMetaData.name

# LoadFolder structure, paths, naming, and static load-selection simulations.
$missingFolders = [Collections.Generic.List[string]]::new()
$baseErrors = [Collections.Generic.List[string]]::new()
$conditionErrors = [Collections.Generic.List[string]]::new()
$allConditionIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$loadFolderNodes = @{}
foreach ($mod in $gameplay) {
    $spec = $expected[$mod.Name]
    [xml]$loadXml = [IO.File]::ReadAllText((Join-Path $mod.FullName 'LoadFolders.xml'))
    $nodes = @($loadXml.SelectNodes('/loadFolders/v1.6/li'))
    $loadFolderNodes[$mod.Name] = $nodes
    $expectedBaseText = "LoadFolders/$($spec.Base)"
    if ($nodes.Count -eq 0 -or $nodes[0].InnerText.Trim() -cne $expectedBaseText -or $nodes[0].Attributes.Count -ne 0) {
        $baseErrors.Add("$($mod.Name): first entry must be unconditional $expectedBaseText")
    }
    $basePath = Join-Path $mod.FullName $expectedBaseText.Replace('/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path -LiteralPath $basePath -PathType Container) -or @(Get-ChildItem -LiteralPath $basePath -File -Recurse).Count -eq 0) {
        $baseErrors.Add("$($mod.Name): base folder missing or empty")
    }
    for ($index = 0; $index -lt $nodes.Count; $index++) {
        $node = [Xml.XmlElement]$nodes[$index]
        $relative = $node.InnerText.Trim()
        $physical = Join-Path $mod.FullName $relative.Replace('/', [IO.Path]::DirectorySeparatorChar)
        if (-not (Test-Path -LiteralPath $physical -PathType Container)) { $missingFolders.Add("$($mod.Name): $relative") }
        if ($index -gt 0 -and $node.Attributes.Count -eq 0) { $conditionErrors.Add("$($mod.Name): optional folder $relative is unconditional") }
        if ($node.HasAttribute('IfModActive') -and $node.GetAttribute('IfModActive').Contains(',')) { $conditionErrors.Add("$($mod.Name): $relative uses a list in IfModActive") }
        if ($node.HasAttribute('IfModActiveAll') -and $node.GetAttribute('IfModActiveAll').Split(',').Count -lt 2) { $conditionErrors.Add("$($mod.Name): $relative uses IfModActiveAll for one mod") }
        foreach ($id in Get-ConditionIds $node) { [void]$allConditionIds.Add($id) }
    }

    $minimal = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$minimal.Add($spec.PackageId)
    foreach ($required in $spec.Requirements) { [void]$minimal.Add($required) }
    $minimalSelected = @($nodes | Where-Object { Test-LoadCondition $_ $minimal })
    if ($minimalSelected.Count -eq 0 -or $minimalSelected[0].InnerText.Trim() -cne $expectedBaseText) {
        $conditionErrors.Add("$($mod.Name): minimal simulation did not select its base first")
    }
    foreach ($node in $nodes | Select-Object -Skip 1) {
        $ids = @(Get-ConditionIds $node)
        if ($ids.Count) {
            $singleTest = [Collections.Generic.HashSet[string]]::new($minimal, [StringComparer]::OrdinalIgnoreCase)
            foreach ($id in $ids) { [void]$singleTest.Add($id) }
            if (-not (Test-LoadCondition $node $singleTest)) { $conditionErrors.Add("$($mod.Name): optional simulation failed for $($node.InnerText.Trim())") }
            if ($node.HasAttribute('IfModActiveAll')) {
                foreach ($omitted in $ids) {
                    $partial = [Collections.Generic.HashSet[string]]::new($minimal, [StringComparer]::OrdinalIgnoreCase)
                    foreach ($id in $ids) { if ($id -cne $omitted) { [void]$partial.Add($id) } }
                    if (Test-LoadCondition $node $partial) { $conditionErrors.Add("$($mod.Name): multi-mod folder activates without $omitted") }
                }
            }
        }
    }
}

$baseNamedFolders = @($gameplay | ForEach-Object { Get-ChildItem -LiteralPath $_.FullName -Directory -Recurse | Where-Object Name -CEQ 'Base' })
Add-Check 'LoadFolders' 'Named nonempty base folders load first' ($baseErrors.Count -eq 0) $(if ($baseErrors.Count) { $baseErrors -join '; ' } else { '14 module-specific base folders' })
Add-Check 'LoadFolders' 'Every declared folder exists' ($missingFolders.Count -eq 0) $(if ($missingFolders.Count) { $missingFolders -join '; ' } else { 'all declared paths exist' })
Add-Check 'LoadFolders' 'No folder is named Base' ($baseNamedFolders.Count -eq 0) "$($baseNamedFolders.Count) found"
Add-Check 'LoadFolders' 'Conditions and static combination simulations' ($conditionErrors.Count -eq 0) $(if ($conditionErrors.Count) { $conditionErrors -join '; ' } else { 'minimal, every optional entry, partial multi-mod exclusions and full-condition syntax passed' })

# Exact high-risk ownership/condition assertions.
function Find-LoadFolderNode([string]$Module, [string]$Path) {
    return @($loadFolderNodes[$Module] | Where-Object { $_.InnerText.Trim() -ceq $Path }) | Select-Object -First 1
}
$waiter = Find-LoadFolderNode 'FIP-Lucky 38' 'LoadFolders/MechanoidWaiter_RobCo'
$waiterExact = $waiter -and $waiter.GetAttribute('IfModActiveAll') -ceq 'FIP.RobCo,GonDragon.MechanoidWaiter'
$robcoWaiterRefs = @(Get-ChildItem -LiteralPath (Join-Path $ReleaseRoot 'FIP-RobCo') -File -Recurse | Where-Object { $_.FullName -notmatch '\\About\\' } | Select-String -SimpleMatch 'MechanoidWaiter')
Add-Check 'Ownership' 'Mechanoid Waiter plus RobCo belongs entirely to Lucky 38' ($waiterExact -and $robcoWaiterRefs.Count -eq 0) "Lucky condition exact: $waiterExact; RobCo duplicate references: $($robcoWaiterRefs.Count)"

$empireStandard = Find-LoadFolderNode 'FIP-Whitespring' 'LoadFolders/Empire_StandardMaterials'
$empireDonaustahl = Find-LoadFolderNode 'FIP-Whitespring' 'LoadFolders/Empire_Donaustahl'
$empireExact = $empireStandard -and $empireStandard.GetAttribute('IfModActive') -ceq 'OskarPotocki.VFE.Empire' -and $empireStandard.GetAttribute('IfModNotActive') -ceq 'FIP.Donaustahl' -and $empireDonaustahl -and $empireDonaustahl.GetAttribute('IfModActiveAll') -ceq 'OskarPotocki.VFE.Empire,FIP.Donaustahl'
$donaustahlEmpireRefs = @(Get-ChildItem -LiteralPath (Join-Path $ReleaseRoot 'FIP-Donaustahl\LoadFolders') -File -Recurse | Select-String -Pattern 'VFE[._ -]?Empire|OskarPotocki\.VFE\.Empire')
Add-Check 'Ownership' 'Empire variants belong to Whitespring and are exclusive' ($empireExact -and $donaustahlEmpireRefs.Count -eq 0) "exclusive conditions: $empireExact; Donaustahl Empire references: $($donaustahlEmpireRefs.Count)"

# FCP exclusively owns the selectable storyteller roster. Keep every underlying
# Def valid for DefOf and third-party code references, but hide every selectable
# non-FCP storyteller in the module that owns its content provider.
$storytellerFolderContracts = @(
    [pscustomobject]@{ Module = 'FIP-Donaustahl';  Path = 'LoadFolders/Donaustahl'; ExpectedAttribute = '';               ExpectedValue = '' },
    [pscustomobject]@{ Module = 'FIP-H&HTools';    Path = 'LoadFolders/Medieval2';   ExpectedAttribute = 'IfModActive';    ExpectedValue = 'OskarPotocki.VFE.Medieval2' },
    [pscustomobject]@{ Module = 'FIP-H&HTools';    Path = 'LoadFolders/Tribals';     ExpectedAttribute = 'IfModActive';    ExpectedValue = 'OskarPotocki.VFE.Tribals' },
    [pscustomobject]@{ Module = 'FIP-H&HTools';    Path = 'LoadFolders/Settlers';    ExpectedAttribute = 'IfModActive';    ExpectedValue = 'OskarPotocki.VanillaFactionsExpanded.SettlersModule' },
    [pscustomobject]@{ Module = 'FIP-Hubris';      Path = 'LoadFolders/Psycasts';    ExpectedAttribute = 'IfModActive';    ExpectedValue = 'VanillaExpanded.VPsycastsE' },
    [pscustomobject]@{ Module = 'FIP-Whitespring'; Path = 'LoadFolders/Empire';      ExpectedAttribute = 'IfModActive';    ExpectedValue = 'OskarPotocki.VFE.Empire' },
    [pscustomobject]@{ Module = 'FIP-Whitespring'; Path = 'LoadFolders/Deserters';   ExpectedAttribute = 'IfModActive';    ExpectedValue = 'OskarPotocki.VFE.Deserters' }
)
$storytellerFolderErrors = [Collections.Generic.List[string]]::new()
foreach ($contract in $storytellerFolderContracts) {
    $node = Find-LoadFolderNode $contract.Module $contract.Path
    if (-not $node) {
        $storytellerFolderErrors.Add("$($contract.Module): missing $($contract.Path)")
        continue
    }
    if (-not $contract.ExpectedAttribute) {
        if ($node.Attributes.Count -ne 0) { $storytellerFolderErrors.Add("$($contract.Module): base storyteller visibility patch is conditional") }
    }
    elseif ($node.GetAttribute($contract.ExpectedAttribute) -cne $contract.ExpectedValue -or $node.Attributes.Count -ne 1) {
        $storytellerFolderErrors.Add("$($contract.Module): $($contract.Path) must use only $($contract.ExpectedAttribute)=$($contract.ExpectedValue)")
    }
}
Add-Check 'Storytellers' 'Visibility-patch folders have exact provider conditions' ($storytellerFolderErrors.Count -eq 0) $(if ($storytellerFolderErrors.Count) { $storytellerFolderErrors -join '; ' } else { 'Donaustahl base plus six provider-specific optional folders' })

$storytellerVisibilityContracts = @(
    [pscustomobject]@{ DefName = 'Cassandra';                 Module = 'FIP-Donaustahl';  RelativePath = 'LoadFolders\Donaustahl\Patches\FIP-Donaustahl\Storytellers\Donaustahl_RemoveVanillaStorytellers.xml' },
    [pscustomobject]@{ DefName = 'Phoebe';                   Module = 'FIP-Donaustahl';  RelativePath = 'LoadFolders\Donaustahl\Patches\FIP-Donaustahl\Storytellers\Donaustahl_RemoveVanillaStorytellers.xml' },
    [pscustomobject]@{ DefName = 'Randy';                    Module = 'FIP-Donaustahl';  RelativePath = 'LoadFolders\Donaustahl\Patches\FIP-Donaustahl\Storytellers\Donaustahl_RemoveVanillaStorytellers.xml' },
    [pscustomobject]@{ DefName = 'VFEM_MaynardMedieval';     Module = 'FIP-H&HTools';    RelativePath = 'LoadFolders\Medieval2\Patches\FIP-H&HTools\Storytellers\HHTools_RemoveMaynardMedieval.xml' },
    [pscustomobject]@{ DefName = 'VFET_TalonTribal';         Module = 'FIP-H&HTools';    RelativePath = 'LoadFolders\Tribals\Patches\FIP-H&HTools\Storytellers\HHTools_RemoveTalonTribal.xml' },
    [pscustomobject]@{ DefName = 'VFES_DD';                  Module = 'FIP-H&HTools';    RelativePath = 'LoadFolders\Settlers\Patches\FIP-H&HTools\Storytellers\HHTools_RemoveDiegoDire.xml' },
    [pscustomobject]@{ DefName = 'VPE_Basilicus';            Module = 'FIP-Hubris';      RelativePath = 'LoadFolders\Psycasts\Patches\FIP-Hubris\Storytellers\Hubris_RemoveBasilicus.xml' },
    [pscustomobject]@{ DefName = 'VFEE_AriadneArchduchess';  Module = 'FIP-Whitespring'; RelativePath = 'LoadFolders\Empire\Patches\FIP-Whitespring\Storytellers\Whitespring_RemoveAriadneArchduchess.xml' },
    [pscustomobject]@{ DefName = 'VFED_Damocles';            Module = 'FIP-Whitespring'; RelativePath = 'LoadFolders\Deserters\Patches\FIP-Whitespring\Storytellers\Whitespring_RemoveDamocles.xml' }
)
$storytellerVisibilityErrors = [Collections.Generic.List[string]]::new()
$allStorytellerVisibilityTargets = [Collections.Generic.List[object]]::new()
$storytellerDefDeletions = [Collections.Generic.List[object]]::new()
foreach ($mod in $gameplay) {
    foreach ($file in Get-ChildItem -LiteralPath $mod.FullName -File -Recurse -Filter *.xml) {
        [xml]$xml = [IO.File]::ReadAllText($file.FullName)
        foreach ($operation in $xml.SelectNodes('/Patch/Operation[@Class="PatchOperationConditional"][xpath[contains(., "StorytellerDef") and contains(., "/listVisible")]]')) {
            $allStorytellerVisibilityTargets.Add([pscustomobject]@{
                Module = $mod.Name
                File = $file.FullName
                XPath = $operation.SelectSingleNode('xpath').InnerText.Trim()
                MatchClass = $operation.SelectSingleNode('match').GetAttribute('Class')
                MatchXPath = $operation.SelectSingleNode('match/xpath').InnerText.Trim()
                MatchValue = $operation.SelectSingleNode('match/value/listVisible').InnerText.Trim()
                NoMatchClass = $operation.SelectSingleNode('nomatch').GetAttribute('Class')
                NoMatchXPath = $operation.SelectSingleNode('nomatch/xpath').InnerText.Trim()
                NoMatchValue = $operation.SelectSingleNode('nomatch/value/listVisible').InnerText.Trim()
            })
        }
        foreach ($xpath in $xml.SelectNodes('//Operation[@Class="PatchOperationRemove"]/xpath[contains(., "StorytellerDef")]')) {
            $storytellerDefDeletions.Add([pscustomobject]@{ Module = $mod.Name; File = $file.FullName; XPath = $xpath.InnerText.Trim() })
        }
    }
}
foreach ($contract in $storytellerVisibilityContracts) {
    $path = Join-Path (Join-Path $ReleaseRoot $contract.Module) $contract.RelativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $storytellerVisibilityErrors.Add("$($contract.DefName): missing $path")
        continue
    }
    $defXPath = "/Defs/StorytellerDef[defName=`"$($contract.DefName)`"]"
    $visibleXPath = "$defXPath/listVisible"
    $matches = @($allStorytellerVisibilityTargets | Where-Object {
        $_.Module -ceq $contract.Module -and $_.File -ceq $path -and
        $_.XPath -ceq $visibleXPath -and $_.MatchClass -ceq 'PatchOperationReplace' -and
        $_.MatchXPath -ceq $visibleXPath -and $_.MatchValue -ceq 'false' -and
        $_.NoMatchClass -ceq 'PatchOperationAdd' -and $_.NoMatchXPath -ceq $defXPath -and
        $_.NoMatchValue -ceq 'false'
    })
    if ($matches.Count -ne 1) { $storytellerVisibilityErrors.Add("$($contract.DefName): expected one exact false visibility contract in $($contract.Module), found $($matches.Count)") }
}
$expectedStorytellerXPaths = @($storytellerVisibilityContracts | ForEach-Object { "/Defs/StorytellerDef[defName=`"$($_.DefName)`"]/listVisible" })
$actualStorytellerXPaths = @($allStorytellerVisibilityTargets | ForEach-Object XPath)
$storytellerVisibilitySetExact = $allStorytellerVisibilityTargets.Count -eq 9 -and (Same-Set $actualStorytellerXPaths $expectedStorytellerXPaths)
Add-Check 'Storytellers' 'Nine non-FCP storytellers are hidden exactly once by their owners' ($storytellerVisibilityErrors.Count -eq 0 -and $storytellerVisibilitySetExact) $(if ($storytellerVisibilityErrors.Count) { $storytellerVisibilityErrors -join '; ' } else { 'Cassandra, Phoebe, Randy, Maynard, Talon, Diego, Basilicus, Ariadne and Damocles' })
Add-Check 'Storytellers' 'Storyteller defs remain valid for DefOf and code references' ($storytellerDefDeletions.Count -eq 0) "$($storytellerDefDeletions.Count) StorytellerDef deletion operations"

$directFipStorytellers = [Collections.Generic.List[string]]::new()
$storytellerLanguageEntries = [Collections.Generic.List[object]]::new()
foreach ($module in $moduleRoots) {
    foreach ($file in Get-ChildItem -LiteralPath $module.FullName -File -Recurse -Filter *.xml) {
        [xml]$xml = [IO.File]::ReadAllText($file.FullName)
        foreach ($def in $xml.SelectNodes('/Defs/StorytellerDef[defName]')) { $directFipStorytellers.Add("$($module.Name):$($def.defName)") }
        if ($file.FullName -match '\\DefInjected\\StorytellerDef\\') {
            foreach ($entry in $xml.SelectNodes('/LanguageData/*')) {
                $storytellerLanguageEntries.Add([pscustomobject]@{ Module = $module.Name; Key = $entry.LocalName; File = $file.FullName })
            }
        }
    }
}
$nonFcpStorytellerLanguageEntries = @($storytellerLanguageEntries | Where-Object { $_.Key -notlike 'FCP_Storyteller_*' })
$fcpStorytellerLanguageEntries = @($storytellerLanguageEntries | Where-Object { $_.Key -like 'FCP_Storyteller_*' })
Add-Check 'Storytellers' 'FIP defines no replacement storytellers' ($directFipStorytellers.Count -eq 0) $(if ($directFipStorytellers.Count) { $directFipStorytellers -join '; ' } else { 'FCP remains the sole content owner' })
Add-Check 'Storytellers' 'FIP does not rename or redescribe hidden storytellers' ($nonFcpStorytellerLanguageEntries.Count -eq 0) "$($nonFcpStorytellerLanguageEntries.Count) non-FCP StorytellerDef translation keys"
Add-Check 'Storytellers' 'All six FCP storytellers retain every shipped translation' ($fcpStorytellerLanguageEntries.Count -eq 60) "$($fcpStorytellerLanguageEntries.Count) entries: 6 storytellers x label/description x 5 languages"

$bigMtBase = Join-Path $bigMt 'LoadFolders\BigMT'
$bigMtDirectDefs = @(Get-ChildItem -LiteralPath $bigMtBase -File -Recurse -Filter *.xml | ForEach-Object { [xml]$x = [IO.File]::ReadAllText($_.FullName); $x.SelectNodes('/Defs/*') })
$bigMtFoldersExact = (Find-LoadFolderNode 'FIP-Big MT' 'LoadFolders/Anomaly').GetAttribute('IfModActive') -ceq 'Ludeon.RimWorld.Anomaly' -and (Find-LoadFolderNode 'FIP-Big MT' 'LoadFolders/Anomaly_Insanity').GetAttribute('IfModActiveAll') -ceq 'Ludeon.RimWorld.Anomaly,VanillaExpanded.VAnomalyEInsanity' -and (Find-LoadFolderNode 'FIP-Big MT' 'LoadFolders/Anomaly_WestTek').GetAttribute('IfModActiveAll') -ceq 'Ludeon.RimWorld.Anomaly,FIP.WestTek'
Add-Check 'Ownership' 'Big MT is safe without Anomaly' ($bigMtDirectDefs.Count -eq 0 -and $bigMtFoldersExact) "base gameplay defs: $($bigMtDirectDefs.Count); optional conditions exact: $bigMtFoldersExact"

# Runtime-schema assertions for the Big MT content that is active only with Anomaly + WestTek.
$bigMtAboutPath = Join-Path $bigMt 'About\About.xml'
$bigMtEntitiesPath = Join-Path $bigMt 'LoadFolders\Anomaly_WestTek\Defs\BigMT_WestTek_Entities.xml'
$bigMtFactionPath = Join-Path $bigMt 'LoadFolders\Anomaly_WestTek\Defs\BigMT_WestTek_Faction.xml'
$bigMtResearchPath = Join-Path $bigMt 'LoadFolders\Anomaly_WestTek\Defs\BigMT_WestTek_Research.xml'
[xml]$bigMtAboutXml = [IO.File]::ReadAllText($bigMtAboutPath)
[xml]$bigMtEntitiesXml = [IO.File]::ReadAllText($bigMtEntitiesPath)
[xml]$bigMtFactionXml = [IO.File]::ReadAllText($bigMtFactionPath)
[xml]$bigMtResearchXml = [IO.File]::ReadAllText($bigMtResearchPath)
$bigMtLoadAfter = @($bigMtAboutXml.SelectNodes('/ModMetaData/loadAfter/li') | ForEach-Object { $_.InnerText.Trim() })
$bigMtRequiredOrder = @('Ludeon.RimWorld.Anomaly', 'VanillaExpanded.VAnomalyEInsanity', 'FIP.WestTek')
$bigMtOrderExact = $bigMtLoadAfter.Count -eq $bigMtRequiredOrder.Count -and @($bigMtRequiredOrder | Where-Object { $bigMtLoadAfter -cnotcontains $_ }).Count -eq 0
Add-Check 'Runtime schema' 'Big MT declares every conditional content provider in loadAfter' $bigMtOrderExact "declared: $($bigMtLoadAfter -join ', ')"

$invalidLifeStageAges = @($bigMtEntitiesXml.SelectNodes('/Defs/PawnKindDef/lifeStages/li/minAge'))
$validWorkTags = @('None','ManualDumb','ManualSkilled','Violent','Caring','Social','Commoner','PlantWork','Animals','Artistic','Crafting','Firefighting','Cleaning','Hauling','Cooking','Hunting','Mining','Constructing','Shooting','Intellectual','AllWork')
$invalidWorkTags = @($bigMtEntitiesXml.SelectNodes('/Defs/PawnKindDef/disabledWorkTags/li') | Where-Object { $validWorkTags -cnotcontains $_.InnerText.Trim() } | ForEach-Object { $_.InnerText.Trim() })
Add-Check 'Runtime schema' 'Big MT PawnKindDefs use valid life-stage and WorkTags fields' ($invalidLifeStageAges.Count -eq 0 -and $invalidWorkTags.Count -eq 0) "life-stage minAge nodes: $($invalidLifeStageAges.Count); invalid WorkTags: $($invalidWorkTags -join ', ')"

$legacySkillFields = @($bigMtEntitiesXml.SelectNodes('/Defs/PawnKindDef/skills/li/minLevel | /Defs/PawnKindDef/skills/li/maxLevel'))
$skillRanges = @($bigMtEntitiesXml.SelectNodes('/Defs/PawnKindDef/skills/li/range'))
$pawnKinds = @($bigMtEntitiesXml.SelectNodes('/Defs/PawnKindDef'))
$missingPrisonerRanges = @($pawnKinds | Where-Object { -not $_.SelectSingleNode('initialWillRange') -or -not $_.SelectSingleNode('initialResistanceRange') })
Add-Check 'Runtime schema' 'Big MT PawnKindDefs use SkillRange and prisoner ranges required by RimWorld 1.6' ($legacySkillFields.Count -eq 0 -and $skillRanges.Count -eq 3 -and $missingPrisonerRanges.Count -eq 0) "legacy skill fields: $($legacySkillFields.Count); skill range nodes: $($skillRanges.Count); pawn kinds missing will/resistance: $($missingPrisonerRanges.Count)"

$legacyPawnOptions = @($bigMtFactionXml.SelectNodes('/Defs/FactionDef/pawnGroupMakers/li/options/li'))
$directPawnOptions = @($bigMtFactionXml.SelectNodes('/Defs/FactionDef/pawnGroupMakers/li/options/*[self::BigMT_CrazedSuperMutant or self::BigMT_Nightkin]'))
Add-Check 'Runtime schema' 'Big MT faction uses RimWorld 1.6 PawnGenOption dictionary syntax' ($legacyPawnOptions.Count -eq 0 -and $directPawnOptions.Count -eq 2) "legacy li options: $($legacyPawnOptions.Count); direct pawn-kind weights: $($directPawnOptions.Count)"

$bigMtFactionNode = $bigMtFactionXml.SelectSingleNode('/Defs/FactionDef')
$bigMtFactionRequired = $bigMtFactionNode -and $bigMtFactionNode.GetAttribute('ParentName') -ceq 'FactionBase' -and $bigMtFactionNode.SelectSingleNode('backstoryFilters/li/categories/li') -and $bigMtFactionNode.SelectSingleNode('raidLootValueFromPointsCurve/points/li') -and $bigMtFactionNode.SelectSingleNode('maxPawnCostPerTotalPointsCurve/points/li')
Add-Check 'Runtime schema' 'Big MT humanlike faction supplies inherited and raid-generation requirements' ([bool]$bigMtFactionRequired) "FactionBase, backstory filter, raid loot curve and maximum pawn-cost curve present: $([bool]$bigMtFactionRequired)"

$holdingPlatformRefs = @($bigMtResearchXml.SelectNodes('/Defs/ResearchProjectDef/prerequisites/li[text()="HoldingPlatform"]'))
$entityContainmentRefs = @($bigMtResearchXml.SelectNodes('/Defs/ResearchProjectDef/prerequisites/li[text()="EntityContainment"]'))
Add-Check 'Runtime schema' 'Big MT research references the Anomaly EntityContainment Def' ($holdingPlatformRefs.Count -eq 0 -and $entityContainmentRefs.Count -ge 1) "obsolete HoldingPlatform refs: $($holdingPlatformRefs.Count); EntityContainment refs: $($entityContainmentRefs.Count)"

$luckyProps = Find-LoadFolderNode 'FIP-Lucky 38' 'LoadFolders/FCP_Plants_CoffeeTea_PropsAndDecor'
$luckyPropsExact = $luckyProps -and $luckyProps.GetAttribute('IfModActiveAll') -ceq 'Rick.FCP.Plants,VanillaExpanded.VBrewECandT,VanillaExpanded.VFEPropsandDecor'
$luckyCoffeeFile = Join-Path $ReleaseRoot 'FIP-Lucky 38\LoadFolders\FCP_Plants_CoffeeTea\Patches\FIP-Lucky 38\Lucky38_CoffeeWorkbench_Texture.xml'
$luckyPropsFile = Join-Path $ReleaseRoot 'FIP-Lucky 38\LoadFolders\FCP_Plants_CoffeeTea_PropsAndDecor\Patches\FIP-Lucky 38\Lucky38_PropsAndDecorCoffeeWorkbench_Texture.xml'
$luckyCoffeeText = [IO.File]::ReadAllText($luckyCoffeeFile)
$luckyPropsText = if (Test-Path -LiteralPath $luckyPropsFile) { [IO.File]::ReadAllText($luckyPropsFile) } else { '' }
$luckyContentSplit = $luckyCoffeeText -notmatch 'VFEPD_' -and $luckyPropsText -match 'VFEPD_EspressoMachine'
Add-Check 'Ownership' 'Lucky 38 Props and Decor patch has an exact three-mod condition' ($luckyPropsExact -and $luckyContentSplit) "condition exact: $luckyPropsExact; patch content split: $luckyContentSplit"

$sunsetResidueFiles = @(Get-ChildItem -LiteralPath $gameplay.FullName -File -Recurse | Where-Object { $_.Name -match 'Sunset_' -or $_.FullName -match 'FIP-Sunset' })
$sunsetResidueText = @(Get-ChildItem -LiteralPath $gameplay.FullName -File -Recurse -Include *.xml,*.cs | Select-String -Pattern 'Sunset_|FIP-Sunset')
Add-Check 'Ownership' 'No obsolete Sunset filenames or content references' ($sunsetResidueFiles.Count -eq 0 -and $sunsetResidueText.Count -eq 0) "files: $($sunsetResidueFiles.Count); text references: $($sunsetResidueText.Count)"

$ceRefs = @(Get-ChildItem -LiteralPath $gameplay.FullName -File -Recurse -Include *.xml,*.cs | Select-String -Pattern 'CombatExtended|Combat Extended|CETeam')
Add-Check 'Ownership' 'No Combat Extended integration' ($ceRefs.Count -eq 0) "$($ceRefs.Count) references"

# Visible scenario text is a contract: identity, description, summary and start dialog must travel
# together, and each English override must have a direct ScenarioDef patch as a fallback.
$scenarioContracts = @(
    [pscustomobject]@{ Def = 'Crashlanded'; Fields = @('description','scenario.summary','scenario.parts.GameStartDialog.text'); Identity = 'airship|aircraft'; Label = $null },
    [pscustomobject]@{ Def = 'LostTribe'; Fields = @('description','scenario.summary','scenario.parts.GameStartDialog.text'); Identity = 'tribe'; Label = $null },
    [pscustomobject]@{ Def = 'NakedBrutality'; Fields = @('description','scenario.summary','scenario.parts.GameStartDialog.text'); Identity = 'naked'; Label = $null },
    [pscustomobject]@{ Def = 'TheRichExplorer'; Fields = @('description','scenario.summary','scenario.parts.GameStartDialog.text'); Identity = 'vault'; Label = $null },
    [pscustomobject]@{ Def = 'Mechanitor'; Fields = @('label','description','scenario.summary','scenario.parts.GameStartDialog.text'); Identity = 'RobCo'; Label = 'The RobCo Mechanic' },
    [pscustomobject]@{ Def = 'Sanguophage'; Fields = @('label','description','scenario.summary','scenario.parts.GameStartDialog.text'); Identity = 'Wendigo'; Label = 'The Wendigo' },
    [pscustomobject]@{ Def = 'TheGravship'; Fields = @('label','description','scenario.summary','scenario.parts.GameStartDialog.text'); Identity = 'Helixen'; Label = 'The Helixen' },
    [pscustomobject]@{ Def = 'VFED_NewSafehaven'; Fields = @('label','description','scenario.summary','scenario.parts.GameStartDialog.text'); Identity = 'Brotherhood'; Label = 'Brotherhood of Steel Expedition' },
    [pscustomobject]@{ Def = 'VFEE_NewFamily'; Fields = @('label','description','scenario.summary','scenario.parts.GameStartDialog.text'); Identity = 'splinter cell'; Label = 'New Splinter Cell' }
)
$scenarioLanguageValues = @{}
$scenarioPatchXpaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($mod in $gameplay) {
    foreach ($file in Get-ChildItem -LiteralPath $mod.FullName -File -Recurse -Filter *.xml) {
        if ($file.FullName -match '\\Languages\\English\\DefInjected\\ScenarioDef\\') {
            [xml]$scenarioXml = [IO.File]::ReadAllText($file.FullName)
            foreach ($node in $scenarioXml.DocumentElement.ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element }) {
                $split = $node.LocalName.Split('.', 2)
                if ($split.Count -eq 2) {
                    $key = "$($split[0])|$($split[1])"
                    if (-not $scenarioLanguageValues.ContainsKey($key)) { $scenarioLanguageValues[$key] = [Collections.Generic.List[string]]::new() }
                    $scenarioLanguageValues[$key].Add($node.InnerText.Trim())
                }
            }
        }
        if ($file.FullName -match '\\Patches\\') {
            [xml]$patchXml = [IO.File]::ReadAllText($file.FullName)
            foreach ($xpathNode in $patchXml.SelectNodes('//xpath')) { [void]$scenarioPatchXpaths.Add($xpathNode.InnerText.Trim()) }
        }
    }
}
$scenarioFieldErrors = [Collections.Generic.List[string]]::new()
$scenarioIdentityErrors = [Collections.Generic.List[string]]::new()
$scenarioFallbackErrors = [Collections.Generic.List[string]]::new()
foreach ($contract in $scenarioContracts) {
    foreach ($field in $contract.Fields) {
        $key = "$($contract.Def)|$field"
        [object[]]$values = if ($scenarioLanguageValues.ContainsKey($key)) { @($scenarioLanguageValues[$key] | ForEach-Object { $_ }) } else { @() }
        if ($values.Count -ne 1) {
            $scenarioFieldErrors.Add("$key has $($values.Count) English values")
            continue
        }
        if ($values[0] -notmatch $contract.Identity) { $scenarioIdentityErrors.Add("$key does not contain $($contract.Identity)") }
        if ($field -ceq 'label' -and $contract.Label -and $values[0] -cne $contract.Label) { $scenarioIdentityErrors.Add("$key is '$($values[0])', expected '$($contract.Label)'") }

        $directField = switch ($field) {
            'scenario.summary' { 'scenario/summary' }
            'scenario.parts.GameStartDialog.text' { 'scenario/parts/li[@Class="ScenPart_GameStartDialog"]/text' }
            default { $field }
        }
        $expectedXpath = "/Defs/ScenarioDef[defName=`"$($contract.Def)`"]/$directField"
        $covered = $scenarioPatchXpaths.Contains($expectedXpath)
        if (-not $covered -and $field -ceq 'scenario.parts.GameStartDialog.text') {
            $covered = $scenarioPatchXpaths.Contains($expectedXpath.Substring(0, $expectedXpath.Length - 5))
        }
        if (-not $covered) { $scenarioFallbackErrors.Add("$key has no direct ScenarioDef fallback") }
    }
}
Add-Check 'Scenarios' 'Every visible FIP scenario has one complete English field bundle' ($scenarioFieldErrors.Count -eq 0) $(if ($scenarioFieldErrors.Count) { $scenarioFieldErrors -join '; ' } else { "$($scenarioContracts.Count) scenario contracts complete" })
Add-Check 'Scenarios' 'Scenario identity is coherent across every visible field' ($scenarioIdentityErrors.Count -eq 0) $(if ($scenarioIdentityErrors.Count) { $scenarioIdentityErrors -join '; ' } else { 'all contract fields carry the same thematic identity' })
Add-Check 'Scenarios' 'Every scenario language override has a direct ScenarioDef fallback' ($scenarioFallbackErrors.Count -eq 0) $(if ($scenarioFallbackErrors.Count) { $scenarioFallbackErrors -join '; ' } else { 'labels, descriptions, summaries and start dialogs are directly patched' })

# Scenario and faction labels are proper names in the UI, not ordinary lowercase
# RimWorld item labels. Check direct defs, direct patch fallbacks, and English keys.
$properNameCaseErrors = [Collections.Generic.List[string]]::new()
$slanterSpellingErrors = [Collections.Generic.List[string]]::new()
$allowedSlanterForms = @("S'Lanter", "S'Lanters", "S'Nuffy", "S'Nuffies")
$displayNodeNames = @('label','labelShort','labelShortAdj','labelPlural','description','descriptionShort','summary','text','packageLabel')
foreach ($mod in $gameplay) {
    foreach ($file in Get-ChildItem -LiteralPath $mod.FullName -File -Recurse -Filter *.xml) {
        [xml]$xml = [IO.File]::ReadAllText($file.FullName)
        foreach ($node in $xml.SelectNodes('/Defs/FactionDef/label | /Defs/ScenarioDef/label')) {
            $value = $node.InnerText.Trim()
            if ($value -and $value[0] -cmatch '[a-z]') { $properNameCaseErrors.Add("$value in $($file.FullName)") }
        }
        if ($file.FullName -match '\\Languages\\English\\DefInjected\\(FactionDef|ScenarioDef)\\') {
            foreach ($node in $xml.SelectNodes('/LanguageData/*')) {
                if ($node.LocalName.EndsWith('.label', [StringComparison]::Ordinal)) {
                    $value = $node.InnerText.Trim()
                    if ($value -and $value[0] -cmatch '[a-z]') { $properNameCaseErrors.Add("$value in $($file.FullName)") }
                }
            }
        }
        foreach ($xpathNode in $xml.SelectNodes('//xpath')) {
            if ($xpathNode.InnerText.Trim() -match '^/Defs/(FactionDef|ScenarioDef)\[.+\]/label$') {
                $labelNode = $xpathNode.ParentNode.SelectSingleNode('value/label')
                if ($labelNode) {
                    $value = $labelNode.InnerText.Trim()
                    if ($value -and $value[0] -cmatch '[a-z]') { $properNameCaseErrors.Add("$value in $($file.FullName)") }
                }
            }
        }

        foreach ($node in $xml.SelectNodes('//*[not(*)]')) {
            $isDisplayText = $displayNodeNames -contains $node.LocalName -or $node.LocalName -match '\.(label|labelShort|labelShortAdj|labelPlural|description|descriptionShort|summary|text|packageLabel)$'
            if (-not $isDisplayText) { continue }
            foreach ($match in [regex]::Matches($node.InnerText, "(?i)(?:S['\u2019]Lanter(?:s)?|S['\u2019]Nuff(?:y|ies)|\bSlanters?\b|\bSnuff(?:y|ies)\b)")) {
                if ($allowedSlanterForms -cnotcontains $match.Value) {
                    $slanterSpellingErrors.Add("'$($match.Value)' in $($file.FullName)")
                }
            }
        }
    }
}
Add-Check 'Naming' 'Scenario and faction names begin uppercase' ($properNameCaseErrors.Count -eq 0) $(if ($properNameCaseErrors.Count) { $properNameCaseErrors -join '; ' } else { 'direct defs, patch fallbacks and English overrides passed' })
Add-Check 'Naming' "S'Lanter-family display spelling is canonical" ($slanterSpellingErrors.Count -eq 0) $(if ($slanterSpellingErrors.Count) { $slanterSpellingErrors -join '; ' } else { "S'Lanter, S'Lanters, S'Nuffy and S'Nuffies only" })

# High-risk gameplay regressions reported during the 1.0 smoke test.
$greenwayMemesPath = Join-Path $ReleaseRoot 'FIP-Greenway\LoadFolders\Memes\Patches\FIP-Greenway\Greenway_VMemesEPatch.xml'
[xml]$greenwayMemesXml = [IO.File]::ReadAllText($greenwayMemesPath)
$originHideOperation = $greenwayMemesXml.SelectSingleNode('/Patch/Operation[@Class="PatchOperationAdd" and xpath[contains(.,''starts-with(defName,"VME_Structure_")'')]]')
$originFactionWeightRemoval = $greenwayMemesXml.SelectSingleNode('/Patch/Operation[@Class="PatchOperationRemove"]/xpath[text()=''/Defs/FactionDef/structureMemeWeights/*[starts-with(name(),"VME_Structure_")]'']')
$originDefRemoval = $greenwayMemesXml.SelectSingleNode('/Patch/Operation[@Class="PatchOperationRemove"]/xpath[contains(.,''/Defs/MemeDef[starts-with(defName,"VME_Structure_")]'')]')
$originDependencyRemoval = $greenwayMemesXml.SelectSingleNode('/Patch/Operation[@Class="PatchOperationRemove"]/xpath[contains(.,''requiredMemes'') or contains(.,''associatedMeme'')]')
$originPresetReferences = @($greenwayMemesXml.SelectNodes('/Patch/Operation/value//*[not(*)]') | Where-Object { $_.InnerText.Trim().StartsWith('VME_Structure_', [StringComparison]::Ordinal) })
$greenwayMemesLanguagePath = Join-Path $ReleaseRoot 'FIP-Greenway\LoadFolders\Memes\Languages\English\DefInjected\MemeDef\Greenway_Memes.xml'
[xml]$greenwayMemesLanguageXml = [IO.File]::ReadAllText($greenwayMemesLanguagePath)
$originLanguageKeys = @($greenwayMemesLanguageXml.SelectNodes('/LanguageData/*[starts-with(name(),"VME_Structure_")]'))
$originVisibilityContract = $originHideOperation -and $originHideOperation.SelectSingleNode('value/hiddenInChooseMemes').InnerText -ceq 'true' -and $originHideOperation.SelectSingleNode('value/randomizationSelectionWeightFactor').InnerText -ceq '0' -and $originFactionWeightRemoval -and -not $originDefRemoval -and -not $originDependencyRemoval -and $originPresetReferences.Count -eq 6 -and $originLanguageKeys.Count -eq 18
Add-Check 'Ideology' 'All Vanilla Memes Expanded origins stay internal but are hidden and excluded from random ideologies' ([bool]$originVisibilityContract) "family hide and zero-weight operation, faction random weights removed, Def deletion absent, retained preset references: $($originPresetReferences.Count); retained language keys: $($originLanguageKeys.Count)"

$tribalsFenceNode = Find-LoadFolderNode 'FIP-H&HTools' 'LoadFolders/Tribals'
$tribalsArchitectNode = Find-LoadFolderNode 'FIP-H&HTools' 'LoadFolders/Tribals_Architect'
$tribalsFencePath = Join-Path $ReleaseRoot 'FIP-H&HTools\LoadFolders\Tribals\Patches\FIP-H&HTools\Research\HHTools_Tribals_Fences.xml'
$tribalsArchitectFencePath = Join-Path $ReleaseRoot 'FIP-H&HTools\LoadFolders\Tribals_Architect\Patches\FIP-H&HTools\Research\HHTools_Tribals_Architect_Fences.xml'
$hhtoolsXmlText = @(Get-ChildItem -LiteralPath (Join-Path $ReleaseRoot 'FIP-H&HTools') -File -Recurse -Filter *.xml | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$fenceContract = $tribalsFenceNode -and $tribalsFenceNode.GetAttribute('IfModActive') -ceq 'OskarPotocki.VFE.Tribals' -and -not $tribalsArchitectNode -and -not (Test-Path -LiteralPath $tribalsFencePath) -and -not (Test-Path -LiteralPath $tribalsArchitectFencePath) -and -not $hhtoolsXmlText.Contains('VFET_AnimalHandling') -and -not $hhtoolsXmlText.Contains('VFET_Construction')
Add-Check 'Research' 'VFE Tribals fence research gating is left untouched' $fenceContract "no FIP fence-prerequisite override and no Tribals_Architect compatibility folder: $fenceContract"

$geneticsNode = Find-LoadFolderNode 'FIP-WestTek' 'LoadFolders/Genetics'
$obsoleteGeneticsCooking = Find-LoadFolderNode 'FIP-WestTek' 'LoadFolders/Genetics_Cooking'
$geneticsResearchPath = Join-Path $ReleaseRoot 'FIP-WestTek\LoadFolders\Genetics\Patches\FIP-WestTek\Research\WestTek_VanillaExpandedResearchTab.xml'
[xml]$geneticsResearchXml = [IO.File]::ReadAllText($geneticsResearchPath)
$geneticsTabs = @($geneticsResearchXml.SelectNodes('/Patch/Operation/value/tab') | ForEach-Object { $_.InnerText.Trim() } | Sort-Object -Unique)
$canonicalVeTab = $geneticsResearchXml.SelectSingleNode('/Patch/Operation/value/ResearchTabDef[defName="VanillaExpanded" and label="Vanilla Expanded"]')
$removesGeneticsTab = $geneticsResearchXml.SelectSingleNode('/Patch/Operation/xpath[text()=''/Defs/ResearchTabDef[defName="GR_GeneticReseach"]'']')
$veTabProviders = @(
    'OskarPotocki.VFE.Deserters', 'OskarPotocki.VFE.Medieval2',
    'VanillaExpanded.HelixienGas', 'VanillaExpanded.Recycling', 'VanillaExpanded.Temperature',
    'VanillaExpanded.VBooksE', 'VanillaExpanded.VFEArt', 'VanillaExpanded.VFECore',
    'VanillaExpanded.VFEFactory', 'VanillaExpanded.VFEFarming', 'VanillaExpanded.VFEPower',
    'VanillaExpanded.VFEProduction', 'VanillaExpanded.VFESecurity', 'VanillaExpanded.VFESpacer',
    'VanillaExpanded.VPlantsESucculents', 'VanillaExpanded.VPsycastsE'
)
[xml]$westTekAboutXml = [IO.File]::ReadAllText((Join-Path $ReleaseRoot 'FIP-WestTek\About\About.xml'))
$westTekLoadAfter = @($westTekAboutXml.SelectNodes('/ModMetaData/loadAfter/li') | ForEach-Object { $_.InnerText.Trim() })
$veProviderOrdering = @($veTabProviders | Where-Object { $_ -notin $westTekLoadAfter }).Count -eq 0
$geneticsResearchContract = $geneticsNode -and $geneticsNode.GetAttribute('IfModActive') -ceq 'VanillaExpanded.VGeneticsE' -and -not $obsoleteGeneticsCooking -and $geneticsTabs.Count -eq 1 -and $geneticsTabs[0] -ceq 'VanillaExpanded' -and $canonicalVeTab -and $removesGeneticsTab -and $veProviderOrdering
Add-Check 'Research' 'Genetics is integrated below one canonical Vanilla Expanded tree without Cooking' ([bool]$geneticsResearchContract) "Genetics-only folder, canonical tab, legacy tab removal, no Genetics_Cooking entry and optional ordering after all 16 tab providers: $([bool]$geneticsResearchContract)"

# All textures belong to the unconditional base and every FIP-owned path resolves.
$textureExtensions = @('.png', '.dds', '.jpg', '.jpeg', '.tga')
$textureIndex = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$textureIndexExact = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$textureLocationErrors = [Collections.Generic.List[string]]::new()
foreach ($mod in $gameplay) {
    $baseRoot = [IO.Path]::GetFullPath((Join-Path $mod.FullName "LoadFolders\$($expected[$mod.Name].Base)"))
    foreach ($texture in Get-ChildItem -LiteralPath $mod.FullName -File -Recurse | Where-Object { $_.FullName -match '\\Textures\\' -and $textureExtensions -contains $_.Extension.ToLowerInvariant() }) {
        if (-not $texture.FullName.StartsWith($baseRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            $textureLocationErrors.Add($texture.FullName)
        }
        $marker = $texture.FullName.IndexOf('\Textures\', [StringComparison]::OrdinalIgnoreCase)
        $virtual = $texture.FullName.Substring($marker + 10, $texture.FullName.Length - $marker - 10 - $texture.Extension.Length).Replace([char]92, [char]47)
        [void]$textureIndex.Add($virtual)
        [void]$textureIndexExact.Add($virtual)
    }
}
$textureNodeNames = @('activateTexPath','bodyDessicatedGraphicPath','bodyNakedGraphicPath','factionIconPath','flyingAnimationFramePathPrefix','graphicPath','iconPath','iconTexturePath','immatureGraphicPath','immatureSnowOverlayGraphicPath','settlementTexturePath','snowOverlayGraphicPath','texPath','uiIconPath','wornGraphicPath')
$textureRefs = [Collections.Generic.List[object]]::new()
foreach ($mod in $gameplay) {
    foreach ($file in Get-ChildItem -LiteralPath $mod.FullName -File -Recurse -Filter *.xml) {
        [xml]$xml = [IO.File]::ReadAllText($file.FullName)
        foreach ($node in $xml.SelectNodes('//*[not(*)]')) {
            if ($textureNodeNames -contains $node.LocalName) {
                $value = $node.InnerText.Trim().Replace([char]92, [char]47)
                if ($value -like 'FIP-*') { $textureRefs.Add([pscustomobject]@{ Ref = $value; File = $file.FullName }) }
            }
        }
    }
}
$missingTextures = [Collections.Generic.List[string]]::new()
$caseMismatches = [Collections.Generic.List[string]]::new()
foreach ($reference in $textureRefs) {
    $insensitiveMatches = @($textureIndex | Where-Object { $_ -ieq $reference.Ref -or $_.StartsWith($reference.Ref + '_', [StringComparison]::OrdinalIgnoreCase) -or $_.StartsWith($reference.Ref + '/', [StringComparison]::OrdinalIgnoreCase) })
    if ($insensitiveMatches.Count -eq 0) { $missingTextures.Add("$($reference.Ref) in $($reference.File)"); continue }
    $exactMatches = @($textureIndexExact | Where-Object { $_ -ceq $reference.Ref -or $_.StartsWith($reference.Ref + '_', [StringComparison]::Ordinal) -or $_.StartsWith($reference.Ref + '/', [StringComparison]::Ordinal) })
    if ($exactMatches.Count -eq 0) { $caseMismatches.Add("$($reference.Ref) in $($reference.File)") }
}
$optionalTextureDirs = @($gameplay | ForEach-Object {
    $base = [IO.Path]::GetFullPath((Join-Path $_.FullName "LoadFolders\$($expected[$_.Name].Base)"))
    Get-ChildItem -LiteralPath (Join-Path $_.FullName 'LoadFolders') -Directory -Recurse -Filter Textures | Where-Object { -not $_.FullName.StartsWith($base, [StringComparison]::OrdinalIgnoreCase) }
})
Add-Check 'Assets' 'All textures are in unconditional module folders' ($textureLocationErrors.Count -eq 0 -and $optionalTextureDirs.Count -eq 0) "$($textureIndex.Count) texture files; misplaced: $($textureLocationErrors.Count); optional Texture directories: $($optionalTextureDirs.Count)"
Add-Check 'Assets' 'All FIP texture paths resolve with exact casing' ($missingTextures.Count -eq 0 -and $caseMismatches.Count -eq 0) "$($textureRefs.Count) references / $(@($textureRefs.Ref | Sort-Object -Unique).Count) unique; missing: $($missingTextures.Count); case mismatches: $($caseMismatches.Count)"

$westTekBase = Join-Path $ReleaseRoot 'FIP-WestTek\LoadFolders\WestTek'
$invisibleTextureHash = '1594258D650464372A84556D099C6F0FAD9551BBF0EFE58E7403336FEE183EEE'
$invisibleTexturePaths = [Collections.Generic.List[string]]::new()
foreach ($direction in @('south','east','north')) {
    $invisibleTexturePaths.Add((Join-Path $westTekBase "Textures\Things\Pawn\Humanlike\HeadAttachments\Saplings\Saplings_$direction.png"))
    $invisibleTexturePaths.Add((Join-Path $westTekBase "Textures\Things\Pawn\Humanlike\Heads\WestTek\Skinwalker\WestTek_Skinwalker_InvisibleHead_$direction.png"))
}
$badInvisibleTextures = @($invisibleTexturePaths | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) -or (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash -cne $invisibleTextureHash })
Add-Check 'Assets' 'Numen cosmetics and Skinwalker head use validated invisible directional placeholders' ($badInvisibleTextures.Count -eq 0) "six 128x128 fully transparent PNG contracts; invalid or missing: $($badInvisibleTextures.Count)"

$skinwalkerGenePath = Join-Path $westTekBase 'Defs\FIP-WestTek\Genes\WestTek_Skinwalker.xml'
$skinwalkerBodyPath = Join-Path $westTekBase 'Defs\FIP-WestTek\Muties\Xenotype\WestTek_Skinwalker_BodyTypes.xml'
[xml]$skinwalkerGeneXml = [IO.File]::ReadAllText($skinwalkerGenePath)
[xml]$skinwalkerBodyXml = [IO.File]::ReadAllText($skinwalkerBodyPath)
$skinwalkerShape = $skinwalkerGeneXml.SelectSingleNode('/Defs/GeneDef[defName="WestTek_Gene_SkinwalkerRaccoonShape"]')
$skinwalkerFur = $skinwalkerBodyXml.SelectSingleNode('/Defs/FurDef[defName="WestTek_SkinwalkerRaccoon"]')
$skinwalkerHead = $skinwalkerBodyXml.SelectSingleNode('/Defs/HeadTypeDef[defName="WestTek_Skinwalker_InvisibleHead"]')
$skinwalkerArt = @()
foreach ($direction in @('south','east','north')) {
    $skinwalkerArt += Join-Path $westTekBase "Textures\Things\Pawn\Humanlike\Bodies\WestTek\Skinwalker\WestTek_Skinwalker_$direction.png"
    $skinwalkerArt += Join-Path $westTekBase "Textures\Things\Pawn\Humanlike\Bodies\WestTek\Skinwalker\WestTek_Skinwalker_${direction}m.png"
}
$skinwalkerFurPaths = @($skinwalkerFur.bodyTypeGraphicPaths.ChildNodes | ForEach-Object { $_.InnerText.Trim() })
$skinwalkerRenderNode = $skinwalkerShape.SelectSingleNode('renderNodeProperties/li[workerClass="PawnRenderNodeWorker_Fur"]')
$skinwalkerContract = $skinwalkerShape -and $skinwalkerShape.fur -ceq 'WestTek_SkinwalkerRaccoon' -and $skinwalkerShape.forcedHeadTypes.li -ceq 'WestTek_Skinwalker_InvisibleHead' -and $skinwalkerRenderNode -and -not $skinwalkerShape.SelectSingleNode('renderNodeProperties/li[workerClass="PawnRenderNodeWorker_AttachmentBody"]') -and $skinwalkerRenderNode.colorType -ceq 'Skin' -and $skinwalkerFur -and $skinwalkerFurPaths.Count -eq 7 -and @($skinwalkerFurPaths | Where-Object { $_ -cne 'Things/Pawn/Humanlike/Bodies/WestTek/Skinwalker/WestTek_Skinwalker' }).Count -eq 0 -and $skinwalkerHead -and $skinwalkerHead.hairMeshSize -ceq '(0, 0)' -and $skinwalkerHead.beardMeshSize -ceq '(0, 0)' -and @($skinwalkerArt | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }).Count -eq 0
Add-Check 'Assets' 'Skinwalker raccoon art replaces the human silhouette instead of overlaying it' ([bool]$skinwalkerContract) "FurDef body replacement for seven vanilla body types, transparent head, no AttachmentBody overlay, six directional art files: $([bool]$skinwalkerContract)"

$numenPath = Join-Path $westTekBase 'Defs\FIP-WestTek\Xenotypes\WestTek_Numen.xml'
$overgrownPath = Join-Path $westTekBase 'Defs\FIP-WestTek\Xenotypes\WestTek_Overgrown.xml'
$numenGenePath = Join-Path $westTekBase 'Defs\FIP-WestTek\Genes\WestTek_Numen.xml'
[xml]$numenXml = [IO.File]::ReadAllText($numenPath)
[xml]$overgrownXml = [IO.File]::ReadAllText($overgrownPath)
[xml]$numenGeneXml = [IO.File]::ReadAllText($numenGenePath)
$numenGenes = @($numenXml.SelectNodes('/Defs/XenotypeDef/genes/li') | ForEach-Object { $_.InnerText.Trim() })
$overgrownGenes = @($overgrownXml.SelectNodes('/Defs/XenotypeDef/genes/li') | ForEach-Object { $_.InnerText.Trim() })
$plantskinGene = $numenGeneXml.SelectSingleNode('/Defs/GeneDef[defName="WestTek_Gene_Plantskin"]')
$plantskinRenderNode = $plantskinGene.SelectSingleNode('renderNodeProperties/li[workerClass="PawnRenderNodeWorker_Fur"]')
$floraBodyContract = $numenGenes -contains 'Skin_Green' -and $numenGenes -contains 'WestTek_SaplingGrowth' -and $numenGenes -notcontains 'WestTek_Gene_Plantskin' -and $overgrownGenes -contains 'Skin_Green' -and $overgrownGenes -contains 'WestTek_SaplingGrowth' -and $overgrownGenes -contains 'WestTek_Gene_Plantskin' -and $overgrownGenes -notcontains 'Furskin' -and $plantskinGene -and $plantskinGene.fur -ceq 'Furskin' -and @($plantskinGene.forcedHeadTypes.li).Count -eq 10 -and $plantskinRenderNode -and $plantskinRenderNode.colorType -ceq 'Skin' -and @($numenGenes + $overgrownGenes | Where-Object { $_ -like 'Body_*' }).Count -eq 0
Add-Check 'Assets' 'Overgrown use their own green Plantskin gene while Numen remain unfurred' ([bool]$floraBodyContract) "Plantskin reuses Furskin body and head art with Skin color; only Overgrown carry it: $([bool]$floraBodyContract)"

$superGenePath = Join-Path $westTekBase 'Defs\FIP-WestTek\Genes\Westtek_Xenotype_Genes.xml'
[xml]$superGeneXml = [IO.File]::ReadAllText($superGenePath)
$superGene = $superGeneXml.SelectSingleNode('/Defs/GeneDef[defName="WestTek_Gene_SuperMutant"]')
$superHarmonySourcePath = Join-Path $ReleaseRoot 'Source\FIP-WestTek\Harmony\SuperMutantRenderPatch.cs'
$superHarmonySource = [IO.File]::ReadAllText($superHarmonySourcePath)
$westHarmonyNode = Find-LoadFolderNode 'FIP-WestTek' 'LoadFolders/Harmony'
$superBodyTextures = @('south','east','north') | ForEach-Object { Join-Path $westTekBase "Textures\Things\Pawn\Humanlike\Bodies\WestTek\SuperMutant\WestTek_Naked_Hulk_$_.png" }
$superContract = $superGene -and $superGene.geneClass -ceq 'FIP.WestTek.Gene_WestTekSuperMutantAppearance' -and $superGene.bodyType -ceq 'Hulk' -and @($superGene.forcedHeadTypes.li).Count -eq 6 -and $westHarmonyNode -and $westHarmonyNode.GetAttribute('IfModActive') -ceq 'brrainz.harmony' -and $superHarmonySource.Contains('Things/Pawn/Humanlike/Bodies/WestTek/SuperMutant/WestTek_Naked_Hulk') -and @($superBodyTextures | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }).Count -eq 0
Add-Check 'Assets' 'Super mutants use WestTek heads and the custom naked body when Harmony is active' ([bool]$superContract) "Hulk apparel compatibility, six WestTek heads and optional render replacement wired: $([bool]$superContract)"

# Cross-module direct Def, language-key, and concrete patch-target collisions.
$defRecords = [Collections.Generic.List[object]]::new()
$languageRecords = [Collections.Generic.List[object]]::new()
$patchRecords = [Collections.Generic.List[object]]::new()
$xpathRecords = [Collections.Generic.List[object]]::new()
foreach ($mod in $gameplay) {
    foreach ($file in Get-ChildItem -LiteralPath $mod.FullName -File -Recurse -Filter *.xml) {
        [xml]$xml = [IO.File]::ReadAllText($file.FullName)
        foreach ($node in $xml.SelectNodes('/Defs/*[defName]')) {
            $defRecords.Add([pscustomobject]@{ Module = $mod.Name; Key = "$($node.LocalName)|$($node.defName)"; File = $file.FullName })
        }
        if ($file.FullName -match '\\Languages\\English\\(Keyed|DefInjected)\\') {
            foreach ($node in $xml.SelectNodes('/LanguageData/*')) {
                $languageRecords.Add([pscustomobject]@{ Module = $mod.Name; Key = $node.LocalName; File = $file.FullName })
            }
        }
        foreach ($xpath in $xml.SelectNodes('//xpath[not(*)]')) {
            $pathValue = $xpath.InnerText.Trim()
            if (-not $pathValue) { continue }
            $xpathRecords.Add([pscustomobject]@{ Module = $mod.Name; Key = $pathValue; File = $file.FullName })
            $valueNode = $xpath.ParentNode.SelectSingleNode('value')
            $fields = @()
            if ($valueNode) { $fields = @($valueNode.ChildNodes | Where-Object NodeType -EQ ([Xml.XmlNodeType]::Element) | ForEach-Object LocalName) }
            if ($fields.Count -eq 0) { $fields = @('<target>') }
            foreach ($field in $fields) {
                $patchRecords.Add([pscustomobject]@{ Module = $mod.Name; Key = "$pathValue|$field"; File = $file.FullName })
            }
        }
    }
}
function Get-CrossModuleDuplicates([object[]]$Records) {
    return @($Records | Group-Object Key | Where-Object { @($_.Group.Module | Sort-Object -Unique).Count -gt 1 })
}
$defCollisions = @(Get-CrossModuleDuplicates $defRecords)
$languageCollisions = @(Get-CrossModuleDuplicates $languageRecords)
$patchCollisions = @(Get-CrossModuleDuplicates $patchRecords)
$rootXpathOverlaps = @(Get-CrossModuleDuplicates $xpathRecords)
Add-Check 'Collisions' 'No cross-module direct Def identities' ($defCollisions.Count -eq 0) "$($defRecords.Count) direct defs; $($defCollisions.Count) collisions"
Add-Check 'Collisions' 'No cross-module English language keys' ($languageCollisions.Count -eq 0) "$($languageRecords.Count) entries; $($languageCollisions.Count) collisions"
Add-Check 'Collisions' 'No cross-module concrete XPath plus field targets' ($patchCollisions.Count -eq 0) "$($patchRecords.Count) target signatures; $($patchCollisions.Count) collisions"
$allowedRootXPaths = @('/Defs', 'Defs/FactionDef[defName="Ancients"]', 'Defs/FactionDef[defName="AncientsHostile"]')
$rootXpathExact = $rootXpathOverlaps.Count -eq 3 -and (Same-Set @($rootXpathOverlaps.Name) $allowedRootXPaths)
Add-Check 'Collisions' 'Only documented root-XPath overlaps remain' $rootXpathExact $(if ($rootXpathOverlaps.Count) { $rootXpathOverlaps.Name -join '; ' } else { 'none' })

# Assembly placement and Harmony isolation.
$releaseDlls = @(Get-ChildItem -LiteralPath $gameplay.FullName -File -Recurse -Filter *.dll)
$bundledHarmony = @($releaseDlls | Where-Object Name -IEQ '0Harmony.dll')
$assemblyInfo = [Collections.Generic.List[object]]::new()
foreach ($dll in $releaseDlls) {
    try {
        $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom($dll.FullName)
        $refs = @($assembly.GetReferencedAssemblies() | ForEach-Object Name)
        $assemblyInfo.Add([pscustomobject]@{ File = $dll; Name = $assembly.GetName().Name; References = $refs })
    }
    catch {
        $assemblyInfo.Add([pscustomobject]@{ File = $dll; Name = $dll.BaseName; References = @("ERROR: $($_.Exception.Message)") })
    }
}
$baseAssemblyHarmonyRefs = @($assemblyInfo | Where-Object { $_.File.FullName -notmatch '\\LoadFolders\\Harmony\\' -and $_.References -contains '0Harmony' })
$optionalHarmonyAssemblies = @($assemblyInfo | Where-Object { $_.File.FullName -match '\\LoadFolders\\Harmony\\' })
$optionalHarmonyBad = @($optionalHarmonyAssemblies | Where-Object { $_.References -notcontains '0Harmony' })
$assemblyIdentityDuplicates = @($assemblyInfo | Group-Object Name | Where-Object Count -gt 1)
Add-Check 'Assemblies' 'No private 0Harmony.dll is bundled' ($bundledHarmony.Count -eq 0) "$($bundledHarmony.Count) found"
Add-Check 'Assemblies' 'Harmony references are optional-only' ($baseAssemblyHarmonyRefs.Count -eq 0 -and $optionalHarmonyAssemblies.Count -eq 3 -and $optionalHarmonyBad.Count -eq 0) "base Harmony references: $($baseAssemblyHarmonyRefs.Count); optional Harmony assemblies: $($optionalHarmonyAssemblies.Count)"
Add-Check 'Assemblies' 'Assembly identities are unique' ($assemblyIdentityDuplicates.Count -eq 0) "$($assemblyInfo.Count) assemblies; duplicate identities: $($assemblyIdentityDuplicates.Count)"
$sourceText = @(Get-ChildItem -LiteralPath (Join-Path $ReleaseRoot 'Source') -File -Recurse -Filter *.cs | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$harmonyIds = @('FIP.Lucky38.VanillaTradingExpanded', 'FIP.RobCo.SyntheticPawns', 'FIP.WestTek')
$idsPresent = @($harmonyIds | Where-Object { $sourceText.Contains($_) })
$unpatchCount = [regex]::Matches($sourceText, '\bUnpatch(?:All)?\s*\(').Count
Add-Check 'Assemblies' 'Unique Harmony IDs and no unpatching' ($idsPresent.Count -eq 3 -and $unpatchCount -eq 0) "IDs: $($idsPresent -join ', '); Unpatch calls: $unpatchCount"

# Translation identity and the intentional new meaning of FIP.Sunset.
$translationErrors = [Collections.Generic.List[string]]::new()
$translationDuplicateErrors = [Collections.Generic.List[string]]::new()
$translationNameErrors = [Collections.Generic.List[string]]::new()
$loadAfterSets = [Collections.Generic.List[string]]::new()
foreach ($translation in $translations) {
    $aboutPath = Join-Path $translation.FullName 'About\About.xml'
    [xml]$about = [IO.File]::ReadAllText($aboutPath)
    $loadAfter = @($about.SelectNodes('/ModMetaData/loadAfter/li') | ForEach-Object InnerText)
    $raw = [IO.File]::ReadAllText($aboutPath)
    if ($loadAfter -notcontains 'FIP.Sunset' -or $raw -notmatch 'Big MT') { $translationErrors.Add("$($translation.Name): missing documented Big MT/FIP.Sunset ordering") }
    if ($loadAfter -contains 'brrainz.harmony') { $translationErrors.Add("$($translation.Name): technical Harmony library remains in loadAfter") }
    if (@($loadAfter | Group-Object | Where-Object Count -gt 1).Count) { $translationErrors.Add("$($translation.Name): duplicate loadAfter entries") }
    $loadAfterSets.Add((@($loadAfter | Sort-Object) -join "`n"))

    foreach ($language in Get-ChildItem -LiteralPath (Join-Path $translation.FullName 'Languages') -Directory) {
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        $duplicates = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($file in Get-ChildItem -LiteralPath $language.FullName -File -Recurse -Filter *.xml) {
            $kind = $null
            if ($file.FullName -match '\\DefInjected\\([^\\]+)\\') { $kind = "DefInjected|$($Matches[1])" }
            elseif ($file.FullName -match '\\Keyed\\') { $kind = 'Keyed' }
            if (-not $kind) { continue }
            $languageRaw = [IO.File]::ReadAllText($file.FullName)
            [xml]$languageXml = $languageRaw
            $nameMatches = [regex]::Matches($languageRaw, "(?i)(?<![A-Za-z])s['\u2019](?:lanters?|nuffy|nuffies)\b")
            foreach ($nameMatch in $nameMatches) {
                if ($nameMatch.Value -cnotin @("S'Lanter", "S'Lanters", "S'Nuffy", "S'Nuffies")) {
                    $translationNameErrors.Add("$($translation.Name)/$($language.Name)/$($file.Name): $($nameMatch.Value)")
                }
            }
            if ($kind -eq 'DefInjected|XenotypeDef') {
                $translatedSLanter = $languageXml.SelectSingleNode('/LanguageData/WestTek_Xenotype_SLanter.label')
                $translatedSNuffy = $languageXml.SelectSingleNode('/LanguageData/WestTek_Xenotype_SNuffy.label')
                if ($translatedSLanter -and $translatedSLanter.InnerText -cne "S'Lanter") { $translationNameErrors.Add("$($translation.Name)/$($language.Name): S'Lanter xenotype label is '$($translatedSLanter.InnerText)'") }
                if ($translatedSNuffy -and $translatedSNuffy.InnerText -cne "S'Nuffy") { $translationNameErrors.Add("$($translation.Name)/$($language.Name): S'Nuffy xenotype label is '$($translatedSNuffy.InnerText)'") }
            }
            foreach ($node in $languageXml.SelectNodes('/LanguageData/*')) {
                $signature = "$kind|$($node.LocalName)"
                if (-not $seen.Add($signature)) { [void]$duplicates.Add($signature) }
            }
        }
        if ($duplicates.Count) { $translationDuplicateErrors.Add("$($translation.Name)/$($language.Name): $($duplicates.Count) duplicate signatures") }
    }
}
Add-Check 'Translations' 'All translation modules recognize FIP.Sunset as Big MT' ($translationErrors.Count -eq 0) $(if ($translationErrors.Count) { $translationErrors -join '; ' } else { '4 translation About files carry the documented identity' })
Add-Check 'Translations' 'Translation loadAfter sets are aligned and exclude Harmony' (@($loadAfterSets | Sort-Object -Unique).Count -eq 1 -and $translationErrors.Count -eq 0) '4 aligned source-order sets; technical Harmony library removed'
Add-Check 'Translations' 'No duplicate translated key signatures' ($translationDuplicateErrors.Count -eq 0) $(if ($translationDuplicateErrors.Count) { $translationDuplicateErrors -join '; ' } else { 'Chinese Simplified/Traditional, Japanese, Korean and Russian passed' })
Add-Check 'Translations' "S'Lanter-family names retain canonical capitalization in every language" ($translationNameErrors.Count -eq 0) $(if ($translationNameErrors.Count) { $translationNameErrors -join '; ' } else { "S'Lanter, S'Lanters, S'Nuffy and S'Nuffies only" })

# The build ran before assembly inspection so staged output files were not locked.
Add-Check 'Build' 'Managed solution builds' $buildPassed $buildDetails

$failed = @($checks | Where-Object { -not $_.Passed })
$report = [Collections.Generic.List[string]]::new()
$report.Add('# FIP 1.0 - final validation')
$report.Add('')
$report.Add("Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')")
$report.Add('')
$report.Add("Overall result: **$(if ($failed.Count -eq 0) { 'PASS' } else { 'FAIL' })** - $($checks.Count - $failed.Count)/$($checks.Count) checks passed.")
$report.Add('')
$report.Add('This is a static release audit plus a full managed-code build. LoadFolder combinations are simulated from their declared conditions; an actual RimWorld GUI launch is not performed by this script.')
$report.Add('')
foreach ($group in $checks | Group-Object Category) {
    $report.Add("## $($group.Name)")
    $report.Add('')
    $report.Add('| Status | Check | Details |')
    $report.Add('|---|---|---|')
    foreach ($check in $group.Group) {
        $status = if ($check.Passed) { 'PASS' } else { 'FAIL' }
        $name = $check.Name.Replace('|', '\|')
        $details = $check.Details.Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
        $report.Add("| $status | $name | $details |")
    }
    $report.Add('')
}
$report.Add('## Documented non-colliding overlaps')
$report.Add('')
$report.Add('- H&H Tools and WestTek both select the `Ancients` and `AncientsHostile` faction roots, but they modify different child fields: H&H Tools adds faction naming fields while WestTek adds `xenotypeSet`. The concrete XPath-plus-field audit confirms that these are not duplicate targets.')
$report.Add('- Lucky 38 and WestTek both add distinct defs beneath `/Defs`: Lucky 38 adds cooking recipes while WestTek creates the one canonical `VanillaExpanded` research tab. Their concrete added fields and def identities do not overlap.')
$report.Add('- `FIP.Sunset` in translation load order metadata intentionally means FIP Big MT in 1.0; it is not a surviving Sunset gameplay module.')
$report.Add('')
$report.Add('## Test scope and residual release step')
$report.Add('')
$report.Add('- Static minimum-load simulation was run for every gameplay module.')
$report.Add('- Every single optional LoadFolder condition and every partial `IfModActiveAll` exclusion was evaluated.')
$report.Add('- Storyteller ownership was checked down to exact visibility XPath, optional-provider condition, Def-preservation rule and translated key.')
$report.Add('- All XML and all FIP-owned texture paths were checked across the complete release set.')
$report.Add('- The managed solution was built and its staged assembly references were inspected.')
$report.Add('- Before publishing, perform one manual RimWorld 1.6 smoke start with the intended installed mod set; that is the only runtime/UI check not reproducible in this repository-only validator.')

$reportDirectory = Split-Path -Parent $ReportPath
if (-not (Test-Path -LiteralPath $reportDirectory)) { [void](New-Item -ItemType Directory -Path $reportDirectory) }
[IO.File]::WriteAllText($ReportPath, ($report -join [Environment]::NewLine) + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

foreach ($check in $checks) {
    $status = if ($check.Passed) { 'PASS' } else { 'FAIL' }
    Write-Host "[$status] $($check.Category): $($check.Name) - $($check.Details)"
}
Write-Host "Report: $ReportPath"
if ($failed.Count) { exit 1 }
