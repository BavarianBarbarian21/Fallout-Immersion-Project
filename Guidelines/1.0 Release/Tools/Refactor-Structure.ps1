$ErrorActionPreference = 'Stop'

$releaseRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Assert-InReleaseRoot([string]$Path) {
    $resolved = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolved.StartsWith($releaseRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Path escapes release root: $resolved"
    }
    return $resolved
}

function Rename-ReleaseDirectory([string]$RelativeSource, [string]$RelativeTarget) {
    $source = Assert-InReleaseRoot (Join-Path $releaseRoot $RelativeSource)
    $target = Assert-InReleaseRoot (Join-Path $releaseRoot $RelativeTarget)
    if (-not (Test-Path -LiteralPath $source)) {
        if (Test-Path -LiteralPath $target) { return }
        throw "Missing source directory: $source"
    }
    if ($source.Equals($target, [System.StringComparison]::OrdinalIgnoreCase)) {
        if ($source.Equals($target, [System.StringComparison]::Ordinal)) { return }
        $temporary = Assert-InReleaseRoot ($source + '.__fip_case_rename__')
        if (Test-Path -LiteralPath $temporary) {
            throw "Temporary case-rename path already exists: $temporary"
        }
        Move-Item -LiteralPath $source -Destination $temporary
        Move-Item -LiteralPath $temporary -Destination $target
        return
    }
    if (Test-Path -LiteralPath $target) {
        throw "Rename target already exists: $target"
    }
    $targetParent = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $targetParent)) {
        New-Item -ItemType Directory -Path $targetParent | Out-Null
    }
    Move-Item -LiteralPath $source -Destination $target
}

function Merge-ReleaseDirectory([string]$RelativeSource, [string]$RelativeTarget) {
    $source = Assert-InReleaseRoot (Join-Path $releaseRoot $RelativeSource)
    $target = Assert-InReleaseRoot (Join-Path $releaseRoot $RelativeTarget)
    if (-not (Test-Path -LiteralPath $source)) { return }
    if (-not (Test-Path -LiteralPath $target)) {
        Rename-ReleaseDirectory $RelativeSource $RelativeTarget
        return
    }

    $collisions = @()
    Get-ChildItem -LiteralPath $source -Recurse -File | ForEach-Object {
        $relative = $_.FullName.Substring($source.Length + 1)
        $candidate = Join-Path $target $relative
        if (Test-Path -LiteralPath $candidate) {
            $sourceHash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            $targetHash = (Get-FileHash -LiteralPath $candidate -Algorithm SHA256).Hash
            if ($sourceHash -ne $targetHash) {
                $collisions += $relative
            }
        }
    }
    if ($collisions.Count -gt 0) {
        throw "Non-identical merge collisions from $source to $target`: $($collisions -join ', ')"
    }

    & robocopy $source $target /E /MOVE /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -gt 7) {
        throw "Robocopy failed with exit code $LASTEXITCODE while merging $source"
    }
    if (Test-Path -LiteralPath $source) {
        Remove-Item -LiteralPath $source -Recurse -Force
    }
}

# Rename each always-loaded Base folder to the owning module's readable name.
$baseRenames = [ordered]@{
    'FIP-Arktos\LoadFolders\Base' = 'FIP-Arktos\LoadFolders\Arktos'
    'FIP-Donaustahl\LoadFolders\Base' = 'FIP-Donaustahl\LoadFolders\Donaustahl'
    'FIP-FutureTec\LoadFolders\Base' = 'FIP-FutureTec\LoadFolders\FutureTec'
    'FIP-Greenway\LoadFolders\Base' = 'FIP-Greenway\LoadFolders\Greenway'
    'FIP-H&HTools\LoadFolders\Base' = 'FIP-H&HTools\LoadFolders\HHTools'
    'FIP-Hubris\LoadFolders\Base' = 'FIP-Hubris\LoadFolders\Hubris'
    'FIP-Lucky 38\LoadFolders\Base' = 'FIP-Lucky 38\LoadFolders\Lucky38'
    'FIP-Poseidon\LoadFolders\Base' = 'FIP-Poseidon\LoadFolders\Poseidon'
    'FIP-Repconn\LoadFolders\Base' = 'FIP-Repconn\LoadFolders\Repconn'
    'FIP-RobCo\LoadFolders\Base' = 'FIP-RobCo\LoadFolders\RobCo'
    'FIP-WestTek\LoadFolders\Base' = 'FIP-WestTek\LoadFolders\WestTek'
    'FIP-Whitespring\LoadFolders\Base' = 'FIP-Whitespring\LoadFolders\Whitespring'
}
foreach ($entry in $baseRenames.GetEnumerator()) {
    Rename-ReleaseDirectory $entry.Key $entry.Value
}

