[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$RepoRoot = "C:\Users\Matthias\Desktop\Fallout Immersion Project",
    [string]$Part1EnglishRoot = "C:\Users\Matthias\Desktop\Fallout Immersion Project\FIP-Translation Part 1\Languages\English",
    [string[]]$IncludeMods
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

$AmbiguousDefInjectedResolutions = @{
    'FIP-Arktos|PawnKindDef|Arktos_Ants.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\PawnKindDef\Arktos_Ants.xml'
    )
    'FIP-Arktos|ThingDef|Arktos_Ants.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\ThingDef\Arktos_Ants.xml'
    )
    'FIP-Greenway|ThingDef|Greenway_Drugs.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\ThingDef\Greenway_Drugs.xml'
    )
    'FIP-H&HTools|FactionDef|HHTools_Insect.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\FactionDef\HHTools_Insect.xml'
    )
    'FIP-H&HTools|FactionDef|HHTools_Pilgrims.xml' = @(
        'LoadFolders\Greenway\Languages\English\DefInjected\FactionDef\HHTools_Pilgrims.xml'
    )
    'FIP-H&HTools|FactionDef|HHTools_Salvagers.xml' = @(
        'LoadFolders\Odyssey\Languages\English\DefInjected\FactionDef\HHTools_Salvagers.xml'
    )
    'FIP-H&HTools|FactionDef|HHTools_Sanguophages.xml' = @(
        'LoadFolders\Royalty\Languages\English\DefInjected\FactionDef\HHTools_Sanguophages.xml'
    )
    'FIP-H&HTools|FactionDef|HHTools_TradersGuild.xml' = @(
        'LoadFolders\Odyssey\Languages\English\DefInjected\FactionDef\HHTools_TradersGuild.xml'
    )
    'FIP-Lucky 38|RecipeDef|Lucky38_Cooking.xml' = @(
        'LoadFolders\Plants_VBrewE_VCookE\Languages\English\DefInjected\RecipeDef\Lucky38_Cooking.xml'
    )
    'FIP-Repconn|LandingOutcomeDef|Repconn_Gravship.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\LandingOutcomeDef\Repconn_Gravship.xml',
        'LoadFolders\Gravship\Languages\English\DefInjected\LandingOutcomeDef\Repconn_Gravship.xml'
    )
    'FIP-Repconn|ResearchProjectDef|Repconn_Gravship.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\ResearchProjectDef\Repconn_Gravship.xml',
        'LoadFolders\Gravship\Languages\English\DefInjected\ResearchProjectDef\Repconn_Gravship.xml'
    )
    'FIP-Repconn|ThingDef|Repconn_Gravship.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\ThingDef\Repconn_Gravship.xml',
        'LoadFolders\Gravship\Languages\English\DefInjected\ThingDef\Repconn_Gravship.xml'
    )
    'FIP-Repconn|VanillaGravshipExpanded.LaunchBoonDef|Repconn_Gravship.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\VanillaGravshipExpanded.LaunchBoonDef\Repconn_Gravship.xml',
        'LoadFolders\Gravship\Languages\English\DefInjected\VanillaGravshipExpanded.LaunchBoonDef\Repconn_Gravship.xml'
    )
    'FIP-Repconn|VSE.Expertise.ExpertiseDef|Repconn_Gravship.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\VSE.Expertise.ExpertiseDef\Repconn_Gravship.xml',
        'LoadFolders\Gravship_Skills\Languages\English\DefInjected\VSE.Expertise.ExpertiseDef\Repconn_Gravship.xml'
    )
    'FIP-WestTek|GeneDef|WestTek_GeneDefs_Health.xml' = @(
        'LoadFolders\Base\Languages\English\DefInjected\GeneDef\WestTek_GeneDefs_Health.xml'
    )
}

function Normalize-ListArgument {
    param([string[]]$Values)

    $normalized = New-Object 'System.Collections.Generic.List[string]'
    foreach ($value in @($Values)) {
        if ([string]::IsNullOrWhiteSpace($value)) {
            continue
        }

        foreach ($part in @([string]$value -split ',')) {
            $trimmed = $part.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $normalized.Add($trimmed) | Out-Null
            }
        }
    }

    return $normalized.ToArray()
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        $null = New-Item -ItemType Directory -Path $Path -Force
    }
}

