param(
    [string]$SourceRoot,
    [string]$DestinationRoot,
    [string]$LanguageFolderName = 'German',
    [string[]]$LookupRoots,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$translationModName = 'FIP-Translation Part 1'
$languageFolderName = $LanguageFolderName
$reportDirectory = Join-Path $PSScriptRoot 'Reports'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fallbackListFanoutDefault = 10
$fallbackListFanoutWide = 16

if (-not $PSBoundParameters.ContainsKey('SourceRoot')) {
    $SourceRoot = $repositoryRoot
}

if (-not $PSBoundParameters.ContainsKey('DestinationRoot')) {
    $DestinationRoot = $repositoryRoot
}

if (-not $PSBoundParameters.ContainsKey('LookupRoots')) {
    $LookupRoots = @($SourceRoot)

    foreach ($candidateRoot in @('C:\Modding\Rimworld\Data', 'C:\Modding\Rimworld\FCP Mods')) {
        if (Test-Path -LiteralPath $candidateRoot) {
            $LookupRoots += $candidateRoot
        }
    }
}

$LookupRoots = @(Get-MinimalRootSet -Roots $LookupRoots)
$script:XmlDocumentCache = @{}
$script:DefNameIndex = @{}
$script:NameAttributeIndex = @{}
$script:WildcardDefTypeFallbacks = @{
    'VVE_Traveller' = @('ThingDef')
    'VVE_Traveller_Blueprint' = @('ThingDef')
    'VVE_Highwayman' = @('ThingDef')
    'VVE_Highwayman_Blueprint' = @('ThingDef')
    'VVE_Prowler' = @('ThingDef')
    'VVE_Prowler_Blueprint' = @('ThingDef')
    'VVE_Roadrunner' = @('ThingDef')
    'VVE_Roadrunner_Blueprint' = @('ThingDef')
    'VVE_Roadkill' = @('ThingDef')
    'VVE_Roadkill_Blueprint' = @('ThingDef')
    'VVE_Lightning' = @('ThingDef')
    'VVE_Lightning_Blueprint' = @('ThingDef')
    'VVE_Traveller_UpgradeTree' = @('VehicleUpgradeTreeDef', 'UpgradeTreeDef')
    'VVE_Highwayman_UpgradeTree' = @('VehicleUpgradeTreeDef', 'UpgradeTreeDef')
    'VVE_Prowler_UpgradeTree' = @('VehicleUpgradeTreeDef', 'UpgradeTreeDef')
    'VVE_Roadkill_UpgradeTree' = @('VehicleUpgradeTreeDef', 'UpgradeTreeDef')
    'VPE_Empath' = @('PsycastPathDef')
    'VPE_Skipmaster' = @('PsycastPathDef')
    'VPE_Conflagrator' = @('PsycastPathDef')
    'VPE_Protector' = @('PsycastPathDef')
    'VPE_Warlord' = @('PsycastPathDef')
    'VPE_Chronopath' = @('PsycastPathDef')
    'VPE_Archotechist' = @('PsycastPathDef')
    'VPE_Necropath' = @('PsycastPathDef')
    'VPE_Staticlord' = @('PsycastPathDef')
    'VPE_Archon' = @('PsycastPathDef')
    'VPE_Frostshaper' = @('PsycastPathDef')
    'VPE_Wildspeaker' = @('PsycastPathDef')
    'VPE_Harmonist' = @('PsycastPathDef')
    'VPE_Technomancer' = @('PsycastPathDef')
    'VPE_Nightstalker' = @('PsycastPathDef')
    'VPEH_Hemosage' = @('PsycastPathDef')
    'VPEP_Puppeteer' = @('PsycastPathDef')
    'CashRegister_CashRegister' = @('ThingDef')
    'CashRegister_Mech' = @('ThingDef')
    'ColdBath' = @('ThoughtDef')
    'HeatedPool' = @('ThoughtDef')
    'HotBath' = @('ThoughtDef')
    'HotShower' = @('ThoughtDef')
    'MedievalVendingMachine' = @('ThingDef')
    'Museums_Sign' = @('ThingDef')
    'Museums_TourGatheringSpot' = @('ThingDef')
    'SmallHotSpring' = @('ThingDef')
    'Therapist' = @('ThingDef')
    'Gastronomy_Waiting' = @('WorkTypeDef')
    'HospitalitySpa_WithdrawVendingMachineEarnings' = @('WorkGiverDef', 'WorkTypeDef', 'JobDef')
    'VendingMachine' = @('ThingDef')
    'Facility_VitalsCentre' = @('ThingDef')
    'VFEFactory_Autofarmer' = @('ThingDef')
    'VFEFactory_AutomatedBiofuelRefinery' = @('ThingDef')
    'VFEFactory_AutomatedDrillPlatform' = @('ThingDef')
    'VFEFactory_AutomatedMasonrySaw' = @('ThingDef')
    'VFEFactory_AutomatedSmelter' = @('ThingDef')
    'VFEFactory_Conveyor' = @('ThingDef')
    'VFEFactory_FactoryHopper' = @('ThingDef')
    'VFEFactory_UndergroundConveyorEntrance' = @('ThingDef')
    'VFEFactory_UndergroundConveyorExit' = @('ThingDef')
    'VNPE_NutrientPasteDripper' = @('ThingDef')
    'VNPE_NutrientPasteFeeder' = @('ThingDef')
    'VNPE_NutrientPasteGrinder' = @('ThingDef')
    'VNPE_NutrientPasteTap' = @('ThingDef')
    'VNPE_NutrientPasteVat' = @('ThingDef')
    'GeneratedAsteroid' = @('WorldObjectDef')
    'SpaceSettlement' = @('WorldObjectDef')
    'AsteroidMiningSite' = @('WorldObjectDef')
    'Building_ChemshineBarrel' = @('ThingDef')
    'ChemBoiler' = @('ThingDef')
    'Chemlamp' = @('ThingDef')
    'ChemlampPost' = @('ThingDef')
    'Chemshine' = @('ThingDef')
    'Chemshined' = @('ThoughtDef', 'HediffDef')
    'DiegoDire' = @('StorytellerDef')
    'Plant_Chemroot' = @('ThingDef')
    'TableFaro' = @('ThingDef')
    'VFEM2_AlchemicalWorkbench' = @('ThingDef')
    'VFEM2_AlchemicalWorkbench_Electric' = @('ThingDef')
    'VFEM2_Alchemy' = @('ResearchProjectDef')
    'VFEM2_AmnesiaDraught' = @('ThingDef')
    'VFEM2_ArcheryTarget' = @('ThingDef')
    'VFEM2_CivilClan' = @('FactionDef')
    'VFEM2_ClanRough' = @('FactionDef')
    'VFEM2_ClanSavage' = @('FactionDef')
    'VFEM2_ComaDraught' = @('ThingDef')
    'VFEM2_DestroyedMerchantGuildCamp' = @('SitePartDef', 'WorldObjectDef')
    'VFEM2_EfficiencyDraught' = @('ThingDef')
    'VFEM2_FertilityDraught' = @('ThingDef')
    'VFEM2_FocusDraught' = @('ThingDef')
    'VFEM2_HealingDraught' = @('ThingDef')
    'VFEM2_ImmunizationDraught' = @('ThingDef')
    'VFEM2_InspirationDraught' = @('ThingDef')
    'VFEM2_KingdomCivil' = @('FactionDef')
    'VFEM2_KingdomRough' = @('FactionDef')
    'VFEM2_KingdomSavage' = @('FactionDef')
    'VFEM2_MerchantGuild' = @('FactionDef')
    'VFEM2_MerchantGuildTrader' = @('TraderKindDef', 'VEF.Planet.MovingBaseDef')
    'VFEM2_NewKingdom' = @('ScenarioDef')
    'VFEM2_PainkillerDraught' = @('ThingDef')
    'VFEM2_Plant_Grape' = @('ThingDef')
    'VFEM2_PoisonDraught' = @('ThingDef')
    'VFEM2_StoneskinDraught' = @('ThingDef')
    'VFEM2_StrengthDraught' = @('ThingDef')
    'VFEM2_TorturerDraught' = @('ThingDef')
    'VFEM2_TrainingDummy' = @('ThingDef')
    'VFES_Bandits' = @('ScenarioDef', 'FactionDef')
    'VFES_ChemfuelToChemshine' = @('RecipeDef', 'JobDef')
    'FillChemshineBarrel' = @('RecipeDef', 'JobDef')
    'TakeChemshineOutOfChemshineBarrel' = @('RecipeDef', 'JobDef')
    'SettlerCivil' = @('FactionDef')
    'SettlerRough' = @('FactionDef')
    'VFEE_TerribleExhibit' = @('ThoughtDef')
}

function New-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        $null = New-Item -Path $Path -ItemType Directory -Force
    }
}