# Arktos.
$renames = [ordered]@{
    'FIP-Arktos\LoadFolders\VAE' = 'FIP-Arktos\LoadFolders\Animals'
    'FIP-Arktos\LoadFolders\VAE_Royal' = 'FIP-Arktos\LoadFolders\RoyalAnimals'
    'FIP-Arktos\LoadFolders\Odyssey_VCEF' = 'FIP-Arktos\LoadFolders\Odyssey_Fishing'
    'FIP-Arktos\LoadFolders\AUR' = 'FIP-Arktos\LoadFolders\AncientUrbanRuins'
    'FIP-Arktos\LoadFolders\Exploration' = 'FIP-Arktos\LoadFolders\Landmarks'
    'FIP-Arktos\LoadFolders\AUR_BallisticWeapons' = 'FIP-Arktos\LoadFolders\AncientUrbanRuins_FCP_BallisticWeapons'
    'FIP-Arktos\LoadFolders\AUR_PreWarFood' = 'FIP-Arktos\LoadFolders\AncientUrbanRuins_FCP_PreWarFood'

    # Corvega.
    'FIP-Corvega\LoadFolders\OskarPotocki_VanillaVehiclesExpanded' = 'FIP-Corvega\LoadFolders\Vehicles'
    'FIP-Corvega\LoadFolders\OskarPotocki_VanillaVehiclesExpandedTier3' = 'FIP-Corvega\LoadFolders\Vehicles_Tier3'
    'FIP-Corvega\LoadFolders\OskarPotocki_VanillaVehiclesExpandedUpgrades' = 'FIP-Corvega\LoadFolders\Vehicles_Upgrades'

    # Donaustahl.
    'FIP-Donaustahl\LoadFolders\VAspirE' = 'FIP-Donaustahl\LoadFolders\Aspirations'
    'FIP-Donaustahl\LoadFolders\VBE' = 'FIP-Donaustahl\LoadFolders\Backstories'
    'FIP-Donaustahl\LoadFolders\VEE' = 'FIP-Donaustahl\LoadFolders\Events'
    'FIP-Donaustahl\LoadFolders\VSIE' = 'FIP-Donaustahl\LoadFolders\SocialInteractions'
    'FIP-Donaustahl\LoadFolders\VTE' = 'FIP-Donaustahl\LoadFolders\Traits'
    'FIP-Donaustahl\LoadFolders\VanillaTradingExpanded' = 'FIP-Donaustahl\LoadFolders\Trading'
    'FIP-Donaustahl\LoadFolders\VBooksE' = 'FIP-Donaustahl\LoadFolders\Books'

    # Future-Tec.
    'FIP-FutureTec\LoadFolders\ancients' = 'FIP-FutureTec\LoadFolders\Ancients'
    'FIP-FutureTec\LoadFolders\cryptoforge' = 'FIP-FutureTec\LoadFolders\Cryptoforge'
    'FIP-FutureTec\LoadFolders\deadlife' = 'FIP-FutureTec\LoadFolders\Deadlife'
    'FIP-FutureTec\LoadFolders\generator' = 'FIP-FutureTec\LoadFolders\Generator'

    # Greenway.
    'FIP-Greenway\LoadFolders\VIEHAR' = 'FIP-Greenway\LoadFolders\HatsAndRags'
    'FIP-Greenway\LoadFolders\VMemesE' = 'FIP-Greenway\LoadFolders\Memes'
    'FIP-Greenway\LoadFolders\VMemesE_Royalty' = 'FIP-Greenway\LoadFolders\Memes_Royalty'
    'FIP-Greenway\LoadFolders\VMemesE_VBooksE' = 'FIP-Greenway\LoadFolders\Memes_Books'
    'FIP-Greenway\LoadFolders\VMemesE_VCookE' = 'FIP-Greenway\LoadFolders\Memes_Cooking'

    # Lucky 38.
    'FIP-Lucky 38\LoadFolders\HospitalityCasino' = 'FIP-Lucky 38\LoadFolders\Casino'
    'FIP-Lucky 38\LoadFolders\HospitalitySpa' = 'FIP-Lucky 38\LoadFolders\Spa'
    'FIP-Lucky 38\LoadFolders\DubsBadHygiene' = 'FIP-Lucky 38\LoadFolders\Hygiene'
    'FIP-Lucky 38\LoadFolders\Plants_VBrewE_VCookE' = 'FIP-Lucky 38\LoadFolders\FCPPlants_Brewing_Cooking'
    'FIP-Lucky 38\LoadFolders\Plants_VBrewECandT' = 'FIP-Lucky 38\LoadFolders\FCPPlants_CoffeeTea'
    'FIP-Lucky 38\LoadFolders\Plants_VBrewECandT_VPlantsE' = 'FIP-Lucky 38\LoadFolders\FCPPlants_CoffeeTea_Plants'
    'FIP-Lucky 38\LoadFolders\Plants_VCookESushi' = 'FIP-Lucky 38\LoadFolders\FCPPlants_Sushi'
    'FIP-Lucky 38\LoadFolders\VanillaTradingExpanded' = 'FIP-Lucky 38\LoadFolders\Trading'
    'FIP-Lucky 38\LoadFolders\VanillaTradingExpanded_HHTools' = 'FIP-Lucky 38\LoadFolders\Trading_HHTools'
    'FIP-Lucky 38\LoadFolders\VanillaTradingExpanded_FCP_Core_Tools' = 'FIP-Lucky 38\LoadFolders\Trading_FCP_CoreTools'
    'FIP-Lucky 38\LoadFolders\VBrewE_VCookE' = 'FIP-Lucky 38\LoadFolders\Brewing_Cooking'
    'FIP-Lucky 38\LoadFolders\smallhotspring' = 'FIP-Lucky 38\LoadFolders\HotSpring'
    'FIP-Lucky 38\LoadFolders\spaceports' = 'FIP-Lucky 38\LoadFolders\Spaceports'
    'FIP-Lucky 38\LoadFolders\Plants_VCookE_VBrewECandT' = 'FIP-Lucky 38\LoadFolders\FCPPlants_Cooking_CoffeeTea'

    # Poseidon.
    'FIP-Poseidon\LoadFolders\VChemfuelE' = 'FIP-Poseidon\LoadFolders\Chemfuel'
    'FIP-Poseidon\LoadFolders\VFEArt' = 'FIP-Poseidon\LoadFolders\Furniture_Art'
    'FIP-Poseidon\LoadFolders\VFEFactory' = 'FIP-Poseidon\LoadFolders\Furniture_Factory'
    'FIP-Poseidon\LoadFolders\VFEMedical' = 'FIP-Poseidon\LoadFolders\Furniture_Medical'
    'FIP-Poseidon\LoadFolders\VFEPower' = 'FIP-Poseidon\LoadFolders\Furniture_Power'
    'FIP-Poseidon\LoadFolders\VFEProduction' = 'FIP-Poseidon\LoadFolders\Furniture_Production'
    'FIP-Poseidon\LoadFolders\VFESecurity' = 'FIP-Poseidon\LoadFolders\Furniture_Security'
    'FIP-Poseidon\LoadFolders\VFESpacer' = 'FIP-Poseidon\LoadFolders\Furniture_Spacer'
    'FIP-Poseidon\LoadFolders\VNutrientE' = 'FIP-Poseidon\LoadFolders\NutrientPaste'

    # WestTek.
    'FIP-WestTek\LoadFolders\VGeneticsE' = 'FIP-WestTek\LoadFolders\Genetics'
    'FIP-WestTek\LoadFolders\VGeneticsE_VCookE' = 'FIP-WestTek\LoadFolders\Genetics_Cooking'
    'FIP-WestTek\LoadFolders\sanguophage' = 'FIP-WestTek\LoadFolders\Sanguophage'

    # Whitespring.
    'FIP-Whitespring\LoadFolders\No_FCP_Tools' = 'FIP-Whitespring\LoadFolders\WithoutFCPTools'
    'FIP-Whitespring\LoadFolders\FCP_Tools' = 'FIP-Whitespring\LoadFolders\FCPTools'
    'FIP-Whitespring\LoadFolders\Deserters_VanillaTradingExpanded' = 'FIP-Whitespring\LoadFolders\Deserters_Trading'
    'FIP-Whitespring\LoadFolders\VPersonaWeaponsE' = 'FIP-Whitespring\LoadFolders\PersonaWeapons'
}
foreach ($entry in $renames.GetEnumerator()) {
    Rename-ReleaseDirectory $entry.Key $entry.Value
}