function Increment-SummaryCounter {
    param(
        [System.Collections.IDictionary]$Summary,
        [string]$Name
    )

    $Summary[$Name] = [int]$Summary[$Name] + 1
}

function Get-LanguageSummaryKey {
    param([string]$Result)

    switch ($Result) {
        'create' { return 'LanguageFilesCreated' }
        'update' { return 'LanguageFilesUpdated' }
        'unchanged' { return 'LanguageFilesUnchanged' }
        default { throw "Unknown copy result: $Result" }
    }
}

function Get-AmbiguousDefInjectedTargets {
    param(
        [string]$ModName,
        [string]$ModRoot,
        [string]$DefType,
        [string]$FileName
    )

    $key = $ModName + '|' + $DefType + '|' + $FileName
    if (-not $AmbiguousDefInjectedResolutions.ContainsKey($key)) {
        return @()
    }

    return @($AmbiguousDefInjectedResolutions[$key] | ForEach-Object {
        Join-Path $ModRoot $_
    })
}

function Add-IndexedPath {
    param(
        [hashtable]$Index,
        [string]$Key,
        [string]$Path
    )

    if (-not $Index.ContainsKey($Key)) {
        $Index[$Key] = New-Object 'System.Collections.Generic.List[string]'
    }

    $Index[$Key].Add($Path) | Out-Null
}

function Get-UniquePathMatch {
    param(
        [hashtable]$Index,
        [string]$Key
    )

    if (-not $Index.ContainsKey($Key)) {
        return [pscustomobject]@{
            Status = 'missing'
            Path   = $null
            Paths  = @()
        }
    }

    $paths = @($Index[$Key])
    if ($paths.Count -eq 1) {
        return [pscustomobject]@{
            Status = 'unique'
            Path   = $paths[0]
            Paths  = $paths
        }
    }

    return [pscustomobject]@{
        Status = 'ambiguous'
        Path   = $null
        Paths  = $paths
    }
}

function Get-ModIndex {
    param([string]$ModRoot)

    $index = [ordered]@{
        Keyed       = @{}
        Names       = @{}
        DefInjected = @{}
        Defs        = @{}
    }

    foreach ($file in Get-ChildItem -LiteralPath $ModRoot -Recurse -File -ErrorAction SilentlyContinue) {
        $fullName = $file.FullName

        if ($file.Extension -ieq '.xml' -and $fullName -match '\\Defs\\') {
            Add-IndexedPath -Index $index.Defs -Key $file.Name -Path $fullName
        }

        if ($file.Extension -ieq '.xml' -and $fullName -match '\\Languages\\English\\Keyed\\') {
            Add-IndexedPath -Index $index.Keyed -Key $file.Name -Path $fullName
            continue
        }

        if ($file.Extension -ieq '.txt' -and $fullName -match '\\Languages\\English\\Strings\\Names\\') {
            Add-IndexedPath -Index $index.Names -Key $file.Name -Path $fullName
            continue
        }

        if ($file.Extension -ieq '.xml' -and $fullName -match '\\Languages\\English\\DefInjected\\([^\\]+)\\') {
            $defType = $Matches[1]
            Add-IndexedPath -Index $index.DefInjected -Key ($defType + '|' + $file.Name) -Path $fullName
        }
    }

    return [pscustomobject]$index
}

function Get-DefsLoadRoot {
    param([string]$DefsFilePath)

    $current = Split-Path -Parent $DefsFilePath
    while (-not [string]::IsNullOrWhiteSpace($current)) {
        if ((Split-Path -Leaf $current) -eq 'Defs') {
            return (Split-Path -Parent $current)
        }

        $parent = Split-Path -Parent $current
        if ($parent -eq $current) {
            break
        }
        $current = $parent
    }

    return $null
}

function Get-DerivedDefInjectedPath {
    param(
        [string]$DefsFilePath,
        [string]$DefType,
        [string]$FileName
    )

    $loadRoot = Get-DefsLoadRoot -DefsFilePath $DefsFilePath
    if ([string]::IsNullOrWhiteSpace($loadRoot)) {
        return $null
    }

    return Join-Path $loadRoot (Join-Path (Join-Path 'Languages\English\DefInjected' $DefType) $FileName)
}

