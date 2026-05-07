param(
    [string]$SourceRoot = 'D:\Steam\steamapps\workshop\content\294100',
    [string]$DestinationRoot,
    [string]$LanguageFolderName = 'German',
    [string[]]$LookupRoots,
    [switch]$Clean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$script:LastErrorPath = Join-Path $PSScriptRoot 'last-error.txt'
$script:ProgressPath = Join-Path $PSScriptRoot 'last-progress.txt'
if (Test-Path -LiteralPath $script:LastErrorPath) {
    Remove-Item -LiteralPath $script:LastErrorPath -Force
}
if (Test-Path -LiteralPath $script:ProgressPath) {
    Remove-Item -LiteralPath $script:ProgressPath -Force
}
trap {
    $_ | Out-String | Set-Content -LiteralPath $script:LastErrorPath -Encoding UTF8
    throw
}

$translationModName = 'FIP-Translation Part 3'
$languageFolderName = $LanguageFolderName
$reportDirectory = Join-Path $PSScriptRoot 'Reports'
$repositoryRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$script:XmlDocumentCache = @{}
$script:DefNameIndex = @{}
$script:NameAttributeIndex = @{}
$script:TranslatableNodeNames = @('label', 'description')

if (-not $PSBoundParameters.ContainsKey('DestinationRoot')) {
    $DestinationRoot = $repositoryRoot
}

if (-not (Test-Path -LiteralPath $SourceRoot)) {
    throw "Workshop source root not found: $SourceRoot"
}

Add-Content -LiteralPath $script:ProgressPath -Value 'start'

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

function Add-TranslationEntry {
    param(
        [System.Collections.Generic.List[object]]$Entries,
        [string]$ModName,
        [string]$OutputStem,
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
        ModName    = $ModName
        OutputStem = $OutputStem
        SourceKind = $SourceKind
        DefType    = $DefType
        DefName    = $DefName
        Path       = $entryPath
        Text       = $Text.Trim()
        SourceFile = $SourceFile
        Key        = "$DefType|$DefName|$entryPath"
    })
}

function Walk-DefTree {
    param(
        [System.Xml.XmlNode]$Node,
        [string[]]$Segments,
        [System.Collections.Generic.List[object]]$Entries,
        [string]$ModName,
        [string]$OutputStem,
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
            Walk-DefTree -Node $children[$index] -Segments ($Segments + [string]$index) -Entries $Entries -ModName $ModName -OutputStem $OutputStem -SourceKind $SourceKind -DefType $DefType -DefName $DefName -SourceFile $SourceFile
        }
        return
    }

    foreach ($child in $children) {
        $childSegments = $Segments + $child.LocalName
        if ($script:TranslatableNodeNames -contains $child.LocalName -and -not [string]::IsNullOrWhiteSpace($child.InnerText)) {
            Add-TranslationEntry -Entries $Entries -ModName $ModName -OutputStem $OutputStem -SourceKind $SourceKind -DefType $DefType -DefName $DefName -PathSegments $childSegments -Text $child.InnerText -SourceFile $SourceFile
        }

        Walk-DefTree -Node $child -Segments $childSegments -Entries $Entries -ModName $ModName -OutputStem $OutputStem -SourceKind $SourceKind -DefType $DefType -DefName $DefName -SourceFile $SourceFile
    }
}