function ConvertTo-SafeFileStem {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return 'Unknown'
    }

    $sanitized = [System.Text.RegularExpressions.Regex]::Replace($Value, '[^A-Za-z0-9]+', '_')
    return $sanitized.Trim('_')
}

function Get-MinimalRootSet {
    param([string[]]$Roots)

    $selected = New-Object 'System.Collections.Generic.List[string]'
    $normalizedRoots = @(
        $Roots |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_) } |
            ForEach-Object { (Resolve-Path -LiteralPath $_).Path.TrimEnd('\\') } |
            Sort-Object Length, @{ Expression = { $_ } } -Unique
    )

    foreach ($root in $normalizedRoots) {
        $isNested = $false
        foreach ($selectedRoot in $selected) {
            if ($root.StartsWith($selectedRoot + '\\', [System.StringComparison]::OrdinalIgnoreCase)) {
                $isNested = $true
                break
            }
        }

        if (-not $isNested) {
            $selected.Add($root)
        }
    }

    return @($selected)
}

function Get-ElementChildren {
    param([System.Xml.XmlNode]$Node)

    $children = @()
    foreach ($child in $Node.ChildNodes) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element) {
            $children += $child
        }
    }
    return [System.Xml.XmlNode[]]$children
}

function Add-TranslationEntry {
    param(
        [System.Collections.Generic.List[object]]$Entries,
        [string]$ModFolderName,
        [string]$SourceKind,
        [string]$DefType,
        [string]$DefName,
        [string[]]$PathSegments,
        [string]$Text,
        [string]$SourceFile
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return
    }

    $entryPath = ($PathSegments -join '.')
    $Entries.Add([pscustomobject]@{
        ModFolderName = $ModFolderName
        SourceKind    = $SourceKind
        DefType       = $DefType
        DefName       = $DefName
        Path          = $entryPath
        Text          = $Text.Trim()
        SourceFile    = $SourceFile
        Key           = "$DefType|$DefName|$entryPath"
    })
}