function Copy-IfChanged {
    param(
        [string]$SourcePath,
        [string]$DestinationPath,
        [string]$ActionLabel
    )

    $sourceText = [System.IO.File]::ReadAllText($SourcePath)
    if (Test-Path -LiteralPath $DestinationPath) {
        $destinationText = [System.IO.File]::ReadAllText($DestinationPath)
        if ($destinationText -ceq $sourceText) {
            return 'unchanged'
        }
        $operation = 'Update'
    }
    else {
        $operation = 'Create'
    }

    Ensure-Directory -Path (Split-Path -Parent $DestinationPath)
    if ($PSCmdlet.ShouldProcess($DestinationPath, "$operation $ActionLabel")) {
        [System.IO.File]::Copy($SourcePath, $DestinationPath, $true)
    }

    return $operation.ToLowerInvariant()
}

function Test-ReadableXmlFile {
    param([string]$FilePath)

    if (-not (Test-Path -LiteralPath $FilePath)) {
        return [pscustomobject]@{
            IsValid = $false
            Reason  = 'file does not exist'
        }
    }

    $fileInfo = Get-Item -LiteralPath $FilePath
    if ($fileInfo.Length -eq 0) {
        return [pscustomobject]@{
            IsValid = $false
            Reason  = 'file is empty'
        }
    }

    $document = [System.Xml.XmlDocument]::new()
    try {
        $document.Load($FilePath)
    }
    catch {
        return [pscustomobject]@{
            IsValid = $false
            Reason  = $_.Exception.Message
        }
    }

    return [pscustomobject]@{
        IsValid = $true
        Reason  = $null
    }
}

function Read-PartLanguageEntries {
    param([string]$FilePath)

    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    try {
        $document.Load($FilePath)
    }
    catch {
        return $null
    }
    if ($null -eq $document.DocumentElement -or $document.DocumentElement.Name -ne 'LanguageData') {
        return $null
    }

    $entries = New-Object 'System.Collections.Generic.List[object]'
    foreach ($node in $document.DocumentElement.ChildNodes) {
        if ($node.NodeType -ne [System.Xml.XmlNodeType]::Element) {
            continue
        }

        $entries.Add([pscustomobject]@{
            Key  = $node.LocalName
            Text = $node.InnerText
        }) | Out-Null
    }

    return $entries.ToArray()
}

function Read-DefsXmlDocument {
    param([string]$FilePath)

    $raw = [System.IO.File]::ReadAllText($FilePath, [System.Text.Encoding]::UTF8)
    $raw = [regex]::Replace($raw, '&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;|\w+;)', '&amp;')

    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    try {
        $document.LoadXml($raw)
    }
    catch {
        return $null
    }

    if ($null -eq $document.DocumentElement -or $document.DocumentElement.Name -ne 'Defs') {
        return $null
    }

    return $document
}

function Get-DefNodeIndex {
    param([System.Xml.XmlDocument]$Document)

    $index = @{}
    foreach ($defNode in $Document.DocumentElement.ChildNodes) {
        if ($defNode -isnot [System.Xml.XmlElement]) {
            continue
        }

        $defNameNode = $defNode.SelectSingleNode('./defName')
        if ($null -eq $defNameNode -or [string]::IsNullOrWhiteSpace($defNameNode.InnerText)) {
            continue
        }

        $index[$defNode.LocalName + '|' + $defNameNode.InnerText.Trim()] = $defNode
    }

    return $index
}

function Resolve-DefPathNode {
    param(
        [System.Xml.XmlElement]$DefNode,
        [string[]]$Segments
    )

    $current = $DefNode
    foreach ($segment in $Segments) {
        if ([string]::IsNullOrWhiteSpace($segment)) {
            return $null
        }

        if ($segment -match '^\d+$') {
            $items = @($current.ChildNodes | Where-Object {
                $_ -is [System.Xml.XmlElement] -and $_.LocalName -eq 'li'
            })
            $index = [int]$segment
            if ($index -ge $items.Count) {
                return $null
            }
            $current = $items[$index]
            continue
        }

        $next = $null
        foreach ($child in $current.ChildNodes) {
            if ($child -is [System.Xml.XmlElement] -and $child.LocalName -ceq $segment) {
                $next = $child
                break
            }
        }

        if ($null -eq $next) {
            return $null
        }

        $current = $next
    }

    return $current
}