function Test-ContainsTranslatableValue {
    param([System.Xml.XmlNode]$Node)

    foreach ($child in @(Get-ElementChildren -Node $Node)) {
        if ($script:TranslatableNodeNames -contains $child.LocalName -and -not [string]::IsNullOrWhiteSpace($child.InnerText)) {
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
        [string]$ModName,
        [string]$OutputStem,
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
            Walk-DefTree -Node $Nodes[$index] -Segments ($Segments + [string]$index) -Entries $Entries -ModName $ModName -OutputStem $OutputStem -SourceKind 'Patch' -DefType $DefType -DefName $DefName -SourceFile $SourceFile
        }
        return
    }

    foreach ($node in $Nodes) {
        if ($script:TranslatableNodeNames -contains $node.LocalName -and -not [string]::IsNullOrWhiteSpace($node.InnerText)) {
            Add-TranslationEntry -Entries $Entries -ModName $ModName -OutputStem $OutputStem -SourceKind 'Patch' -DefType $DefType -DefName $DefName -PathSegments ($Segments + $node.LocalName) -Text $node.InnerText -SourceFile $SourceFile
            continue
        }

        Walk-DefTree -Node $node -Segments $Segments -Entries $Entries -ModName $ModName -OutputStem $OutputStem -SourceKind 'Patch' -DefType $DefType -DefName $DefName -SourceFile $SourceFile
    }
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
                            DefType  = $defType
                            FilePath = $file.FullName
                            DefName  = if ($null -ne $defNameNode) { $defNameNode.InnerText.Trim() } else { $null }
                            NameAttr = if ($null -ne $nameAttribute) { $nameAttribute.Value.Trim() } else { $null }
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
            if (-not [string]::IsNullOrWhiteSpace($rawSegment)) {
                $segments += $rawSegment
            }
        }
    }

    return [pscustomobject]@{
        DefType  = $match.Groups['defType'].Value
        Selector = $match.Groups['selector'].Value
        DefName  = $match.Groups['defName'].Value
        Segments = $segments
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
            }
        }

        $states = @($nextStates)
        if ($states.Count -eq 0) {
            return @()
        }
    }

    return @($states | ForEach-Object { [pscustomobject]@{ Segments = $_.Segments } })
}

function Resolve-ConcreteTargets {
    param([pscustomobject]$Target)

    $targetRecords = @(Get-TargetRecords -Target $Target)
    if ($targetRecords.Count -eq 0) {
        return @()
    }

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

function Test-IsTargetMod {
    param(
        [string]$Name,
        [string]$PackageId
    )

    if ($PackageId -match '^FIP\.|^Rick\.FCP\.' -or $Name -match '^(FIP|FCP)') {
        return $false
    }

    if ($PackageId -match '^VanillaExpanded\.|^vanillaexpanded\.|^OskarPotocki\.VanillaFactionsExpanded\.Core$') {
        return $true
    }

    if ($PackageId -match '^Orion\.Hospitality$|^Orion\.Gastronomy$|^Orion\.CashRegister$|^Orion\.Therapy$|^Adamas\.(HospitalityCasino|HospitalitySpa|Storefront|VendingMachines)$') {
        return $true
    }

    return $false
}

function Get-LoadRoots {
    param([string]$ModRoot)

    $roots = New-Object 'System.Collections.Generic.List[string]'
    $loadFoldersFile = Get-ChildItem -LiteralPath $ModRoot -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ieq 'LoadFolders.xml' } |
        Sort-Object FullName |
        Select-Object -First 1

    if ($null -ne $loadFoldersFile) {
        try {
            [xml]$loadFoldersXml = Get-Content -LiteralPath $loadFoldersFile.FullName -Raw
            $versionNode = $loadFoldersXml.SelectSingleNode('/loadFolders/*[local-name()="v1.6"]')
            if ($null -ne $versionNode) {
                foreach ($child in @(Get-ElementChildren -Node $versionNode)) {
                    $relativePath = $child.InnerText.Trim()
                    if ([string]::IsNullOrWhiteSpace($relativePath)) {
                        continue
                    }

                    $candidate = if ($relativePath -eq '/') { $ModRoot } else { Join-Path $ModRoot $relativePath }
                    if (Test-Path -LiteralPath $candidate) {
                        $roots.Add((Resolve-Path -LiteralPath $candidate).Path)
                    }
                }
            }
        }
        catch {
        }
    }

    if ($roots.Count -eq 0) {
        $roots.Add((Resolve-Path -LiteralPath $ModRoot).Path)
        $versionRoot = Join-Path $ModRoot '1.6'
        if (Test-Path -LiteralPath $versionRoot) {
            $roots.Add((Resolve-Path -LiteralPath $versionRoot).Path)
        }
        $commonRoot = Join-Path $ModRoot 'Common'
        if (Test-Path -LiteralPath $commonRoot) {
            $roots.Add((Resolve-Path -LiteralPath $commonRoot).Path)
        }
    }

    return @(Get-MinimalRootSet -Roots $roots)
}