function Walk-DefTree {
    param(
        [System.Xml.XmlNode]$Node,
        [string[]]$Segments,
        [System.Collections.Generic.List[object]]$Entries,
        [string]$ModFolderName,
        [string]$SourceKind,
        [string]$DefType,
        [string]$DefName,
        [string]$SourceFile
    )

    $children = @(Get-ElementChildren -Node $Node)
    if ($children.Count -eq 0) {
        return
    }

    $allListItems = $true
    foreach ($child in $children) {
        if ($child.LocalName -ne 'li') {
            $allListItems = $false
            break
        }
    }

    if ($allListItems) {
        for ($index = 0; $index -lt $children.Count; $index++) {
            Walk-DefTree -Node $children[$index] -Segments ($Segments + [string]$index) -Entries $Entries -ModFolderName $ModFolderName -SourceKind $SourceKind -DefType $DefType -DefName $DefName -SourceFile $SourceFile
        }
        return
    }

    foreach ($child in $children) {
        $childSegments = $Segments + $child.LocalName
        if (($child.LocalName -eq 'label' -or $child.LocalName -eq 'description') -and -not [string]::IsNullOrWhiteSpace($child.InnerText)) {
            Add-TranslationEntry -Entries $Entries -ModFolderName $ModFolderName -SourceKind $SourceKind -DefType $DefType -DefName $DefName -PathSegments $childSegments -Text $child.InnerText -SourceFile $SourceFile
        }

        Walk-DefTree -Node $child -Segments $childSegments -Entries $Entries -ModFolderName $ModFolderName -SourceKind $SourceKind -DefType $DefType -DefName $DefName -SourceFile $SourceFile
    }
}

function Parse-PathSegment {
    param([string]$Segment)

    if ($Segment -match '^li\[(\d+)\]$') {
        return [string]([int]$Matches[1] - 1)
    }

    if ($Segment -eq 'li') {
        return $null
    }

    if ($Segment -match '^(?<name>[A-Za-z0-9_]+)$') {
        return $Matches.name
    }

    return $null
}

function Convert-XPathToTarget {
    param([string]$XPath)

    $pattern = '^/?Defs/(?<defType>[^/\[]+)\[(?<selector>defName|@Name)="(?<defName>[^"]+)"\](?<tail>/.*)?$'
    $match = [System.Text.RegularExpressions.Regex]::Match($XPath, $pattern)
    if (-not $match.Success) {
        return $null
    }

    $segments = @()
    $tail = $match.Groups['tail'].Value
    if (-not [string]::IsNullOrWhiteSpace($tail)) {
        foreach ($rawSegment in ($tail.Trim('/') -split '/')) {
            if ([string]::IsNullOrWhiteSpace($rawSegment)) {
                continue
            }

            $segments += $rawSegment
        }
    }

    return [pscustomobject]@{
        DefType  = $match.Groups['defType'].Value
        Selector = $match.Groups['selector'].Value
        DefName  = $match.Groups['defName'].Value
        Segments = $segments
    }
}

function Test-ContainsTranslatableValue {
    param([System.Xml.XmlNode]$Node)

    foreach ($child in @(Get-ElementChildren -Node $Node)) {
        if (($child.LocalName -eq 'label' -or $child.LocalName -eq 'description') -and -not [string]::IsNullOrWhiteSpace($child.InnerText)) {
            return $true
        }

        if (Test-ContainsTranslatableValue -Node $child) {
            return $true
        }
    }

    return $false
}

function Walk-PatchValue {
    param(
        [System.Xml.XmlNode[]]$Nodes,
        [string[]]$Segments,
        [System.Collections.Generic.List[object]]$Entries,
        [string]$ModFolderName,
        [string]$DefType,
        [string]$DefName,
        [string]$SourceFile
    )

    if ($Nodes.Count -eq 0) {
        return
    }

    $allListItems = $true
    foreach ($node in $Nodes) {
        if ($node.LocalName -ne 'li') {
            $allListItems = $false
            break
        }
    }

    if ($allListItems) {
        for ($index = 0; $index -lt $Nodes.Count; $index++) {
            Walk-DefTree -Node $Nodes[$index] -Segments ($Segments + [string]$index) -Entries $Entries -ModFolderName $ModFolderName -SourceKind 'Patch' -DefType $DefType -DefName $DefName -SourceFile $SourceFile
        }
        return
    }

    foreach ($node in $Nodes) {
        if (($node.LocalName -eq 'label' -or $node.LocalName -eq 'description') -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
            Add-TranslationEntry -Entries $Entries -ModFolderName $ModFolderName -SourceKind 'Patch' -DefType $DefType -DefName $DefName -PathSegments ($Segments + $node.LocalName) -Text $node.InnerText -SourceFile $SourceFile
            continue
        }

        Walk-DefTree -Node $node -Segments $Segments -Entries $Entries -ModFolderName $ModFolderName -SourceKind 'Patch' -DefType $DefType -DefName $DefName -SourceFile $SourceFile
    }
}

function Get-ValueElementChildren {
    param([System.Xml.XmlNode]$Node)

    $elements = @()
    foreach ($child in $Node.ChildNodes) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element) {
            $elements += $child
        }
    }
    return [System.Xml.XmlNode[]]$elements
}