# Merge Lucky 38 folders with identical activation conditions.
Merge-ReleaseDirectory 'FIP-Lucky 38\LoadFolders\Plants_VCookE_VBrewE' 'FIP-Lucky 38\LoadFolders\FCPPlants_Brewing_Cooking'
Merge-ReleaseDirectory 'FIP-Lucky 38\LoadFolders\MechanoidWaiter' 'FIP-Lucky 38\LoadFolders\MechanoidWaiter_RobCo'

# Consolidate H&H Tools' unconditional equipment content.
Merge-ReleaseDirectory 'FIP-H&HTools\LoadFolders\Equipment_Core' 'FIP-H&HTools\LoadFolders\HHTools'
Merge-ReleaseDirectory 'FIP-H&HTools\LoadFolders\Equipment_FIP_HHTools' 'FIP-H&HTools\LoadFolders\HHTools'

# Merge equipment and faction content that share the same exact condition.
$hhMerges = [ordered]@{
    'FIP-H&HTools\LoadFolders\Equipment_Ludeon_RimWorld_Biotech' = 'FIP-H&HTools\LoadFolders\Biotech'
    'FIP-H&HTools\LoadFolders\Equipment_Ludeon_RimWorld_Odyssey' = 'FIP-H&HTools\LoadFolders\Odyssey'
    'FIP-H&HTools\LoadFolders\Equipment_Ludeon_RimWorld_Royalty' = 'FIP-H&HTools\LoadFolders\Royalty'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_BOS' = 'FIP-H&HTools\LoadFolders\FCP_BOS'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_BallisticWeapons' = 'FIP-H&HTools\LoadFolders\FCP_BallisticWeapons'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_Enclave' = 'FIP-H&HTools\LoadFolders\FCP_Enclave'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_EnergyWeapons' = 'FIP-H&HTools\LoadFolders\FCP_EnergyWeapons'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_GreatKhans' = 'FIP-H&HTools\LoadFolders\FCP_GreatKhans'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_Legion' = 'FIP-H&HTools\LoadFolders\FCP_Legion'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_MeleeWeapons' = 'FIP-H&HTools\LoadFolders\FCP_MeleeWeapons'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_NCR' = 'FIP-H&HTools\LoadFolders\FCP_NCR'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_Raiders' = 'FIP-H&HTools\LoadFolders\FCP_Raiders'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_UnarmedWeapons' = 'FIP-H&HTools\LoadFolders\FCP_UnarmedWeapons'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_Wastelanders' = 'FIP-H&HTools\LoadFolders\FCP_Wastelanders'
}
foreach ($entry in $hhMerges.GetEnumerator()) {
    Merge-ReleaseDirectory $entry.Key $entry.Value
}