function Copy-KeyedFile {
    param(
        [string]$SourceFile,
        [string]$DestinationRoot,
        [string]$OutputStem,
        [System.Collections.Generic.HashSet[string]]$SeenSources,
        [System.Collections.Generic.List[object]]$CopiedFiles,
        [string]$ModName,
        [string]$PackageId
    )

    if (-not $SeenSources.Add($SourceFile)) {
        return
    }

    $relativeStem = ConvertTo-SafeFileStem -Value ([System.IO.Path]::GetFileNameWithoutExtension($SourceFile))
    $destinationFile = Join-Path $DestinationRoot ($OutputStem + '_' + $relativeStem + '.xml')
    Copy-Item -LiteralPath $SourceFile -Destination $destinationFile -Force
    $CopiedFiles.Add([pscustomobject]@{
        ModName      = $ModName
        PackageId    = $PackageId
        Source       = $SourceFile
        Destination  = $destinationFile
    })
}

$targetMods = New-Object 'System.Collections.Generic.List[object]'
foreach ($modFolder in Get-ChildItem -LiteralPath $SourceRoot -Directory | Sort-Object Name) {
    $aboutPath = Join-Path $modFolder.FullName 'About\About.xml'
    if (-not (Test-Path -LiteralPath $aboutPath)) {
        continue
    }

    try {
        [xml]$aboutXml = Get-Content -LiteralPath $aboutPath -Raw
    }
    catch {
        continue
    }

    $name = [string]$aboutXml.ModMetaData.name
    $packageId = [string]$aboutXml.ModMetaData.packageId
    if (-not (Test-IsTargetMod -Name $name -PackageId $packageId)) {
        continue
    }

    $outputStem = ConvertTo-SafeFileStem -Value $(if ([string]::IsNullOrWhiteSpace($packageId)) { $name } else { $packageId })
    $targetMods.Add([pscustomobject]@{
        FolderPath  = $modFolder.FullName
        FolderName  = $modFolder.Name
        Name        = $name
        PackageId   = $packageId
        OutputStem  = $outputStem
        LoadRoots   = @(Get-LoadRoots -ModRoot $modFolder.FullName)
    })
}

Add-Content -LiteralPath $script:ProgressPath -Value ("targetMods=" + $targetMods.Count)

if (-not $PSBoundParameters.ContainsKey('LookupRoots')) {
    $LookupRoots = @()
    foreach ($targetMod in $targetMods) {
        $LookupRoots += $targetMod.LoadRoots
    }

    $rimworldDataRoot = 'D:\Steam\steamapps\common\RimWorld\Data'
    if (Test-Path -LiteralPath $rimworldDataRoot) {
        $LookupRoots += $rimworldDataRoot
    }
}

$LookupRoots = @(Get-MinimalRootSet -Roots $LookupRoots)

$destinationModRoot = Join-Path $DestinationRoot $translationModName
if (-not (Test-Path -LiteralPath $destinationModRoot)) {
    throw "Destination mod root not found: $destinationModRoot"
}

$languageRoot = Join-Path $destinationModRoot (Join-Path 'Languages' $languageFolderName)
$keyedRoot = Join-Path $languageRoot 'Keyed'
$defInjectedRoot = Join-Path $languageRoot 'DefInjected'