function Get-DirectChildElement {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$LocalName
    )

    foreach ($child in @(Get-ElementChildren -Node $Node)) {
        if ($child.LocalName -eq $LocalName) {
            return $child
        }
    }

    return $null
}

function Copy-LanguageFiles {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$Pattern,
        [System.Collections.Generic.List[object]]$CopiedFiles,
        [string]$ModFolderName,
        [string]$Category
    )

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        return
    }

    New-Directory -Path $DestinationPath
    foreach ($sourceFile in Get-ChildItem -LiteralPath $SourcePath -Filter $Pattern -File) {
        $destinationFile = Join-Path $DestinationPath $sourceFile.Name
        Copy-Item -LiteralPath $sourceFile.FullName -Destination $destinationFile -Force
        $CopiedFiles.Add([pscustomobject]@{
            ModFolderName = $ModFolderName
            Category      = $Category
            Source        = $sourceFile.FullName
            Destination   = $destinationFile
        })
    }
}

function Get-CollectionCount {
    param($Value)

    if ($null -eq $Value) {
        return 0
    }

    if ($Value -is [System.Collections.ICollection]) {
        return $Value.Count
    }

    return (@($Value) | Measure-Object).Count
}

function Get-CachedXmlDocument {
    param([string]$FilePath)

    if (-not $script:XmlDocumentCache.ContainsKey($FilePath)) {
        $script:XmlDocumentCache[$FilePath] = [xml](Get-Content -LiteralPath $FilePath -Raw)
    }

    return $script:XmlDocumentCache[$FilePath]
}

function Add-DefIndexEntry {
    param(
        [hashtable]$Index,
        [string]$Key,
        [pscustomobject]$Record
    )

    if ([string]::IsNullOrWhiteSpace($Key)) {
        return
    }

    if (-not $Index.ContainsKey($Key)) {
        $Index[$Key] = New-Object 'System.Collections.Generic.List[object]'
    }

    $Index[$Key].Add($Record)
}

function Build-DefIndex {
    param([string[]]$Roots)

    $seenFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($root in $Roots) {
        foreach ($defsDirectory in Get-ChildItem -LiteralPath $root -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'Defs' }) {
            foreach ($file in Get-ChildItem -LiteralPath $defsDirectory.FullName -Filter '*.xml' -File -Recurse -ErrorAction SilentlyContinue) {
                if (-not $seenFiles.Add($file.FullName)) {
                    continue
                }

                try {
                    $document = Get-CachedXmlDocument -FilePath $file.FullName
                    $defsRoot = $document.SelectSingleNode('/Defs')
                    if ($null -eq $defsRoot) {
                        continue
                    }

                    foreach ($defNode in @(Get-ElementChildren -Node $defsRoot)) {
                        $defType = $defNode.LocalName
                        $defNameNode = Get-DirectChildElement -Node $defNode -LocalName 'defName'
                        $nameAttribute = $defNode.Attributes['Name']

                        if ($null -eq $defNameNode -and $null -eq $nameAttribute) {
                            continue
                        }

                        $record = [pscustomobject]@{
                            DefType      = $defType
                            FilePath     = $file.FullName
                            DefName      = if ($null -ne $defNameNode) { $defNameNode.InnerText.Trim() } else { $null }
                            NameAttr     = if ($null -ne $nameAttribute) { $nameAttribute.Value.Trim() } else { $null }
                        }

                        Add-DefIndexEntry -Index $script:DefNameIndex -Key $record.DefName -Record $record
                        Add-DefIndexEntry -Index $script:NameAttributeIndex -Key $record.NameAttr -Record $record
                    }
                }
                catch {
                    continue
                }
            }
        }
    }
}

function Get-TargetRecords {
    param([pscustomobject]$Target)

    $index = if ($Target.Selector -eq '@Name') { $script:NameAttributeIndex } else { $script:DefNameIndex }
    if (-not $index.ContainsKey($Target.DefName)) {
        return @()
    }

    $records = @()
    foreach ($record in $index[$Target.DefName]) {
        $records += $record
    }

    if ($Target.DefType -eq '*') {
        return $records
    }

    return @($records | Where-Object { $_.DefType -eq $Target.DefType })
}

function Get-TargetDefNode {
    param(
        [pscustomobject]$Target,
        [pscustomobject]$Record
    )

    $document = Get-CachedXmlDocument -FilePath $Record.FilePath
    $defsRoot = $document.SelectSingleNode('/Defs')
    if ($null -eq $defsRoot) {
        return $null
    }

    foreach ($defNode in @(Get-ElementChildren -Node $defsRoot)) {
        if ($defNode.LocalName -ne $Record.DefType) {
            continue
        }

        if ($Target.Selector -eq '@Name') {
            $nameAttribute = $defNode.Attributes['Name']
            if ($null -ne $nameAttribute -and $nameAttribute.Value -eq $Target.DefName) {
                return $defNode
            }
            continue
        }

        $defNameNode = Get-DirectChildElement -Node $defNode -LocalName 'defName'
        if ($null -ne $defNameNode -and $defNameNode.InnerText.Trim() -eq $Target.DefName) {
            return $defNode
        }
    }

    return $null
}

