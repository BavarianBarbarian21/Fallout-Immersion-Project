$ErrorActionPreference = 'Stop'

$repository = 'C:\Users\Matthias\Desktop\Fallout Immersion Project'
$stage = Join-Path $repository 'Guidelines\1.0 Release'
$sunset = Join-Path $repository 'New-Mods\FIP-Sunset\LoadFolders'
$oldBigMT = Join-Path $repository 'FIP-Big MT\LoadFolders'

function Assert-InStage([string]$Path) {
    $resolvedStage = [IO.Path]::GetFullPath($stage).TrimEnd('\') + '\'
    $resolvedPath = [IO.Path]::GetFullPath($Path)
    if (-not $resolvedPath.StartsWith($resolvedStage, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to mutate a path outside the 1.0 staging tree: $resolvedPath"
    }
}

function Ensure-Directory([string]$Path) {
    Assert-InStage $Path
    [IO.Directory]::CreateDirectory($Path) | Out-Null
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    Assert-InStage $Path
    Ensure-Directory ([IO.Path]::GetDirectoryName($Path))
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function Copy-Tree([string]$Source, [string]$Destination, [hashtable]$Replacements) {
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) { throw "Missing source: $Source" }
    Ensure-Directory $Destination
    foreach ($file in Get-ChildItem -LiteralPath $Source -Recurse -File) {
        $relative = $file.FullName.Substring($Source.Length).TrimStart('\')
        foreach ($key in $Replacements.Keys) { $relative = $relative.Replace($key, $Replacements[$key]) }
        $target = Join-Path $Destination $relative
        Ensure-Directory ([IO.Path]::GetDirectoryName($target))
        if ($file.Extension -eq '.xml') {
            $text = [IO.File]::ReadAllText($file.FullName)
            foreach ($key in $Replacements.Keys) { $text = $text.Replace($key, $Replacements[$key]) }
            $text = $text.Replace('FIP-H&HTools/', 'FIP-H&amp;HTools/')
            Write-Utf8NoBom $target $text
        } else {
            Assert-InStage $target
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    }
}

function Remove-StageDirectory([string]$Path) {
    Assert-InStage $Path
    if (Test-Path -LiteralPath $Path) { Remove-Item -LiteralPath $Path -Recurse -Force }
}

function Save-Xml([xml]$Document, [string]$Path) {
    Assert-InStage $Path
    Ensure-Directory ([IO.Path]::GetDirectoryName($Path))
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Encoding = [Text.UTF8Encoding]::new($false)
    $settings.Indent = $true
    $settings.NewLineChars = "`r`n"
    $settings.NewLineHandling = [Xml.NewLineHandling]::Entitize
    $writer = [Xml.XmlWriter]::Create($Path, $settings)
    try { $Document.Save($writer) } finally { $writer.Dispose() }
}

function New-LanguageDocument {
    $doc = [Xml.XmlDocument]::new()
    $decl = $doc.CreateXmlDeclaration('1.0', 'utf-8', $null)
    $doc.AppendChild($decl) | Out-Null
    $doc.AppendChild($doc.CreateElement('LanguageData')) | Out-Null
    return ,$doc
}

# Sunset is retired: merge every feature into H&H Tools under its actual optional condition.
$hhLoadFolders = Join-Path $stage 'FIP-H&HTools\LoadFolders'
$sunsetMap = [ordered]@{
    'Core_Medieval2' = 'Medieval2_Framework'
    'Medieval2' = 'Medieval2'
    'Medieval2_FCPAnimals' = 'Medieval2_FCP_Animals'
    'Medieval2_HHTools' = 'Medieval2'
    'Medieval2_Ideology' = 'Medieval2_Ideology'
    'SettlersModule' = 'Settlers'
    'SettlersModule_FCPAnimals' = 'Settlers_FCP_Animals'
    'SettlersModule_HHTools' = 'Settlers'
    'SettlersModule_HHTools_FCPAnimals' = 'Settlers_FCP_Animals'
    'Tribals' = 'Tribals'
}
$sunsetReplacements = @{
    'FIP-Sunset' = 'FIP-H&HTools'
    'Sunset_' = 'HHTools_'
}
foreach ($entry in $sunsetMap.GetEnumerator()) {
    Copy-Tree (Join-Path $sunset $entry.Key) (Join-Path $hhLoadFolders $entry.Value) $sunsetReplacements
}

$hhTextureRoot = Join-Path $hhLoadFolders 'HHTools\Textures\FIP-H&HTools'
foreach ($textureFolder in @('ArcheryTarget', 'TrainingDummy')) {
    $sourceTexture = Join-Path $sunset ("Base\Textures\FIP-Sunset\$textureFolder")
    Copy-Tree $sourceTexture (Join-Path $hhTextureRoot $textureFolder) $sunsetReplacements
}

# Split Sunset's mixed unconditional keyed translations into the matching optional integrations.
[xml]$sunsetKeys = [IO.File]::ReadAllText((Join-Path $sunset 'Base\Languages\English\Keyed\Sunset_Medieval.xml'))
$keyTargets = @{
    Medieval2 = New-LanguageDocument
    Settlers = New-LanguageDocument
    Tribals = New-LanguageDocument
}
foreach ($node in @($sunsetKeys.LanguageData.ChildNodes | Where-Object NodeType -eq Element)) {
    $targetName = if ($node.Name.StartsWith('VFEM2_')) { 'Medieval2' }
                  elseif ($node.Name.StartsWith('VFET')) { 'Tribals' }
                  else { 'Settlers' }
    $imported = $keyTargets[$targetName].ImportNode($node, $true)
    $keyTargets[$targetName].DocumentElement.AppendChild($imported) | Out-Null
}
foreach ($targetName in $keyTargets.Keys) {
    $path = Join-Path $hhLoadFolders "$targetName\Languages\English\Keyed\HHTools_$targetName.xml"
    Save-Xml $keyTargets[$targetName] $path
}

# The mutant/ghoul/Numen name makers are WestTek content. Their replacement defs and string files
# are installed separately; remove the obsolete H&H-owned copies.
$hhNameDef = Join-Path $hhLoadFolders 'HHTools\Defs\FIP-H&HTools\Cultures\HHTools_PawnNameMakerDef.xml'
[xml]$nameDoc = [IO.File]::ReadAllText($hhNameDef)
$movedNameDefs = @(
    'HHTools_PawnNameMaker_SuperMutant_Male', 'HHTools_PawnNameMaker_Mutant_Female',
    'HHTools_PawnNameMaker_Ghoul_Male', 'HHTools_PawnNameMaker_Ghoul_Female',
    'HHTools_PawnNameMaker_Numen_Male', 'HHTools_PawnNameMaker_Numen_Female'
)
foreach ($defName in $movedNameDefs) {
    $node = $nameDoc.SelectSingleNode("/Defs/RulePackDef[defName='$defName']")
    if ($node) { $node.ParentNode.RemoveChild($node) | Out-Null }
}
Save-Xml $nameDoc $hhNameDef
foreach ($stem in @('Ghoul', 'Mutant', 'Numen')) {
    $obsolete = Join-Path $hhLoadFolders "HHTools\Languages\English\Strings\Names\HHTools_Names_$stem.txt"
    Assert-InStage $obsolete
    if (Test-Path -LiteralPath $obsolete) { Remove-Item -LiteralPath $obsolete -Force }
}

# Big MT owns all anomaly content while retaining a safe unconditional dummy base.
$bigLoadFolders = Join-Path $stage 'FIP-Big MT\LoadFolders'
Copy-Tree (Join-Path $oldBigMT 'Base') (Join-Path $bigLoadFolders 'Anomaly') @{}
Copy-Tree (Join-Path $oldBigMT 'WestTek') (Join-Path $bigLoadFolders 'Anomaly_WestTek') @{}
Copy-Tree (Join-Path $hhLoadFolders 'Anomaly') (Join-Path $bigLoadFolders 'Anomaly') @{
    'FIP-H&HTools' = 'FIP-BigMT'; 'HHTools_' = 'BigMT_'
}
Copy-Tree (Join-Path $hhLoadFolders 'Equipment_Ludeon_RimWorld_Anomaly') (Join-Path $bigLoadFolders 'Anomaly') @{
    'FIP-H&HTools' = 'FIP-BigMT'; 'HHTools_' = 'BigMT_'
}
Copy-Tree (Join-Path $hhLoadFolders 'Equipment_VanillaExpanded_VAnomalyEInsanity') (Join-Path $bigLoadFolders 'Anomaly_Insanity') @{
    'FIP-H&HTools' = 'FIP-BigMT'; 'HHTools_' = 'BigMT_'
}
Remove-StageDirectory (Join-Path $hhLoadFolders 'Anomaly')
Remove-StageDirectory (Join-Path $hhLoadFolders 'Equipment_Ludeon_RimWorld_Anomaly')
Remove-StageDirectory (Join-Path $hhLoadFolders 'Equipment_VanillaExpanded_VAnomalyEInsanity')

# Empire has one owner. Extract Whitespring's normal-material wording into a mutually exclusive
# variant, install Donaustahl's Saturnite wording as the other variant, and move Royalty-only
# prestige armor wording to Whitespring as well.
$whiteLoadFolders = Join-Path $stage 'FIP-Whitespring\LoadFolders'
$whiteEmpire = Join-Path $whiteLoadFolders 'Empire'
$standardVariant = Join-Path $whiteLoadFolders 'Empire_StandardMaterials'
$saturniteVariant = Join-Path $whiteLoadFolders 'Empire_Donaustahl'
$variantFiles = @{
    'Languages\English\DefInjected\RoyalTitlePermitDef\Whitespring_Empire.xml' = @(
        'VFEI_PlasteelDrop.label', 'VFEI_PlasteelDrop.description'
    )
    'Languages\English\DefInjected\ThingDef\Whitespring_Empire.xml' = @(
        'VFEE_Apparel_AbsolverHelmet.description', 'VFEE_Apparel_ArmorAbsolver.description',
        'VFEE_Apparel_DeserterHelmet.description', 'VFEE_Apparel_JanissaryHelmet.description',
        'VFEE_Apparel_ArmorDeserter.description', 'VFEE_Apparel_JanissaryCuirass.description',
        'VFEE_MeleeWeapon_ToxbladeBladelink.description', 'VFEE_MeleeWeapon_Toxblade.description'
    )
}
foreach ($entry in $variantFiles.GetEnumerator()) {
    $sourcePath = Join-Path $whiteEmpire $entry.Key
    [xml]$sourceDoc = [IO.File]::ReadAllText($sourcePath)
    $variantDoc = New-LanguageDocument
    foreach ($key in $entry.Value) {
        $node = $sourceDoc.SelectSingleNode("/LanguageData/*[name()='$key']")
        if (-not $node) { throw "Missing expected Empire language key $key in $sourcePath" }
        $variantDoc.DocumentElement.AppendChild($variantDoc.ImportNode($node, $true)) | Out-Null
        $node.ParentNode.RemoveChild($node) | Out-Null
    }
    Save-Xml $sourceDoc $sourcePath
    Save-Xml $variantDoc (Join-Path $standardVariant $entry.Key)
}

$donauLoadFolders = Join-Path $stage 'FIP-Donaustahl\LoadFolders'
Copy-Tree (Join-Path $donauLoadFolders 'Empire') $saturniteVariant @{
    'Donaustahl_SaturniteEmpire' = 'Whitespring_Empire_Donaustahl'
}
Copy-Tree (Join-Path $donauLoadFolders 'Whitespring') (Join-Path $whiteLoadFolders 'Donaustahl') @{
    'Donaustahl_SaturniteWhitespring' = 'Whitespring_Donaustahl'
}
Remove-StageDirectory (Join-Path $donauLoadFolders 'Empire')
Remove-StageDirectory (Join-Path $donauLoadFolders 'Whitespring')

Write-Output 'Ownership migration completed: Sunset->HHTools, Anomaly->BigMT, Empire->Whitespring, xenotype names->WestTek.'