if ($Clean) {
    foreach ($path in @($keyedRoot, $defInjectedRoot, $reportDirectory)) {
        if (Test-Path -LiteralPath $path) {
            Remove-Item -LiteralPath $path -Recurse -Force
        }
    }
}

New-Directory -Path $keyedRoot
New-Directory -Path $defInjectedRoot
New-Directory -Path $reportDirectory
Build-DefIndex -Roots $LookupRoots
Add-Content -LiteralPath $script:ProgressPath -Value 'defIndexBuilt'

$translationEntries = New-Object 'System.Collections.Generic.List[object]'
$copiedKeyedFiles = New-Object 'System.Collections.Generic.List[object]'
$unsupportedPatchTargets = New-Object 'System.Collections.Generic.List[object]'
$modSummaries = New-Object 'System.Collections.Generic.List[object]'

foreach ($targetMod in $targetMods) {
    Add-Content -LiteralPath $script:ProgressPath -Value ("scanning=" + $targetMod.PackageId)
    $seenDefs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $seenPatches = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $seenKeyed = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $startingEntryCount = $translationEntries.Count
    $startingKeyedCount = $copiedKeyedFiles.Count

    foreach ($loadRoot in $targetMod.LoadRoots) {
        foreach ($keyedDirectory in Get-ChildItem -LiteralPath $loadRoot -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\Languages\\English\\Keyed$' }) {
            foreach ($keyedFile in Get-ChildItem -LiteralPath $keyedDirectory.FullName -Filter '*.xml' -File -ErrorAction SilentlyContinue) {
                Copy-KeyedFile -SourceFile $keyedFile.FullName -DestinationRoot $keyedRoot -OutputStem $targetMod.OutputStem -SeenSources $seenKeyed -CopiedFiles $copiedKeyedFiles -ModName $targetMod.Name -PackageId $targetMod.PackageId
            }
        }

        foreach ($defsDirectory in Get-ChildItem -LiteralPath $loadRoot -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'Defs' }) {
            foreach ($defsFile in Get-ChildItem -LiteralPath $defsDirectory.FullName -Filter '*.xml' -File -Recurse -ErrorAction SilentlyContinue) {
                if (-not $seenDefs.Add($defsFile.FullName)) {
                    continue
                }

                try {
                    [xml]$defsXml = Get-Content -LiteralPath $defsFile.FullName -Raw
                }
                catch {
                    continue
                }

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

                    Walk-DefTree -Node $defNode -Segments @() -Entries $translationEntries -ModName $targetMod.Name -OutputStem $targetMod.OutputStem -SourceKind 'Def' -DefType $defType -DefName $defNameNode.InnerText.Trim() -SourceFile $defsFile.FullName
                }
            }
        }

        foreach ($patchDirectory in Get-ChildItem -LiteralPath $loadRoot -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'Patches' }) {
            foreach ($patchFile in Get-ChildItem -LiteralPath $patchDirectory.FullName -Filter '*.xml' -File -Recurse -ErrorAction SilentlyContinue) {
                if (-not $seenPatches.Add($patchFile.FullName)) {
                    continue
                }

                try {
                    [xml]$patchXml = Get-Content -LiteralPath $patchFile.FullName -Raw
                }
                catch {
                    continue
                }

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

                            Walk-DefTree -Node $addedDefNode -Segments @() -Entries $translationEntries -ModName $targetMod.Name -OutputStem $targetMod.OutputStem -SourceKind 'Patch' -DefType $defType -DefName $defNameNode.InnerText.Trim() -SourceFile $patchFile.FullName
                        }
                        continue
                    }

                    $target = Convert-XPathToTarget -XPath $xpathText
                    if ($null -eq $target) {
                        if (Test-ContainsTranslatableValue -Node $valueNode) {
                            $unsupportedPatchTargets.Add([pscustomobject]@{
                                ModName    = $targetMod.Name
                                PackageId  = $targetMod.PackageId
                                SourceFile = $patchFile.FullName
                                XPath      = $xpathText
                            })
                        }
                        continue
                    }

                    $valueElements = @(Get-ValueElementChildren -Node $valueNode)
                    $concreteTargets = @(Resolve-ConcreteTargets -Target $target)
                    if ($concreteTargets.Count -eq 0) {
                        if (Test-ContainsTranslatableValue -Node $valueNode) {
                            $unsupportedPatchTargets.Add([pscustomobject]@{
                                ModName    = $targetMod.Name
                                PackageId  = $targetMod.PackageId
                                SourceFile = $patchFile.FullName
                                XPath      = $xpathText
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

                            Walk-PatchValue -Nodes @($valueElements[0]) -Segments $prefixSegments -Entries $translationEntries -ModName $targetMod.Name -OutputStem $targetMod.OutputStem -DefType $concreteTarget.DefType -DefName $concreteTarget.DefName -SourceFile $patchFile.FullName
                            continue
                        }

                        Walk-PatchValue -Nodes $valueElements -Segments $concreteTarget.Segments -Entries $translationEntries -ModName $targetMod.Name -OutputStem $targetMod.OutputStem -DefType $concreteTarget.DefType -DefName $concreteTarget.DefName -SourceFile $patchFile.FullName
                    }
                }
            }
        }
    }

    $modSummaries.Add([pscustomobject]@{
        ModName            = $targetMod.Name
        PackageId          = $targetMod.PackageId
        KeyedFilesCopied   = $copiedKeyedFiles.Count - $startingKeyedCount
        TranslationEntries = $translationEntries.Count - $startingEntryCount
        LoadRoots          = $targetMod.LoadRoots
    })
}