function Get-DirectChildElementsByName {
    param(
        [System.Xml.XmlNode]$Node,
        [string]$LocalName
    )

    $matches = @()
    foreach ($child in @(Get-ElementChildren -Node $Node)) {
        if ($child.LocalName -eq $LocalName) {
            $matches += $child
        }
    }

    return [System.Xml.XmlNode[]]$matches
}

function Resolve-XPathSegmentsAgainstNode {
    param(
        [System.Xml.XmlNode]$DefNode,
        [string[]]$RawSegments
    )

    $states = @([pscustomobject]@{ Node = $DefNode; Segments = @() })

    foreach ($rawSegment in $RawSegments) {
        $nextStates = @()

        foreach ($state in $states) {
            $currentNode = $state.Node

            if ($rawSegment -match '^li\[(\d+)\]$') {
                $liNodes = @(Get-DirectChildElementsByName -Node $currentNode -LocalName 'li')
                $index = [int]$Matches[1] - 1
                if ($index -ge 0 -and $index -lt $liNodes.Count) {
                    $nextStates += [pscustomobject]@{ Node = $liNodes[$index]; Segments = ($state.Segments + [string]$index) }
                }
                continue
            }

            if ($rawSegment -eq 'li') {
                $liNodes = @(Get-DirectChildElementsByName -Node $currentNode -LocalName 'li')
                for ($index = 0; $index -lt $liNodes.Count; $index++) {
                    $nextStates += [pscustomobject]@{ Node = $liNodes[$index]; Segments = ($state.Segments + [string]$index) }
                }
                continue
            }

            if ($rawSegment -match '^li\[key="([^"]+)"\]$') {
                $expectedKey = $Matches[1]
                $liNodes = @(Get-DirectChildElementsByName -Node $currentNode -LocalName 'li')
                for ($index = 0; $index -lt $liNodes.Count; $index++) {
                    $keyNode = Get-DirectChildElement -Node $liNodes[$index] -LocalName 'key'
                    if ($null -ne $keyNode -and $keyNode.InnerText.Trim() -eq $expectedKey) {
                        $nextStates += [pscustomobject]@{ Node = $liNodes[$index]; Segments = ($state.Segments + [string]$index) }
                    }
                }
                continue
            }

            if ($rawSegment -match '^li\[@Class="([^"]+)"\]\[(\d+)\]$') {
                $expectedClass = $Matches[1]
                $matchIndex = [int]$Matches[2] - 1
                $liNodes = @(Get-DirectChildElementsByName -Node $currentNode -LocalName 'li')
                $matchingStates = @()
                for ($index = 0; $index -lt $liNodes.Count; $index++) {
                    $classAttribute = $liNodes[$index].Attributes['Class']
                    if ($null -ne $classAttribute -and $classAttribute.Value -eq $expectedClass) {
                        $matchingStates += [pscustomobject]@{ Node = $liNodes[$index]; Segments = ($state.Segments + [string]$index) }
                    }
                }
                if ($matchIndex -ge 0 -and $matchIndex -lt $matchingStates.Count) {
                    $nextStates += $matchingStates[$matchIndex]
                }
                continue
            }

            if ($rawSegment -match '^li\[@Class="([^"]+)"\]$') {
                $expectedClass = $Matches[1]
                $liNodes = @(Get-DirectChildElementsByName -Node $currentNode -LocalName 'li')
                for ($index = 0; $index -lt $liNodes.Count; $index++) {
                    $classAttribute = $liNodes[$index].Attributes['Class']
                    if ($null -ne $classAttribute -and $classAttribute.Value -eq $expectedClass) {
                        $nextStates += [pscustomobject]@{ Node = $liNodes[$index]; Segments = ($state.Segments + [string]$index) }
                    }
                }
                continue
            }

            if ($rawSegment -match '^li\[text\(\)="([^"]+)"\]$') {
                $expectedText = $Matches[1]
                $liNodes = @(Get-DirectChildElementsByName -Node $currentNode -LocalName 'li')
                for ($index = 0; $index -lt $liNodes.Count; $index++) {
                    if ($liNodes[$index].InnerText.Trim() -eq $expectedText) {
                        $nextStates += [pscustomobject]@{ Node = $liNodes[$index]; Segments = ($state.Segments + [string]$index) }
                    }
                }
                continue
            }

            if ($rawSegment -match '^li\[contains\(text\(\),"([^"]+)"\)\]$') {
                $expectedFragment = $Matches[1]
                $liNodes = @(Get-DirectChildElementsByName -Node $currentNode -LocalName 'li')
                for ($index = 0; $index -lt $liNodes.Count; $index++) {
                    if ($liNodes[$index].InnerText -like "*$expectedFragment*") {
                        $nextStates += [pscustomobject]@{ Node = $liNodes[$index]; Segments = ($state.Segments + [string]$index) }
                    }
                }
                continue
            }

            if ($rawSegment -match '^(?<name>[A-Za-z0-9_]+)$') {
                foreach ($child in @(Get-DirectChildElementsByName -Node $currentNode -LocalName $Matches.name)) {
                    $nextStates += [pscustomobject]@{ Node = $child; Segments = ($state.Segments + $Matches.name) }
                }
                continue
            }
        }

        $states = @($nextStates)
        if ($states.Count -eq 0) {
            return @()
        }
    }

    return @($states | ForEach-Object {
        [pscustomobject]@{
            Segments = $_.Segments
        }
    })
}