# Rename remaining generated H&H Tools equipment folders.
$hhRenames = [ordered]@{
    'FIP-H&HTools\LoadFolders\Equipment_Ludeon_RimWorld_Ideology' = 'FIP-H&HTools\LoadFolders\Ideology'
    'FIP-H&HTools\LoadFolders\Equipment_OskarPotocki_VFE_Medieval2' = 'FIP-H&HTools\LoadFolders\Medieval2'
    'FIP-H&HTools\LoadFolders\Equipment_OskarPotocki_VFE_Tribals' = 'FIP-H&HTools\LoadFolders\Tribals'
    'FIP-H&HTools\LoadFolders\Equipment_OskarPotocki_VanillaFactionsExpanded_SettlersModule' = 'FIP-H&HTools\LoadFolders\Settlers'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_Core_Tools' = 'FIP-H&HTools\LoadFolders\FCP_CoreTools'
    'FIP-H&HTools\LoadFolders\Equipment_Rick_FCP_PowerArmor' = 'FIP-H&HTools\LoadFolders\FCP_PowerArmor'
    'FIP-H&HTools\LoadFolders\Equipment_VanillaExpanded_VAEAccessories' = 'FIP-H&HTools\LoadFolders\Accessories'
    'FIP-H&HTools\LoadFolders\Equipment_VanillaExpanded_VIEHAR' = 'FIP-H&HTools\LoadFolders\HatsAndRags_Equipment'
    'FIP-H&HTools\LoadFolders\Equipment_VanillaExpanded_VPersonaWeaponsE' = 'FIP-H&HTools\LoadFolders\PersonaWeapons_Equipment'
    'FIP-H&HTools\LoadFolders\Equipment_VanillaExpanded_VPsycastsE' = 'FIP-H&HTools\LoadFolders\Psycasts_Equipment'
    'FIP-H&HTools\LoadFolders\Equipment_VanillaExpanded_VWETB' = 'FIP-H&HTools\LoadFolders\TribalWeapons'
    'FIP-H&HTools\LoadFolders\Equipment_vanillaexpanded_gravship' = 'FIP-H&HTools\LoadFolders\Gravship_Equipment'
    'FIP-H&HTools\LoadFolders\Equipment_vanillaquestsexpanded_ancients' = 'FIP-H&HTools\LoadFolders\Ancients_Equipment'
    'FIP-H&HTools\LoadFolders\Equipment_vanillaquestsexpanded_cryptoforge' = 'FIP-H&HTools\LoadFolders\Cryptoforge_Equipment'
    'FIP-H&HTools\LoadFolders\Equipment_vanillaquestsexpanded_deadlife' = 'FIP-H&HTools\LoadFolders\Deadlife_Equipment'
    'FIP-H&HTools\LoadFolders\Equipment_vanillaquestsexpanded_generator' = 'FIP-H&HTools\LoadFolders\Generator_Equipment'
    'FIP-H&HTools\LoadFolders\Equipment_Odyssey_FCP_Enclave' = 'FIP-H&HTools\LoadFolders\Odyssey_FCP_Enclave'
}
foreach ($entry in $hhRenames.GetEnumerator()) {
    Rename-ReleaseDirectory $entry.Key $entry.Value
}

# Corvega needs a safe unconditional module folder even with no vehicle mods.
$corvegaBase = Assert-InReleaseRoot (Join-Path $releaseRoot 'FIP-Corvega\LoadFolders\Corvega\Languages\English\Keyed')
if (-not (Test-Path -LiteralPath $corvegaBase)) {
    New-Item -ItemType Directory -Path $corvegaBase | Out-Null
}

# Move all Lucky 38 textures out of the optional coffee/tea folder.
$optionalLuckyTextures = Join-Path $releaseRoot 'FIP-Lucky 38\LoadFolders\FCPPlants_CoffeeTea\Textures'
$baseLuckyTextures = Join-Path $releaseRoot 'FIP-Lucky 38\LoadFolders\Lucky38\Textures'
if (Test-Path -LiteralPath $optionalLuckyTextures) {
    Merge-ReleaseDirectory 'FIP-Lucky 38\LoadFolders\FCPPlants_CoffeeTea\Textures' 'FIP-Lucky 38\LoadFolders\Lucky38\Textures'
}

Write-Output 'Structural directory refactor completed.'