Add-Content -LiteralPath $script:ProgressPath -Value 'scanComplete'

$resolvedEntries = $translationEntries |
    Group-Object Key |
    ForEach-Object {
        $patchEntry = $_.Group | Where-Object { $_.SourceKind -eq 'Patch' } | Select-Object -Last 1
        if ($null -ne $patchEntry) {
            return $patchEntry
        }

        return $_.Group | Select-Object -First 1
    } |
    Sort-Object DefType, OutputStem, DefName, Path

foreach ($defTypeGroup in ($resolvedEntries | Group-Object DefType)) {
    $defTypeDirectory = Join-Path $defInjectedRoot $defTypeGroup.Name
    New-Directory -Path $defTypeDirectory

    foreach ($modGroup in ($defTypeGroup.Group | Group-Object OutputStem)) {
        $outputFile = Join-Path $defTypeDirectory ($modGroup.Name + '.xml')

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

Add-Content -LiteralPath $script:ProgressPath -Value 'filesWritten'

$report = [pscustomobject]@{
    GeneratedAt           = (Get-Date).ToString('s')
    TranslationModName    = $translationModName
    LanguageFolder        = $languageFolderName
    SourceRoot            = $SourceRoot
    DestinationRoot       = $destinationModRoot
    TargetModCount        = $targetMods.Count
    LookupRoots           = $LookupRoots
    KeyedFilesCopied      = $copiedKeyedFiles.Count
    DefInjectedEntryCount = @($resolvedEntries).Count
    UnsupportedPatchCount = $unsupportedPatchTargets.Count
    Mods                  = $modSummaries
    UnsupportedPatches    = $unsupportedPatchTargets
}

$reportPath = Join-Path $reportDirectory 'generation-report.json'
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Add-Content -LiteralPath $script:ProgressPath -Value 'reportWritten'

Write-Host "Generated $languageFolderName placeholder scaffold for $($targetMods.Count) workshop mods."
Write-Host "Copied Keyed files: $($copiedKeyedFiles.Count)"
Write-Host "Resolved DefInjected entries: $(@($resolvedEntries).Count)"
Write-Host "Unsupported patch targets: $($unsupportedPatchTargets.Count)"