function Get-FallbackDefTypes {
    param([pscustomobject]$Target)

    if ($Target.DefType -ne '*') {
        return @($Target.DefType)
    }

    if ($script:WildcardDefTypeFallbacks.ContainsKey($Target.DefName)) {
        return @($script:WildcardDefTypeFallbacks[$Target.DefName])
    }

    return @()
}

function Get-FallbackFanoutCount {
    param([string]$PreviousSegment)

    if ($PreviousSegment -in @('nodes', 'rulesStrings')) {
        return $fallbackListFanoutWide
    }

    return $fallbackListFanoutDefault
}

function Resolve-FallbackSegments {
    param([string[]]$RawSegments)

    $states = @([pscustomobject]@{ Segments = @() })

    for ($segmentIndex = 0; $segmentIndex -lt $RawSegments.Count; $segmentIndex++) {
        $rawSegment = $RawSegments[$segmentIndex]
        $nextStates = @()
        $previousSegment = if ($segmentIndex -gt 0) { $RawSegments[$segmentIndex - 1] } else { '' }

        foreach ($state in $states) {
            if ($rawSegment -match '^li\[(\d+)\]$') {
                $nextStates += [pscustomobject]@{ Segments = ($state.Segments + [string]([int]$Matches[1] - 1)) }
                continue
            }

            if ($rawSegment -match '^li\[@Class="([^"]+)"\]\[(\d+)\]$') {
                $nextStates += [pscustomobject]@{ Segments = ($state.Segments + [string]([int]$Matches[2] - 1)) }
                continue
            }

            if ($rawSegment -eq 'li' -or $rawSegment -match '^li\[key="([^"]+)"\]$' -or $rawSegment -match '^li\[@Class="([^"]+)"\]$' -or $rawSegment -match '^li\[text\(\)="([^"]+)"\]$' -or $rawSegment -match '^li\[contains\(text\(\),"([^"]+)"\)\]$') {
                $fanoutCount = Get-FallbackFanoutCount -PreviousSegment $previousSegment
                for ($index = 0; $index -lt $fanoutCount; $index++) {
                    $nextStates += [pscustomobject]@{ Segments = ($state.Segments + [string]$index) }
                }
                continue
            }

            if ($rawSegment -match '^(?<name>[A-Za-z0-9_]+)$') {
                $nextStates += [pscustomobject]@{ Segments = ($state.Segments + $Matches.name) }
                continue
            }
        }

        $states = @($nextStates)
        if ($states.Count -eq 0) {
            return @()
        }
    }

    return @($states)
}

function Resolve-ConcreteTargets {
    param([pscustomobject]$Target)

    $targetRecords = @(Get-TargetRecords -Target $Target)
    if ($targetRecords.Count -gt 0) {
        $resolvedTargets = @()
        foreach ($record in $targetRecords) {
            $rawSegments = @($Target.Segments)
            if ($rawSegments.Count -eq 0) {
                $resolvedTargets += [pscustomobject]@{
                    DefType  = $record.DefType
                    DefName  = $Target.DefName
                    Segments = @()
                }
                continue
            }

            $defNode = Get-TargetDefNode -Target $Target -Record $record
            if ($null -eq $defNode) {
                continue
            }

            foreach ($resolvedPath in @(Resolve-XPathSegmentsAgainstNode -DefNode $defNode -RawSegments $rawSegments)) {
                $resolvedTargets += [pscustomobject]@{
                    DefType  = $record.DefType
                    DefName  = $Target.DefName
                    Segments = $resolvedPath.Segments
                }
            }
        }

        return @($resolvedTargets)
    }

    $fallbackDefTypes = @(Get-FallbackDefTypes -Target $Target)
    if ($fallbackDefTypes.Count -eq 0) {
        return @()
    }

    $fallbackSegmentStates = @(Resolve-FallbackSegments -RawSegments @($Target.Segments))
    if ($fallbackSegmentStates.Count -eq 0) {
        return @()
    }

    $fallbackTargets = @()
    foreach ($fallbackDefType in $fallbackDefTypes) {
        foreach ($segmentState in $fallbackSegmentStates) {
            $fallbackTargets += [pscustomobject]@{
                DefType  = $fallbackDefType
                DefName  = $Target.DefName
                Segments = $segmentState.Segments
            }
        }
    }

    return @($fallbackTargets)
}

$destinationModRoot = Join-Path $DestinationRoot $translationModName
if (-not (Test-Path -LiteralPath $destinationModRoot)) {
    throw "Destination mod root not found: $destinationModRoot"
}

$languageRoot = Join-Path $destinationModRoot (Join-Path 'Languages' $languageFolderName)
$keyedRoot = Join-Path $languageRoot 'Keyed'
$stringsNamesRoot = Join-Path $languageRoot (Join-Path 'Strings' 'Names')
$defInjectedRoot = Join-Path $languageRoot 'DefInjected'