function Save-XmlDocument {
    param(
        [System.Xml.XmlDocument]$Document,
        [string]$FilePath
    )

    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = $Utf8NoBom
    $settings.Indent = $false
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::None

    $writer = [System.Xml.XmlWriter]::Create($FilePath, $settings)
    try {
        $Document.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

function Update-DefsFromPartFile {
    param(
        [string]$PartFilePath,
        [string]$DefType,
        [string]$DefsFilePath
    )

    $entries = Read-PartLanguageEntries -FilePath $PartFilePath
    if ($null -eq $entries) {
        return [pscustomobject]@{
            ChangedEntries = 0
            MissingEntries = 0
            FileChanged    = $false
            ParseError     = $true
        }
    }

    $entries = @($entries)
    if ($entries.Count -eq 0) {
        return [pscustomobject]@{
            ChangedEntries = 0
            MissingEntries = 0
            FileChanged    = $false
            ParseError     = $false
        }
    }

    $document = Read-DefsXmlDocument -FilePath $DefsFilePath
    if ($null -eq $document) {
        return [pscustomobject]@{
            ChangedEntries = 0
            MissingEntries = $entries.Count
            FileChanged    = $false
            ParseError     = $true
        }
    }

    $defIndex = Get-DefNodeIndex -Document $document
    $changedEntries = 0
    $missingEntries = 0
    $fileChanged = $false

    foreach ($entry in $entries) {
        $segments = @($entry.Key -split '\.')
        if ($segments.Count -lt 2) {
            $missingEntries++
            continue
        }

        $defName = $segments[0]
        $defIndexKey = $DefType + '|' + $defName
        if (-not $defIndex.ContainsKey($defIndexKey)) {
            $missingEntries++
            continue
        }

        $targetNode = Resolve-DefPathNode -DefNode $defIndex[$defIndexKey] -Segments $segments[1..($segments.Count - 1)]
        if ($null -eq $targetNode) {
            $missingEntries++
            continue
        }

        $childElements = @($targetNode.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })
        if ($childElements.Count -gt 0) {
            $missingEntries++
            continue
        }

        if ($targetNode.InnerText -cne $entry.Text) {
            $targetNode.InnerText = $entry.Text
            $changedEntries++
            $fileChanged = $true
        }
    }

    if ($fileChanged -and $PSCmdlet.ShouldProcess($DefsFilePath, 'Update defs text from Part 1 English')) {
        Save-XmlDocument -Document $document -FilePath $DefsFilePath
    }

    return [pscustomobject]@{
        ChangedEntries = $changedEntries
        MissingEntries = $missingEntries
        FileChanged    = $fileChanged
        ParseError     = $false
    }
}

$IncludeMods = @(Normalize-ListArgument -Values $IncludeMods)

if (-not (Test-Path -LiteralPath $Part1EnglishRoot)) {
    throw "Part 1 English root not found: $Part1EnglishRoot"
}

$modListPath = Join-Path $RepoRoot 'Tools\TranslationSync\mod-lists\fip.txt'
if (-not (Test-Path -LiteralPath $modListPath)) {
    throw "FIP mod list not found: $modListPath"
}

$mods = @([System.IO.File]::ReadAllLines($modListPath, [System.Text.Encoding]::UTF8) | ForEach-Object {
    $_.Trim()
} | Where-Object {
    -not [string]::IsNullOrWhiteSpace($_)
})

if ($IncludeMods.Count -gt 0) {
    $requested = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($modName in $IncludeMods) {
        [void]$requested.Add($modName)
    }

    $mods = @($mods | Where-Object { $requested.Contains($_) })
}

$summary = [ordered]@{
    ModsProcessed              = 0
    LanguageFilesCreated       = 0
    LanguageFilesUpdated       = 0
    LanguageFilesUnchanged     = 0
    LanguageTargetMissing      = 0
    LanguageTargetAmbiguous    = 0
    SourceXmlSkipped           = 0
    DefFilesUpdated            = 0
    DefEntriesUpdated          = 0
    DefEntriesMissingPath      = 0
    DefSourceMissing           = 0
    DefSourceAmbiguous         = 0
    DefSourceParseError        = 0
}

$issues = New-Object 'System.Collections.Generic.List[string]'

foreach ($modName in $mods) {
    $modRoot = Join-Path $RepoRoot $modName
    if (-not (Test-Path -LiteralPath $modRoot)) {
        $issues.Add("[$modName] missing mod root") | Out-Null
        continue
    }

    $summary.ModsProcessed++
    $index = Get-ModIndex -ModRoot $modRoot

    $keyedRoot = Join-Path $Part1EnglishRoot 'Keyed'
    foreach ($partFile in @(Get-ChildItem -LiteralPath $keyedRoot -File -Filter ($modName + '__*.xml') -ErrorAction SilentlyContinue)) {
        $xmlCheck = Test-ReadableXmlFile -FilePath $partFile.FullName
        if (-not $xmlCheck.IsValid) {
            $summary.SourceXmlSkipped++
            $issues.Add("[$modName] skipped invalid Part 1 Keyed file $($partFile.FullName) ($($xmlCheck.Reason))") | Out-Null
            continue
        }

        $sourceFileName = $partFile.Name.Substring(($modName + '__').Length)
        $match = Get-UniquePathMatch -Index $index.Keyed -Key $sourceFileName
        switch ($match.Status) {
            'unique' {
                $result = Copy-IfChanged -SourcePath $partFile.FullName -DestinationPath $match.Path -ActionLabel 'Keyed language file'
                Increment-SummaryCounter -Summary $summary -Name (Get-LanguageSummaryKey -Result $result)
            }
            'missing' {
                $summary.LanguageTargetMissing++
                $issues.Add("[$modName] missing Keyed target for $sourceFileName") | Out-Null
            }
            'ambiguous' {
                $summary.LanguageTargetAmbiguous++
                $issues.Add("[$modName] ambiguous Keyed target for $sourceFileName => $($match.Paths -join ' | ')") | Out-Null
            }
        }
    }

    $namesRoot = Join-Path $Part1EnglishRoot 'Strings\Names'
    foreach ($partFile in @(Get-ChildItem -LiteralPath $namesRoot -File -Filter ($modName + '__*.txt') -ErrorAction SilentlyContinue)) {
        $sourceFileName = $partFile.Name.Substring(($modName + '__').Length)
        $match = Get-UniquePathMatch -Index $index.Names -Key $sourceFileName
        switch ($match.Status) {
            'unique' {
                $result = Copy-IfChanged -SourcePath $partFile.FullName -DestinationPath $match.Path -ActionLabel 'Names file'
                Increment-SummaryCounter -Summary $summary -Name (Get-LanguageSummaryKey -Result $result)
            }
            'missing' {
                $summary.LanguageTargetMissing++
                $issues.Add("[$modName] missing Names target for $sourceFileName") | Out-Null
            }
            'ambiguous' {
                $summary.LanguageTargetAmbiguous++
                $issues.Add("[$modName] ambiguous Names target for $sourceFileName => $($match.Paths -join ' | ')") | Out-Null
            }
        }
    }

    $defInjectedRoot = Join-Path $Part1EnglishRoot 'DefInjected'
    foreach ($partFile in @(Get-ChildItem -LiteralPath $defInjectedRoot -Recurse -File -Filter ($modName + '__*.xml') -ErrorAction SilentlyContinue)) {
        $xmlCheck = Test-ReadableXmlFile -FilePath $partFile.FullName
        if (-not $xmlCheck.IsValid) {
            $summary.SourceXmlSkipped++
            $issues.Add("[$modName] skipped invalid Part 1 DefInjected file $($partFile.FullName) ($($xmlCheck.Reason))") | Out-Null
            continue
        }

        $defType = $partFile.Directory.Name
        $sourceFileName = $partFile.Name.Substring(($modName + '__').Length)
        $languageMatch = Get-UniquePathMatch -Index $index.DefInjected -Key ($defType + '|' + $sourceFileName)
        $defsMatch = Get-UniquePathMatch -Index $index.Defs -Key $sourceFileName

        $languageDestination = $null
        $languageDestinations = New-Object 'System.Collections.Generic.List[string]'
        switch ($languageMatch.Status) {
            'unique' {
                $languageDestinations.Add($languageMatch.Path) | Out-Null
            }
            'missing' {
                if ($defsMatch.Status -eq 'unique') {
                    $languageDestinations.Add((Get-DerivedDefInjectedPath -DefsFilePath $defsMatch.Path -DefType $defType -FileName $sourceFileName)) | Out-Null
                }
                else {
                    $summary.LanguageTargetMissing++
                    $issues.Add("[$modName] missing DefInjected target for $defType\\$sourceFileName") | Out-Null
                }
            }
            'ambiguous' {
                $resolvedTargets = @(Get-AmbiguousDefInjectedTargets -ModName $modName -ModRoot $modRoot -DefType $defType -FileName $sourceFileName)
                if ($resolvedTargets.Count -gt 0) {
                    $allMatch = $true
                    foreach ($resolvedTarget in $resolvedTargets) {
                        if ($languageMatch.Paths -notcontains $resolvedTarget) {
                            $allMatch = $false
                            break
                        }
                    }

                    if ($allMatch) {
                        foreach ($resolvedTarget in $resolvedTargets) {
                            $languageDestinations.Add($resolvedTarget) | Out-Null
                        }
                        break
                    }
                }

                if ($defsMatch.Status -eq 'unique') {
                    $derivedPath = Get-DerivedDefInjectedPath -DefsFilePath $defsMatch.Path -DefType $defType -FileName $sourceFileName
                    if ($languageMatch.Paths -contains $derivedPath) {
                        $languageDestinations.Add($derivedPath) | Out-Null
                    }
                    else {
                        $summary.LanguageTargetAmbiguous++
                        $issues.Add("[$modName] ambiguous DefInjected target for $defType\\$sourceFileName => $($languageMatch.Paths -join ' | ') (derived=$derivedPath)") | Out-Null
                    }
                }
                else {
                    $summary.LanguageTargetAmbiguous++
                    $issues.Add("[$modName] ambiguous DefInjected target for $defType\\$sourceFileName => $($languageMatch.Paths -join ' | ')") | Out-Null
                }
            }
        }

        foreach ($languageDestination in @($languageDestinations | Select-Object -Unique)) {
            if ([string]::IsNullOrWhiteSpace($languageDestination)) {
                continue
            }

            $result = Copy-IfChanged -SourcePath $partFile.FullName -DestinationPath $languageDestination -ActionLabel 'DefInjected language file'
            Increment-SummaryCounter -Summary $summary -Name (Get-LanguageSummaryKey -Result $result)
        }

        switch ($defsMatch.Status) {
            'unique' {
                $defsUpdate = Update-DefsFromPartFile -PartFilePath $partFile.FullName -DefType $defType -DefsFilePath $defsMatch.Path
                if ($defsUpdate.FileChanged) {
                    $summary.DefFilesUpdated++
                }
                $summary.DefEntriesUpdated += $defsUpdate.ChangedEntries
                $summary.DefEntriesMissingPath += $defsUpdate.MissingEntries
                if ($defsUpdate.ParseError) {
                    $summary.DefSourceParseError++
                    $issues.Add("[$modName] parse error updating defs file $($defsMatch.Path)") | Out-Null
                }
            }
            'missing' {
                $summary.DefSourceMissing++
            }
            'ambiguous' {
                $summary.DefSourceAmbiguous++
                $issues.Add("[$modName] ambiguous defs source for $sourceFileName => $($defsMatch.Paths -join ' | ')") | Out-Null
            }
        }
    }
}

foreach ($key in $summary.Keys) {
    Write-Output ($key + '=' + $summary[$key])
}

if ($issues.Count -gt 0) {
    Write-Output 'Issues:'
    foreach ($issue in $issues) {
        Write-Output $issue
    }
}