if ($Clean) {
    foreach ($path in @($keyedRoot, (Split-Path -Parent $stringsNamesRoot), $defInjectedRoot, $reportDirectory)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

New-Directory -Path $keyedRoot
New-Directory -Path $stringsNamesRoot
New-Directory -Path $defInjectedRoot
New-Directory -Path $reportDirectory
Build-DefIndex -Roots $LookupRoots

$translationEntries = New-Object 'System.Collections.Generic.List[object]'
$copiedFiles = New-Object 'System.Collections.Generic.List[object]'
$unsupportedPatchTargets = New-Object 'System.Collections.Generic.List[object]'
$modSummaries = New-Object 'System.Collections.Generic.List[object]'

$modFolders = Get-ChildItem -LiteralPath $SourceRoot -Directory |
    Where-Object { $_.Name -like 'FIP-*' -and $_.Name -notlike 'FIP-Translation Part *' } |
    Sort-Object Name

foreach ($modFolder in $modFolders) {
    $aboutPath = Join-Path $modFolder.FullName 'About\About.xml'
    if (-not (Test-Path -LiteralPath $aboutPath)) {
        continue
    }

    [xml]$aboutXml = Get-Content -LiteralPath $aboutPath -Raw
    $packageId = $aboutXml.ModMetaData.packageId
    $englishLanguageRoot = Join-Path $modFolder.FullName 'Languages\English'

    Copy-LanguageFiles -SourcePath (Join-Path $englishLanguageRoot 'Keyed') -DestinationPath $keyedRoot -Pattern '*.xml' -CopiedFiles $copiedFiles -ModFolderName $modFolder.Name -Category 'Keyed'
    Copy-LanguageFiles -SourcePath (Join-Path $englishLanguageRoot 'Strings\Names') -DestinationPath $stringsNamesRoot -Pattern '*.txt' -CopiedFiles $copiedFiles -ModFolderName $modFolder.Name -Category 'Strings/Names'

    $startingCount = $translationEntries.Count

    foreach ($defsFile in Get-ChildItem -LiteralPath (Join-Path $modFolder.FullName 'Defs') -Filter '*.xml' -File -Recurse -ErrorAction SilentlyContinue) {
        [xml]$defsXml = Get-Content -LiteralPath $defsFile.FullName -Raw
        $defsRoot = $defsXml.SelectSingleNode('/Defs')
        if ($null -eq $defsRoot) {
            continue
        }

        foreach ($defNode in @(Get-ElementChildren -Node $defsRoot)) {
            $defType = $defNode.LocalName
            $defNameNode = Get-DirectChildElement -Node $defNode -LocalName 'defName'
            if ($null -eq $defNameNode -or [string]::IsNullOrWhiteSpace($defNameNode.InnerText)) {
                continue
            }

            Walk-DefTree -Node $defNode -Segments @() -Entries $translationEntries -ModFolderName $modFolder.Name -SourceKind 'Def' -DefType $defType -DefName $defNameNode.InnerText.Trim() -SourceFile $defsFile.FullName
        }
    }

    foreach ($patchFile in Get-ChildItem -LiteralPath (Join-Path $modFolder.FullName 'Patches') -Filter '*.xml' -File -Recurse -ErrorAction SilentlyContinue) {
        [xml]$patchXml = Get-Content -LiteralPath $patchFile.FullName -Raw
        foreach ($operationNode in $patchXml.SelectNodes('//*[xpath and value]')) {
            $xpathNode = Get-DirectChildElement -Node $operationNode -LocalName 'xpath'
            $valueNode = Get-DirectChildElement -Node $operationNode -LocalName 'value'
            if ($null -eq $xpathNode -or $null -eq $valueNode -or [string]::IsNullOrWhiteSpace($xpathNode.InnerText)) {
                continue
            }

            $xpathText = $xpathNode.InnerText.Trim()
            if ($xpathText -eq '/Defs' -or $xpathText -eq 'Defs') {
                foreach ($addedDefNode in @(Get-ValueElementChildren -Node $valueNode)) {
                    $defType = $addedDefNode.LocalName
                    $defNameNode = Get-DirectChildElement -Node $addedDefNode -LocalName 'defName'
                    if ($null -eq $defNameNode -or [string]::IsNullOrWhiteSpace($defNameNode.InnerText)) {
                        continue
                    }

                    Walk-DefTree -Node $addedDefNode -Segments @() -Entries $translationEntries -ModFolderName $modFolder.Name -SourceKind 'Patch' -DefType $defType -DefName $defNameNode.InnerText.Trim() -SourceFile $patchFile.FullName
                }
                continue
            }

            $target = Convert-XPathToTarget -XPath $xpathText
            if ($null -eq $target) {
                if (Test-ContainsTranslatableValue -Node $valueNode) {
                    $unsupportedPatchTargets.Add([pscustomobject]@{
                        ModFolderName = $modFolder.Name
                        SourceFile    = $patchFile.FullName
                        XPath         = $xpathText
                    })
                }
                continue
            }

            $valueElements = @(Get-ValueElementChildren -Node $valueNode)
            $concreteTargets = @(Resolve-ConcreteTargets -Target $target)
            if ($concreteTargets.Count -eq 0) {
                if (Test-ContainsTranslatableValue -Node $valueNode) {
                    $unsupportedPatchTargets.Add([pscustomobject]@{
                        ModFolderName = $modFolder.Name
                        SourceFile    = $patchFile.FullName
                        XPath         = $xpathText
                    })
                }
                continue
            }

            foreach ($concreteTarget in $concreteTargets) {
                if ($valueElements.Count -eq 1 -and $concreteTarget.Segments.Count -gt 0 -and $valueElements[0].LocalName -eq $concreteTarget.Segments[-1]) {
                    $prefixSegments = @()
                    if ($concreteTarget.Segments.Count -gt 1) {
                        $prefixSegments = $concreteTarget.Segments[0..($concreteTarget.Segments.Count - 2)]
                    }

                    Walk-PatchValue -Nodes @($valueElements[0]) -Segments $prefixSegments -Entries $translationEntries -ModFolderName $modFolder.Name -DefType $concreteTarget.DefType -DefName $concreteTarget.DefName -SourceFile $patchFile.FullName
                    continue
                }

                Walk-PatchValue -Nodes $valueElements -Segments $concreteTarget.Segments -Entries $translationEntries -ModFolderName $modFolder.Name -DefType $concreteTarget.DefType -DefName $concreteTarget.DefName -SourceFile $patchFile.FullName
            }
        }
    }

    $modSummaries.Add([pscustomobject]@{
        ModFolderName      = $modFolder.Name
        PackageId          = $packageId
        KeyedFilesCopied   = @($copiedFiles | Where-Object { $_.ModFolderName -eq $modFolder.Name -and $_.Category -eq 'Keyed' }).Count
        NameFilesCopied    = @($copiedFiles | Where-Object { $_.ModFolderName -eq $modFolder.Name -and $_.Category -eq 'Strings/Names' }).Count
        TranslationEntries = $translationEntries.Count - $startingCount
    })
}

$resolvedEntries = $translationEntries |
    Group-Object Key |
    ForEach-Object {
        $patchEntry = $_.Group | Where-Object { $_.SourceKind -eq 'Patch' } | Select-Object -Last 1
        if ($null -ne $patchEntry) {
            return $patchEntry
        }

        return $_.Group | Select-Object -First 1
    } |
    Sort-Object DefType, ModFolderName, DefName, Path

$sourceDuplicateEntryCountCollapsed = $translationEntries.Count - (Get-CollectionCount -Value ($translationEntries | Group-Object Key))

$entriesByDefType = $resolvedEntries | Group-Object DefType
foreach ($defTypeGroup in $entriesByDefType) {
    $defTypeDirectory = Join-Path $defInjectedRoot $defTypeGroup.Name
    New-Directory -Path $defTypeDirectory

    foreach ($modGroup in ($defTypeGroup.Group | Group-Object ModFolderName)) {
        $fileStem = ConvertTo-SafeFileStem -Value $modGroup.Name
        $outputFile = Join-Path $defTypeDirectory ($fileStem + '.xml')

        $xmlSettings = New-Object System.Xml.XmlWriterSettings
        $xmlSettings.Indent = $true
        $xmlSettings.IndentChars = '  '
        $xmlSettings.NewLineChars = "`r`n"
        $xmlSettings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
        $xmlSettings.Encoding = New-Object System.Text.UTF8Encoding($false)

        $fileStream = [System.IO.File]::Create($outputFile)
        $streamWriter = New-Object System.IO.StreamWriter($fileStream, [System.Text.UTF8Encoding]::new($false))
        $xmlWriter = [System.Xml.XmlWriter]::Create($streamWriter, $xmlSettings)

        $xmlWriter.WriteStartDocument()
        $xmlWriter.WriteStartElement('LanguageData')

        foreach ($entry in ($modGroup.Group | Sort-Object DefName, Path)) {
            $xmlWriter.WriteStartElement("$($entry.DefName).$($entry.Path)")
            $xmlWriter.WriteString($entry.Text)
            $xmlWriter.WriteEndElement()
        }

        $xmlWriter.WriteEndElement()
        $xmlWriter.WriteEndDocument()
        $xmlWriter.Flush()
        $xmlWriter.Dispose()
        $streamWriter.Dispose()
        $fileStream.Dispose()
    }
}

$keyedFileCount = Get-CollectionCount -Value @($copiedFiles | Where-Object { $_.Category -eq 'Keyed' })
$nameFileCount = Get-CollectionCount -Value @($copiedFiles | Where-Object { $_.Category -eq 'Strings/Names' })
$resolvedEntryCount = Get-CollectionCount -Value $resolvedEntries
$unsupportedPatchCount = Get-CollectionCount -Value $unsupportedPatchTargets

$report = [pscustomobject]@{
    GeneratedAt            = (Get-Date).ToString('s')
    TranslationModName     = $translationModName
    LanguageFolder         = $languageFolderName
    SourceRoot             = $SourceRoot
    DestinationRoot        = $destinationModRoot
    LookupRoots            = $LookupRoots
    ModCount               = $modSummaries.Count
    KeyedFilesCopied       = $keyedFileCount
    NameFilesCopied        = $nameFileCount
    DefInjectedEntryCount  = $resolvedEntryCount
    SourceDuplicateEntryCountCollapsed = $sourceDuplicateEntryCountCollapsed
    OutputDuplicateEntryCount          = 0
    UnsupportedPatchTarget = $unsupportedPatchCount
    Mods                   = $modSummaries
    UnsupportedPatches     = $unsupportedPatchTargets
}

$reportPath = Join-Path $reportDirectory 'generation-report.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8

Write-Host "Generated $languageFolderName locale scaffold for $($modSummaries.Count) mods."
Write-Host "Copied Keyed files: $keyedFileCount"
Write-Host "Copied Names files: $nameFileCount"
Write-Host "Resolved DefInjected entries: $resolvedEntryCount"
Write-Host "Source duplicate entries collapsed: $sourceDuplicateEntryCountCollapsed"
Write-Host "Output duplicate entries: 0"
Write-Host "Unsupported patch targets: $unsupportedPatchCount"