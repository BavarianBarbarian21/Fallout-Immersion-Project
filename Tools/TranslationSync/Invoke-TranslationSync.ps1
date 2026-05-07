<#
.SYNOPSIS
    Incremental translation sync tool for the four translation parts.

.DESCRIPTION
    Builds and updates four translation mod outputs from configured source mod lists.
    The tool maintains an English dummy baseline and propagates only new or changed
    entries into other language folders as English fallback text.

        Part mapping:
            Part 1 = FIP source mods in this repository
            Part 2 = FCP source mods
            Part 3 = external mods referenced by FIP dependencies, load order, and patch metadata
            Part 4 = other playset mods not already covered by Parts 1-3

    Commands:
            refresh-lists  Refresh the part source lists.
      sync          Sync one category.
      sync-all      Sync every configured category.
#>

param(
    [Parameter(Mandatory)]
    [ValidateSet('refresh-lists', 'sync', 'sync-all')]
    [string]$Command,

    [ValidateSet('fip', 'fcp', 'compatible', 'playset-other', 'part1', 'part2', 'part3', 'part4')]
    [string]$Category,

    [string]$ConfigPath = (Join-Path $PSScriptRoot 'config.json'),

    [string]$PlaysetModsRoot,

    [string[]]$Languages,

    [switch]$RefreshLists,

    [switch]$RefreshFipSource,

    [switch]$RefreshFcpSources
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[void][System.Reflection.Assembly]::LoadWithPartialName('System.Web.Extensions')
$JsonSerializer = [System.Web.Script.Serialization.JavaScriptSerializer]::new()
$JsonSerializer.MaxJsonLength = 67108864

$TranslatableLeafNames = @{}
@(
    'label','labelShort','labelMale','labelFemale',
    'labelMalePlural','labelFemalePlural','labelPlural',
    'description','descriptionShort','baseDesc',
    'title','titleShort','titleFemale','titleShortFemale',
    'leaderTitle','leaderTitleFemale',
    'jobString','reportString','inspectString','baseInspectLine',
    'verb','chargeNoun',
    'letterLabel','letterText','letterDesc',
    'beginLetterLabel','beginLetterText',
    'arrivedLetterLabel','arrivedLetterText',
    'pawnsArrivalMessage','joinText',
    'successMessage','successText','failMessage','failText',
    'rejectInputMessage',
    'customLetterLabel','customLetterText',
    'gerundLabel','customLabel',
    'pawnLabel','pawnSingular','pawnPlural',
    'skillLabel','skillDescription',
    'headerTip','tip','helpText','summary',
    'text','note',
    'nameNoun','nameSuffix','namePrefix',
    'failTriggerText','pawnCannotEquipReason',
    'descriptionExtra','labelNounPretty',
    'customSummary','extraTooltip',
    'header',
    'baseTitle','baseTitleFemale',
    'pawnsPlural','leaderPawnSingular',
    'name',
    'summary'
) | ForEach-Object {
    $TranslatableLeafNames[$_.ToLowerInvariant()] = $true
}

$StringListParents = @{}
@('rulesStrings') | ForEach-Object {
    $StringListParents[$_.ToLowerInvariant()] = $true
}

function Resolve-CategoryAlias {
    param([string]$CategoryName)

    switch ($CategoryName) {
        'part1' { return 'fip' }
        'part2' { return 'fcp' }
        'part3' { return 'compatible' }
        'part4' { return 'playset-other' }
        default { return $CategoryName }
    }
}

function Import-TranslatableMembersFromRimWorldSource {
    param([string]$RimWorldRoot)

    if ([string]::IsNullOrWhiteSpace($RimWorldRoot)) {
        return
    }

    $sourceRoot = Join-Path $RimWorldRoot 'Source'
    if (-not (Test-Path -LiteralPath $sourceRoot)) {
        return
    }

    $mustTranslatePattern = [regex]'(?ms)\[MustTranslate(?:\([^\)]*\))?\]\s+public\s+(?<type>[\w<>,\[\]\.?]+)\s+(?<name>\w+)'
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter '*.cs' -File -ErrorAction SilentlyContinue) {
        $content = [System.IO.File]::ReadAllText($file.FullName, [System.Text.Encoding]::UTF8)
        foreach ($match in $mustTranslatePattern.Matches($content)) {
            $memberName = $match.Groups['name'].Value
            if ([string]::IsNullOrWhiteSpace($memberName)) {
                continue
            }

            $memberNameKey = $memberName.ToLowerInvariant()
            $memberType = $match.Groups['type'].Value.ToLowerInvariant().Replace(' ', '')
            if ($memberType -like 'list<string>' -or $memberType -like 'ilist<string>') {
                $StringListParents[$memberNameKey] = $true
            }
            else {
                $TranslatableLeafNames[$memberNameKey] = $true
            }
        }
    }
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        $null = New-Item -Path $Path -ItemType Directory -Force
    }
}

function Get-AbsolutePath {
    param(
        [string]$BasePath,
        [string]$Path
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Load-JsonFile {
    param([string]$FilePath)

    if (-not (Test-Path -LiteralPath $FilePath)) {
        return $null
    }

    $raw = [System.IO.File]::ReadAllText($FilePath, $Utf8NoBom)
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }

    return $JsonSerializer.DeserializeObject($raw)
}

function Save-JsonFile {
    param(
        [string]$FilePath,
        [object]$Data,
        [int]$Depth = 100
    )

    Ensure-Directory -Path (Split-Path -Parent $FilePath)
    $json = $Data | ConvertTo-Json -Depth $Depth
    [System.IO.File]::WriteAllText($FilePath, $json, $Utf8NoBom)
}

function Save-TextFile {
    param(
        [string]$FilePath,
        [string]$Content
    )

    Ensure-Directory -Path (Split-Path -Parent $FilePath)
    [System.IO.File]::WriteAllText($FilePath, $Content, $Utf8NoBom)
}

function Load-Config {
    param([string]$FilePath)

    $config = Load-JsonFile -FilePath $FilePath
    if ($null -eq $config) {
        throw "Config file not found or empty: $FilePath"
    }

    $configRoot = Split-Path -Parent $FilePath
    $config['__ConfigRoot'] = $configRoot

    $paths = @{}
    $paths.RepoRoot = Get-AbsolutePath -BasePath $configRoot -Path $config.paths.repoRoot
    $paths.RimWorldRoot = Get-AbsolutePath -BasePath $configRoot -Path $config.paths.rimWorldRoot
    $paths.FcpCacheRoot = Get-AbsolutePath -BasePath $configRoot -Path $config.paths.fcpCacheRoot
    $paths.LanguageTemplateRoot = Get-AbsolutePath -BasePath $configRoot -Path $config.paths.languageTemplateRoot
    $paths.PlaysetModsRoot = if ($PlaysetModsRoot) {
        Get-AbsolutePath -BasePath $configRoot -Path $PlaysetModsRoot
    }
    else {
        Get-AbsolutePath -BasePath $configRoot -Path $config.paths.playsetModsRoot
    }
    $paths.StateRoot = Get-AbsolutePath -BasePath $configRoot -Path $config.paths.stateRoot
    $paths.ModListRoot = Get-AbsolutePath -BasePath $configRoot -Path $config.paths.modListRoot
    $config.paths = $paths

    foreach ($categoryEntry in $config.categories) {
        $categoryEntry.outputModPath = Get-AbsolutePath -BasePath $configRoot -Path $categoryEntry.outputModPath
        $categoryEntry.modListPath = Get-AbsolutePath -BasePath $configRoot -Path $categoryEntry.modListPath
    }

    return $config
}

function Get-CategoryMap {
    param([hashtable]$Config)

    $map = @{}
    foreach ($category in $Config.categories) {
        $map[$category.id] = $category
    }
    return $map
}

function Get-LanguageList {
    param(
        [hashtable]$Config,
        [string[]]$RequestedLanguages
    )

    $templateRoot = $Config.paths.LanguageTemplateRoot
    if (-not (Test-Path -LiteralPath $templateRoot)) {
        throw "Language template root not found: $templateRoot"
    }

    $available = @(Get-ChildItem -LiteralPath $templateRoot -Directory | Select-Object -ExpandProperty Name | Sort-Object)
    if ($available -notcontains 'English') {
        $available = @('English') + $available
    }

    if ($RequestedLanguages -and $RequestedLanguages.Count -gt 0) {
        $selected = New-Object 'System.Collections.Generic.List[string]'
        foreach ($language in $RequestedLanguages) {
            if ($available -contains $language) {
                $selected.Add($language) | Out-Null
            }
            else {
                throw "Unknown language '$language'. Available languages come from $templateRoot."
            }
        }
        if ($selected -notcontains 'English') {
            $selected.Insert(0, 'English')
        }
        return @($selected)
    }

    return @($available)
}

function Get-DirectoryStamp {
    param([string]$FilePath)

    $item = Get-Item -LiteralPath $FilePath -Force
    return '{0}|{1}' -f $item.Length, $item.LastWriteTimeUtc.Ticks
}

function Escape-XmlText {
    param([string]$Text)

    $escaped = $Text.Replace('&', '&amp;')
    $escaped = $escaped.Replace('<', '&lt;')
    $escaped = $escaped.Replace('>', '&gt;')
    $escaped = $escaped.Replace([string][char]34, '&quot;')
    return $escaped
}

function Get-TranslatablePairs {
    param(
        [System.Xml.XmlElement]$Element,
        [string]$PathPrefix,
        [bool]$IsStringListParent = $false
    )

    $results = New-Object 'System.Collections.Generic.List[object]'
    $liIndex = 0

    foreach ($child in $Element.ChildNodes) {
        if ($child -isnot [System.Xml.XmlElement]) {
            continue
        }

        $childName = $child.LocalName
        $isLi = ($childName -eq 'li')
        $childPath = if ($isLi) {
            $currentIndex = $liIndex
            $liIndex++
            "${PathPrefix}.${currentIndex}"
        }
        else {
            if ($PathPrefix) {
                "${PathPrefix}.${childName}"
            }
            else {
                $childName
            }
        }

        $childElements = @($child.ChildNodes | Where-Object { $_ -is [System.Xml.XmlElement] })
        $hasChildElements = $childElements.Count -gt 0

        if ($IsStringListParent -and $isLi) {
            $text = $child.InnerText.Trim()
            if ($text) {
                $results.Add([pscustomobject]@{
                    Key  = $childPath
                    Text = Escape-XmlText -Text $text
                }) | Out-Null
            }
            continue
        }

        if (-not $hasChildElements) {
            $leafKey = $childName.ToLowerInvariant()
            if ($TranslatableLeafNames.ContainsKey($leafKey)) {
                $text = $child.InnerText.Trim()
                if ($text) {
                    $results.Add([pscustomobject]@{
                        Key  = $childPath
                        Text = Escape-XmlText -Text $text
                    }) | Out-Null
                }
            }
            continue
        }

        $childIsStringList = $StringListParents.ContainsKey($childName.ToLowerInvariant())
        $subResults = Get-TranslatablePairs -Element $child -PathPrefix $childPath -IsStringListParent $childIsStringList
        foreach ($subResult in $subResults) {
            $results.Add($subResult) | Out-Null
        }
    }

    return @($results)
}

function Read-DefsXmlDocument {
    param([string]$FilePath)

    $raw = [System.IO.File]::ReadAllText($FilePath, [System.Text.Encoding]::UTF8)
    $raw = [regex]::Replace($raw, '&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9a-fA-F]+;|\w+;)', '&amp;')
    try {
        return [xml]$raw
    }
    catch {
        try {
            $withoutHeader = [regex]::Replace($raw, '<\?xml[^>]*\?>', '', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            return [xml]("<?xml version='1.0' encoding='UTF-8'?><Defs>$withoutHeader</Defs>")
        }
        catch {
            Write-Warning "Skipping parse error in $FilePath"
            return $null
        }
    }
}

function Extract-DefFile {
    param([string]$FilePath)

    $doc = Read-DefsXmlDocument -FilePath $FilePath
    if ($null -eq $doc -or $null -eq $doc.Defs) {
        return @()
    }

    $byType = @{}
    foreach ($defNode in $doc.Defs.ChildNodes) {
        if ($defNode -isnot [System.Xml.XmlElement]) {
            continue
        }

        if ($defNode.GetAttribute('Abstract') -ieq 'True') {
            continue
        }

        $defNameNode = $defNode.SelectSingleNode('./defName')
        if ($null -eq $defNameNode -or [string]::IsNullOrWhiteSpace($defNameNode.InnerText)) {
            continue
        }

        $pairs = Get-TranslatablePairs -Element $defNode -PathPrefix $defNameNode.InnerText.Trim()
        if ($pairs.Count -eq 0) {
            continue
        }

        $defType = $defNode.LocalName
        if (-not $byType.ContainsKey($defType)) {
            $byType[$defType] = New-Object 'System.Collections.Generic.List[object]'
        }

        foreach ($pair in $pairs) {
            $byType[$defType].Add($pair) | Out-Null
        }
    }

    $groups = New-Object 'System.Collections.Generic.List[object]'
    foreach ($defType in ($byType.Keys | Sort-Object)) {
        $groups.Add([pscustomobject]@{
            DefType = $defType
            Entries = @($byType[$defType] | Sort-Object Key)
        }) | Out-Null
    }

    return @($groups)
}

function Read-LanguageXml {
    param([string]$FilePath)

    if (-not (Test-Path -LiteralPath $FilePath)) {
        return $null
    }

    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    $document.Load($FilePath)

    $comments = New-Object 'System.Collections.Generic.List[string]'
    $entries = New-Object 'System.Collections.Specialized.OrderedDictionary'

    foreach ($node in $document.DocumentElement.ChildNodes) {
        if ($node.NodeType -eq [System.Xml.XmlNodeType]::Comment) {
            $comments.Add($node.Value.Trim()) | Out-Null
            continue
        }

        if ($node.NodeType -ne [System.Xml.XmlNodeType]::Element) {
            continue
        }

        $entries[$node.LocalName] = $node.InnerXml
    }

    return [pscustomobject]@{
        Comments = @($comments)
        Entries  = $entries
    }
}

function Write-LanguageXml {
    param(
        [string]$FilePath,
        [System.Collections.Specialized.OrderedDictionary]$Entries,
        [string[]]$Comments
    )

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    [void]$builder.AppendLine('<LanguageData>')

    if ($Comments) {
        foreach ($comment in $Comments) {
            [void]$builder.AppendLine("  <!-- $comment -->")
        }
        if ($Comments.Count -gt 0) {
            [void]$builder.AppendLine()
        }
    }

    foreach ($key in $Entries.Keys) {
        [void]$builder.AppendLine("  <$key>$($Entries[$key])</$key>")
    }

    [void]$builder.AppendLine('</LanguageData>')
    Save-TextFile -FilePath $FilePath -Content $builder.ToString()
}

function Read-NamesFile {
    param([string]$FilePath)

    if (-not (Test-Path -LiteralPath $FilePath)) {
        return $null
    }

    return @([System.IO.File]::ReadAllLines($FilePath, [System.Text.Encoding]::UTF8))
}

function Write-NamesFile {
    param(
        [string]$FilePath,
        [string[]]$Lines
    )

    Ensure-Directory -Path (Split-Path -Parent $FilePath)
    [System.IO.File]::WriteAllLines($FilePath, $Lines, $Utf8NoBom)
}

function Get-OrderedEntriesFromPairs {
    param([object[]]$Pairs)

    $ordered = New-Object 'System.Collections.Specialized.OrderedDictionary'
    foreach ($pair in $Pairs) {
        $ordered[$pair.Key] = $pair.Text
    }
    return $ordered
}

function Get-RelativePathFragment {
    param(
        [string]$BasePath,
        [string]$FullPath
    )

    $relative = $FullPath.Substring($BasePath.Length).TrimStart('\')
    return $relative.Replace('\', '/')
}

function Load-State {
    param([hashtable]$Config)

    $statePath = Join-Path $Config.paths.StateRoot 'translation-sync-state.json'
    $state = Load-JsonFile -FilePath $statePath
    if ($null -eq $state) {
        $state = @{
            version  = 1
            defCache = @{}
        }
    }

    if (-not $state.ContainsKey('defCache')) {
        $state.defCache = @{}
    }

    return [pscustomobject]@{
        Path = $statePath
        Data = $state
    }
}

function Save-State {
    param([pscustomobject]$State)

    Save-JsonFile -FilePath $State.Path -Data $State.Data
}

function Get-AboutMetadata {
    param([string]$ModRoot)

    $aboutPath = Join-Path $ModRoot 'About\About.xml'
    $fallback = [pscustomobject]@{
        Name      = [System.IO.Path]::GetFileName($ModRoot)
        PackageId = [System.IO.Path]::GetFileName($ModRoot)
    }

    if (-not (Test-Path -LiteralPath $aboutPath)) {
        return $fallback
    }

    try {
        [xml]$about = [System.IO.File]::ReadAllText($aboutPath, [System.Text.Encoding]::UTF8)
        $name = if ($about.ModMetaData.name) { $about.ModMetaData.name } else { $fallback.Name }
        $packageId = if ($about.ModMetaData.packageId) { $about.ModMetaData.packageId } else { $fallback.PackageId }
        return [pscustomobject]@{
            Name      = [string]$name
            PackageId = [string]$packageId
        }
    }
    catch {
        return $fallback
    }
}

function Get-ModMetadataIndex {
    param([string[]]$Roots)

    $index = @{}
    foreach ($root in $Roots) {
        if ([string]::IsNullOrWhiteSpace($root) -or -not (Test-Path -LiteralPath $root)) {
            continue
        }

        foreach ($directory in Get-ChildItem -LiteralPath $root -Directory -ErrorAction SilentlyContinue) {
            $meta = Get-AboutMetadata -ModRoot $directory.FullName
            if (-not $index.ContainsKey($meta.PackageId)) {
                $index[$meta.PackageId] = [pscustomobject]@{
                    FolderName = $directory.Name
                    ModRoot    = $directory.FullName
                    Name       = $meta.Name
                    PackageId  = $meta.PackageId
                }
            }
        }
    }
    return $index
}

function Test-IsIgnoredPackageId {
    param(
        [string]$PackageId,
        [hashtable]$Config
    )

    if ([string]::IsNullOrWhiteSpace($PackageId)) {
        return $true
    }

    foreach ($pattern in $Config.compatibility.ignorePackageIdPrefixes) {
        if ($PackageId.StartsWith($pattern, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Split-PackageIdValues {
    param([string]$RawValue)

    if ([string]::IsNullOrWhiteSpace($RawValue)) {
        return @()
    }

    return @($RawValue -split '\s*,\s*' | ForEach-Object {
        $_.Trim()
    } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Get-RequiredPackageIdsFromXmlFile {
    param([string]$FilePath)

    $packageIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $content = [System.IO.File]::ReadAllText($FilePath, [System.Text.Encoding]::UTF8)
    foreach ($match in [regex]::Matches($content, '(MayRequire|IfModActive|IfModNotActive)\s*=\s*"([^"]+)"', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        foreach ($packageId in Split-PackageIdValues -RawValue $match.Groups[2].Value) {
            [void]$packageIds.Add($packageId)
        }
    }
    return $packageIds
}

function Get-RequiredPackageIdsFromAboutFile {
    param([string]$FilePath)

    $packageIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    try {
        [xml]$about = [System.IO.File]::ReadAllText($FilePath, [System.Text.Encoding]::UTF8)
    }
    catch {
        return $packageIds
    }

    $nodePaths = @(
        '/ModMetaData/modDependencies/li/packageId',
        '/ModMetaData/modDependenciesByVersion/li/packageId',
        '/ModMetaData/modDependenciesOptional/li/packageId',
        '/ModMetaData/loadAfter/li'
    )

    foreach ($nodePath in $nodePaths) {
        $nodes = $about.SelectNodes($nodePath)
        if ($null -eq $nodes) {
            continue
        }

        foreach ($node in $nodes) {
            if (-not [string]::IsNullOrWhiteSpace($node.InnerText)) {
                foreach ($packageId in Split-PackageIdValues -RawValue $node.InnerText.Trim()) {
                    [void]$packageIds.Add($packageId)
                }
            }
        }
    }

    return $packageIds
}

function Get-FipModDirectories {
    param([hashtable]$Config)

    return @(Get-ChildItem -LiteralPath $Config.paths.RepoRoot -Directory | Where-Object {
        $_.Name -like 'FIP-*' -and
        $_.Name -notlike 'FIP-Translation*' -and
        $_.Name -notin @('Tools', 'Translations', 'Guidelines', 'Outdated')
    } | Sort-Object Name)
}

function Refresh-FipList {
    param(
        [hashtable]$Config,
        [switch]$UpdateSource
    )

    if ($UpdateSource) {
        $gitDirectory = Join-Path $Config.paths.RepoRoot '.git'
        if (Test-Path -LiteralPath $gitDirectory) {
            Write-Host 'Updating local FIP repository'
            Invoke-Git -WorkingDirectory $Config.paths.RepoRoot -Arguments @('pull', '--ff-only')
        }
    }

    $path = (Get-CategoryMap -Config $Config)['fip'].modListPath
    $names = @(Get-FipModDirectories -Config $Config | Select-Object -ExpandProperty Name)
    Save-TextFile -FilePath $path -Content (($names -join [Environment]::NewLine) + [Environment]::NewLine)
    return $names
}

function Invoke-Git {
    param(
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )

    $oldLocation = Get-Location
    try {
        if ($WorkingDirectory) {
            Set-Location -LiteralPath $WorkingDirectory
        }
        & git @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Set-Location -LiteralPath $oldLocation
    }
}

function Remove-EmptyDirectories {
    param([string]$RootPath)

    if (-not (Test-Path -LiteralPath $RootPath)) {
        return
    }

    $directories = @(Get-ChildItem -LiteralPath $RootPath -Directory -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    foreach ($directory in $directories) {
        $childCount = @(Get-ChildItem -LiteralPath $directory.FullName -Force -ErrorAction SilentlyContinue).Count
        if ($childCount -eq 0) {
            Remove-Item -LiteralPath $directory.FullName -Force
        }
    }
}

function Get-FcpRepositoriesFromGitHub {
    param([hashtable]$Config)

    $org = $Config.fcp.githubOrg
    $headers = @{ 'User-Agent' = 'FIP-TranslationSync' }
    if ($env:GITHUB_TOKEN) {
        $headers.Authorization = "Bearer $($env:GITHUB_TOKEN)"
    }

    $page = 1
    $names = New-Object 'System.Collections.Generic.List[string]'
    while ($true) {
        $url = "https://api.github.com/orgs/$org/repos?per_page=100&page=$page&type=public"
        $response = Invoke-RestMethod -Uri $url -Headers $headers -Method Get
        if ($null -eq $response -or $response.Count -eq 0) {
            break
        }

        foreach ($repo in $response) {
            $matchesInclude = $false
            foreach ($pattern in $Config.fcp.repoNamePatterns) {
                if ($repo.name -like $pattern) {
                    $matchesInclude = $true
                    break
                }
            }
            if ($matchesInclude) {
                $names.Add($repo.name) | Out-Null
            }
        }

        if ($response.Count -lt 100) {
            break
        }
        $page++
    }

    return @($names | Sort-Object -Unique)
}

function Refresh-FcpList {
    param(
        [hashtable]$Config,
        [switch]$UpdateSources
    )

    $repoNames = Get-FcpRepositoriesFromGitHub -Config $Config
    $path = (Get-CategoryMap -Config $Config)['fcp'].modListPath
    Save-TextFile -FilePath $path -Content (($repoNames -join [Environment]::NewLine) + [Environment]::NewLine)

    if ($UpdateSources) {
        Ensure-Directory -Path $Config.paths.FcpCacheRoot
        foreach ($repoName in $repoNames) {
            $target = Join-Path $Config.paths.FcpCacheRoot $repoName
            $cloneUrl = "https://github.com/$($Config.fcp.githubOrg)/$repoName.git"
            if (Test-Path -LiteralPath $target) {
                Write-Host "Updating FCP repo $repoName"
                Invoke-Git -WorkingDirectory $target -Arguments @('fetch', '--depth', '1', 'origin')
                Invoke-Git -WorkingDirectory $target -Arguments @('reset', '--hard', 'origin/HEAD')
            }
            else {
                Write-Host "Cloning FCP repo $repoName"
                Invoke-Git -WorkingDirectory $Config.paths.FcpCacheRoot -Arguments @('clone', '--depth', '1', $cloneUrl, $repoName)
            }
        }
    }

    return $repoNames
}

function Refresh-CompatibleList {
    param([hashtable]$Config)

    $fipMods = Get-FipModDirectories -Config $Config
    $referencedPackageIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($mod in $fipMods) {
        $aboutPath = Join-Path $mod.FullName 'About\About.xml'
        if (Test-Path -LiteralPath $aboutPath) {
            $aboutIds = Get-RequiredPackageIdsFromAboutFile -FilePath $aboutPath
            foreach ($packageId in $aboutIds) {
                if (-not (Test-IsIgnoredPackageId -PackageId $packageId -Config $Config)) {
                    [void]$referencedPackageIds.Add($packageId)
                }
            }
        }

        foreach ($xmlFile in Get-ChildItem -LiteralPath $mod.FullName -Recurse -Filter '*.xml' -File -ErrorAction SilentlyContinue) {
            $xmlIds = Get-RequiredPackageIdsFromXmlFile -FilePath $xmlFile.FullName
            foreach ($packageId in $xmlIds) {
                if (-not (Test-IsIgnoredPackageId -PackageId $packageId -Config $Config)) {
                    [void]$referencedPackageIds.Add($packageId)
                }
            }
        }
    }

    $lookupRoots = @($Config.paths.RimWorldRoot, $Config.paths.FcpCacheRoot) + @($Config.compatibility.additionalLookupRoots | ForEach-Object {
        Get-AbsolutePath -BasePath $Config.__ConfigRoot -Path $_
    })
    $metadataIndex = Get-ModMetadataIndex -Roots $lookupRoots

    $resolvedNames = New-Object 'System.Collections.Generic.List[string]'
    $unresolved = New-Object 'System.Collections.Generic.List[string]'
    foreach ($packageId in ($referencedPackageIds | Sort-Object)) {
        if ($metadataIndex.ContainsKey($packageId)) {
            $resolvedNames.Add($metadataIndex[$packageId].FolderName) | Out-Null
        }
        else {
            $unresolved.Add($packageId) | Out-Null
        }
    }

    $resolvedNames = @($resolvedNames | Sort-Object -Unique)
    $category = (Get-CategoryMap -Config $Config)['compatible']
    Save-TextFile -FilePath $category.modListPath -Content (($resolvedNames -join [Environment]::NewLine) + [Environment]::NewLine)

    $unresolvedPath = Join-Path $Config.paths.ModListRoot 'compatible-unresolved-packageIds.txt'
    if ($unresolved.Count -gt 0) {
        Save-TextFile -FilePath $unresolvedPath -Content (($unresolved -join [Environment]::NewLine) + [Environment]::NewLine)
    }
    elseif (Test-Path -LiteralPath $unresolvedPath) {
        Remove-Item -LiteralPath $unresolvedPath -Force
    }

    return $resolvedNames
}

function Refresh-PlaysetOtherList {
    param([hashtable]$Config)

    $category = (Get-CategoryMap -Config $Config)['playset-other']
    if ([string]::IsNullOrWhiteSpace($Config.paths.PlaysetModsRoot) -or -not (Test-Path -LiteralPath $Config.paths.PlaysetModsRoot)) {
        Save-TextFile -FilePath $category.modListPath -Content ''
        return @()
    }

    $excludedNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($listPath in @(
        (Get-CategoryMap -Config $Config)['fip'].modListPath,
        (Get-CategoryMap -Config $Config)['fcp'].modListPath,
        (Get-CategoryMap -Config $Config)['compatible'].modListPath
    )) {
        if (Test-Path -LiteralPath $listPath) {
            foreach ($line in [System.IO.File]::ReadAllLines($listPath, [System.Text.Encoding]::UTF8)) {
                if (-not [string]::IsNullOrWhiteSpace($line)) {
                    [void]$excludedNames.Add($line.Trim())
                }
            }
        }
    }

    $remaining = @(Get-ChildItem -LiteralPath $Config.paths.PlaysetModsRoot -Directory | Where-Object {
        -not $excludedNames.Contains($_.Name)
    } | Sort-Object Name | Select-Object -ExpandProperty Name)
    Save-TextFile -FilePath $category.modListPath -Content (($remaining -join [Environment]::NewLine) + [Environment]::NewLine)
    return $remaining
}

function Refresh-ModLists {
    param(
        [hashtable]$Config,
        [switch]$UpdateFipSource,
        [switch]$UpdateFcpSources
    )

    Write-Host 'Refreshing Part 1 source list'
    $null = Refresh-FipList -Config $Config -UpdateSource:$UpdateFipSource

    Write-Host 'Refreshing Part 2 source list'
    $null = Refresh-FcpList -Config $Config -UpdateSources:$UpdateFcpSources

    Write-Host 'Refreshing Part 3 source list'
    $null = Refresh-CompatibleList -Config $Config

    Write-Host 'Refreshing Part 4 source list'
    $null = Refresh-PlaysetOtherList -Config $Config
}

function Get-ModNamesFromList {
    param([string]$FilePath)

    if (-not (Test-Path -LiteralPath $FilePath)) {
        return @()
    }

    return @([System.IO.File]::ReadAllLines($FilePath, [System.Text.Encoding]::UTF8) | ForEach-Object {
        $_.Trim()
    } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Resolve-ModRoot {
    param(
        [string]$ModName,
        [string[]]$SearchRoots
    )

    foreach ($root in $SearchRoots) {
        if ([string]::IsNullOrWhiteSpace($root) -or -not (Test-Path -LiteralPath $root)) {
            continue
        }
        $candidate = Join-Path $root $ModName
        if (Test-Path -LiteralPath $candidate) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }

    return $null
}

function Get-CategorySearchRoots {
    param(
        [hashtable]$Config,
        [hashtable]$CategoryConfig
    )

    switch ($CategoryConfig.id) {
        'fip' { return @($Config.paths.RepoRoot) }
        'fcp' { return @($Config.paths.FcpCacheRoot, (Join-Path $Config.paths.RimWorldRoot 'FCP Mods')) }
        'compatible' {
            $roots = New-Object 'System.Collections.Generic.List[string]'
            $roots.Add($Config.paths.RimWorldRoot) | Out-Null
            $roots.Add((Join-Path $Config.paths.RimWorldRoot 'Data')) | Out-Null
            $roots.Add((Join-Path $Config.paths.RimWorldRoot 'FCP Mods')) | Out-Null
            foreach ($path in $Config.compatibility.additionalLookupRoots) {
                $roots.Add((Get-AbsolutePath -BasePath $Config.__ConfigRoot -Path $path)) | Out-Null
            }
            return @($roots | Select-Object -Unique)
        }
        'playset-other' {
            return @($Config.paths.PlaysetModsRoot)
        }
        default {
            throw "Unknown category: $($CategoryConfig.id)"
        }
    }
}

function Get-SourceLanguageDirs {
    param(
        [string]$ModRoot,
        [string]$LeafSuffix
    )

    return @(Get-ChildItem -LiteralPath $ModRoot -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $_.FullName -like "*\\Languages\\English\\$LeafSuffix"
    })
}

function Add-KeyedOutputsFromMod {
    param(
        [string]$ModRoot,
        [string]$OutputStem,
        [System.Collections.Generic.List[object]]$Outputs
    )

    foreach ($dir in Get-SourceLanguageDirs -ModRoot $ModRoot -LeafSuffix 'Keyed') {
        foreach ($file in Get-ChildItem -LiteralPath $dir.FullName -Filter '*.xml' -File -ErrorAction SilentlyContinue | Sort-Object Name) {
            $outputs.Add([pscustomobject]@{
                Kind          = 'Keyed'
                OutputRelPath = ('Keyed/{0}__{1}' -f $OutputStem, $file.Name).Replace('/', '\')
                SourcePath    = $file.FullName
            }) | Out-Null
        }
    }
}

function Add-NamesOutputsFromMod {
    param(
        [string]$ModRoot,
        [string]$OutputStem,
        [System.Collections.Generic.List[object]]$Outputs
    )

    foreach ($dir in Get-SourceLanguageDirs -ModRoot $ModRoot -LeafSuffix 'Strings\Names') {
        foreach ($file in Get-ChildItem -LiteralPath $dir.FullName -Filter '*.txt' -File -ErrorAction SilentlyContinue | Sort-Object Name) {
            $outputs.Add([pscustomobject]@{
                Kind          = 'Names'
                OutputRelPath = ('Strings/Names/{0}__{1}' -f $OutputStem, $file.Name).Replace('/', '\')
                SourcePath    = $file.FullName
            }) | Out-Null
        }
    }
}

function Add-DefInjectedOutputsFromMod {
    param(
        [string]$ModRoot,
        [string]$OutputStem,
        [System.Collections.Generic.List[object]]$Outputs,
        [pscustomobject]$State
    )

    $defDirectories = @(Get-ChildItem -LiteralPath $ModRoot -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -eq 'Defs'
    })

    foreach ($defDir in $defDirectories) {
        foreach ($file in Get-ChildItem -LiteralPath $defDir.FullName -Filter '*.xml' -File -ErrorAction SilentlyContinue | Sort-Object Name) {
            $stamp = Get-DirectoryStamp -FilePath $file.FullName
            $cacheRecord = $null
            if ($State.Data.defCache.ContainsKey($file.FullName)) {
                $candidate = $State.Data.defCache[$file.FullName]
                if ($candidate.stamp -eq $stamp) {
                    $cacheRecord = $candidate
                }
            }

            $groups = if ($null -ne $cacheRecord) {
                @($cacheRecord.groups)
            }
            else {
                $parsedGroups = @(Extract-DefFile -FilePath $file.FullName)
                $serializedGroups = New-Object 'System.Collections.Generic.List[object]'
                foreach ($group in $parsedGroups) {
                    $serializedEntries = New-Object 'System.Collections.Generic.List[object]'
                    foreach ($entry in $group.Entries) {
                        $serializedEntries.Add(@{ Key = $entry.Key; Text = $entry.Text }) | Out-Null
                    }
                    $serializedGroups.Add(@{ DefType = $group.DefType; Entries = @($serializedEntries) }) | Out-Null
                }
                $State.Data.defCache[$file.FullName] = @{
                    stamp  = $stamp
                    groups = @($serializedGroups)
                }
                @($serializedGroups)
            }

            foreach ($group in $groups) {
                if ($group.Entries.Count -eq 0) {
                    continue
                }
                $outputs.Add([pscustomobject]@{
                    Kind          = 'DefInjected'
                    OutputRelPath = ('DefInjected/{0}/{1}__{2}' -f $group.DefType, $OutputStem, $file.Name).Replace('/', '\')
                    Entries       = @($group.Entries)
                }) | Out-Null
            }
        }
    }
}

function Get-CategorySources {
    param(
        [hashtable]$Config,
        [hashtable]$CategoryConfig,
        [pscustomobject]$State
    )

    $modNames = Get-ModNamesFromList -FilePath $CategoryConfig.modListPath
    $searchRoots = Get-CategorySearchRoots -Config $Config -CategoryConfig $CategoryConfig
    $resolvedMods = New-Object 'System.Collections.Generic.List[object]'
    $missingMods = New-Object 'System.Collections.Generic.List[string]'

    foreach ($modName in $modNames) {
        $modRoot = Resolve-ModRoot -ModName $modName -SearchRoots $searchRoots
        if ($null -eq $modRoot) {
            $missingMods.Add($modName) | Out-Null
            continue
        }

        $metadata = Get-AboutMetadata -ModRoot $modRoot
        $outputs = New-Object 'System.Collections.Generic.List[object]'
        Add-KeyedOutputsFromMod -ModRoot $modRoot -OutputStem $modName -Outputs $outputs
        Add-NamesOutputsFromMod -ModRoot $modRoot -OutputStem $modName -Outputs $outputs
        Add-DefInjectedOutputsFromMod -ModRoot $modRoot -OutputStem $modName -Outputs $outputs -State $State

        $resolvedMods.Add([pscustomobject]@{
            FolderName = $modName
            ModRoot    = $modRoot
            Name       = $metadata.Name
            PackageId  = $metadata.PackageId
            Outputs    = @($outputs)
        }) | Out-Null
    }

    return [pscustomobject]@{
        Mods       = @($resolvedMods)
        MissingMods = @($missingMods)
    }
}

function Merge-XmlEntries {
    param(
        [System.Collections.Specialized.OrderedDictionary]$CurrentEnglish,
        [System.Collections.Specialized.OrderedDictionary]$PreviousEnglish,
        [System.Collections.Specialized.OrderedDictionary]$ExistingLocale
    )

    $merged = New-Object 'System.Collections.Specialized.OrderedDictionary'
    foreach ($key in $CurrentEnglish.Keys) {
        $currentValue = [string]$CurrentEnglish[$key]
        $hasPrevious = $null -ne $PreviousEnglish -and $PreviousEnglish.Contains($key)
        $hasLocale = $null -ne $ExistingLocale -and $ExistingLocale.Contains($key)

        if ($hasPrevious -and $hasLocale -and [string]$PreviousEnglish[$key] -eq $currentValue) {
            $merged[$key] = $ExistingLocale[$key]
        }
        else {
            $merged[$key] = $currentValue
        }
    }
    return $merged
}

function Merge-NameLines {
    param(
        [string[]]$CurrentEnglish,
        [string[]]$PreviousEnglish,
        [string[]]$ExistingLocale
    )

    $merged = New-Object 'System.Collections.Generic.List[string]'
    for ($index = 0; $index -lt $CurrentEnglish.Count; $index++) {
        $currentValue = $CurrentEnglish[$index]
        $keepLocale = $false
        if ($null -ne $PreviousEnglish -and $index -lt $PreviousEnglish.Count -and $null -ne $ExistingLocale -and $index -lt $ExistingLocale.Count) {
            if ($PreviousEnglish[$index] -eq $currentValue) {
                $keepLocale = $true
            }
        }
        if ($keepLocale) {
            $merged.Add($ExistingLocale[$index]) | Out-Null
        }
        else {
            $merged.Add($currentValue) | Out-Null
        }
    }
    return @($merged)
}

function Test-FileContentEquals {
    param(
        [string]$FilePath,
        [string]$Content
    )

    if (-not (Test-Path -LiteralPath $FilePath)) {
        return $false
    }

    $existing = [System.IO.File]::ReadAllText($FilePath, $Utf8NoBom)
    return $existing -eq $Content
}

function Build-AboutXml {
    param(
        [hashtable]$Config,
        [hashtable]$CategoryConfig,
        [object[]]$CategoryMods,
        [hashtable]$AllCategorySources
    )

    $packageIds = New-Object 'System.Collections.Generic.List[string]'
    $dependencyItems = New-Object 'System.Collections.Generic.List[object]'
    foreach ($mod in ($CategoryMods | Sort-Object Name)) {
        if (-not [string]::IsNullOrWhiteSpace($mod.PackageId)) {
            $packageIds.Add($mod.PackageId) | Out-Null
            $dependencyItems.Add([pscustomobject]@{
                PackageId   = $mod.PackageId
                DisplayName = $mod.Name
            }) | Out-Null
        }
    }

    if ($CategoryConfig.loadAfterAllKnownPackages) {
        foreach ($sourceInfo in $AllCategorySources.Values) {
            foreach ($mod in $sourceInfo.Mods) {
                if (-not [string]::IsNullOrWhiteSpace($mod.PackageId)) {
                    $packageIds.Add($mod.PackageId) | Out-Null
                }
            }
        }
    }

    foreach ($extraPackageId in $CategoryConfig.extraLoadAfterPackageIds) {
        if (-not [string]::IsNullOrWhiteSpace($extraPackageId)) {
            $packageIds.Add($extraPackageId) | Out-Null
        }
    }

    $packageIds = @($packageIds | Sort-Object -Unique)
    $supportedVersions = @($Config.modDefaults.supportedVersions)

    $builder = [System.Text.StringBuilder]::new()
    [void]$builder.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    [void]$builder.AppendLine('<ModMetaData>')
    [void]$builder.AppendLine("  <packageId>$($CategoryConfig.packageId)</packageId>")
    [void]$builder.AppendLine("  <name>$([System.Security.SecurityElement]::Escape($CategoryConfig.name))</name>")
    [void]$builder.AppendLine("  <author>$([System.Security.SecurityElement]::Escape($Config.modDefaults.author))</author>")
    [void]$builder.AppendLine("  <description>$([System.Security.SecurityElement]::Escape($CategoryConfig.description))</description>")
    [void]$builder.AppendLine('  <supportedVersions>')
    foreach ($version in $supportedVersions) {
        [void]$builder.AppendLine("    <li>$version</li>")
    }
    [void]$builder.AppendLine('  </supportedVersions>')

    [void]$builder.AppendLine('  <modDependenciesOptional>')
    foreach ($item in $dependencyItems) {
        [void]$builder.AppendLine('    <li>')
        [void]$builder.AppendLine("      <packageId>$($item.PackageId)</packageId>")
        [void]$builder.AppendLine("      <displayName>$([System.Security.SecurityElement]::Escape($item.DisplayName))</displayName>")
        [void]$builder.AppendLine('    </li>')
    }
    [void]$builder.AppendLine('  </modDependenciesOptional>')

    [void]$builder.AppendLine('  <loadAfter>')
    foreach ($packageId in $packageIds) {
        [void]$builder.AppendLine("    <li>$packageId</li>")
    }
    [void]$builder.AppendLine('  </loadAfter>')
    [void]$builder.AppendLine('</ModMetaData>')
    return $builder.ToString()
}

function Remove-StaleOutputFiles {
    param(
        [string]$OutputRoot,
        [string[]]$Languages,
        [string[]]$ExpectedRelativePaths
    )

    $expectedSet = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $ExpectedRelativePaths) {
        [void]$expectedSet.Add($path.Replace('/', '\'))
    }

    foreach ($language in $Languages) {
        $languageRoot = Join-Path $OutputRoot "Languages\$language"
        if (-not (Test-Path -LiteralPath $languageRoot)) {
            continue
        }

        foreach ($file in Get-ChildItem -LiteralPath $languageRoot -File -Recurse -ErrorAction SilentlyContinue) {
            $relative = Get-RelativePathFragment -BasePath $languageRoot -FullPath $file.FullName
            $relative = $relative.Replace('/', '\')
            if (-not $expectedSet.Contains($relative)) {
                Remove-Item -LiteralPath $file.FullName -Force
            }
        }

        Remove-EmptyDirectories -RootPath $languageRoot
    }
}

function Sync-Category {
    param(
        [hashtable]$Config,
        [hashtable]$CategoryConfig,
        [string[]]$ResolvedLanguages,
        [pscustomobject]$State,
        [hashtable]$AllCategorySources
    )

    $categorySources = $AllCategorySources[$CategoryConfig.id]
    $outputRoot = $CategoryConfig.outputModPath
    $englishRoot = Join-Path $outputRoot 'Languages\English'
    Ensure-Directory -Path $englishRoot

    $aboutXml = Build-AboutXml -Config $Config -CategoryConfig $CategoryConfig -CategoryMods $categorySources.Mods -AllCategorySources $AllCategorySources
    Save-TextFile -FilePath (Join-Path $outputRoot 'About\About.xml') -Content $aboutXml

    $report = New-Object 'System.Collections.Generic.List[string]'
    if ($categorySources.MissingMods.Count -gt 0) {
        foreach ($missing in $categorySources.MissingMods) {
            $report.Add("Missing mod root for list entry: $missing") | Out-Null
        }
    }

    $outputs = New-Object 'System.Collections.Generic.List[object]'
    foreach ($mod in $categorySources.Mods) {
        foreach ($output in $mod.Outputs) {
            $outputs.Add($output) | Out-Null
        }
    }

    $expectedPaths = @($outputs | Select-Object -ExpandProperty OutputRelPath -Unique)
    Remove-StaleOutputFiles -OutputRoot $outputRoot -Languages $ResolvedLanguages -ExpectedRelativePaths $expectedPaths

    foreach ($output in $outputs) {
        $englishPath = Join-Path $englishRoot $output.OutputRelPath
        switch ($output.Kind) {
            'Keyed' {
                $previousEnglish = Read-LanguageXml -FilePath $englishPath
                Ensure-Directory -Path (Split-Path -Parent $englishPath)
                [System.IO.File]::Copy($output.SourcePath, $englishPath, $true)
                $currentEnglish = Read-LanguageXml -FilePath $englishPath
                foreach ($language in $ResolvedLanguages) {
                    if ($language -eq 'English') {
                        continue
                    }

                    $localePath = Join-Path $outputRoot (Join-Path "Languages\$language" $output.OutputRelPath)
                    $existingLocale = Read-LanguageXml -FilePath $localePath
                    $mergedEntries = Merge-XmlEntries -CurrentEnglish $currentEnglish.Entries -PreviousEnglish $(if ($previousEnglish) { $previousEnglish.Entries } else { $null }) -ExistingLocale $(if ($existingLocale) { $existingLocale.Entries } else { $null })
                    $comments = if ($existingLocale -and $existingLocale.Comments.Count -gt 0) { $existingLocale.Comments } else { $currentEnglish.Comments }
                    Write-LanguageXml -FilePath $localePath -Entries $mergedEntries -Comments $comments
                }
            }
            'Names' {
                $previousEnglish = Read-NamesFile -FilePath $englishPath
                $currentEnglishLines = Read-NamesFile -FilePath $output.SourcePath
                Write-NamesFile -FilePath $englishPath -Lines $currentEnglishLines
                foreach ($language in $ResolvedLanguages) {
                    if ($language -eq 'English') {
                        continue
                    }
                    $localePath = Join-Path $outputRoot (Join-Path "Languages\$language" $output.OutputRelPath)
                    $existingLocale = Read-NamesFile -FilePath $localePath
                    $mergedLines = Merge-NameLines -CurrentEnglish $currentEnglishLines -PreviousEnglish $previousEnglish -ExistingLocale $existingLocale
                    Write-NamesFile -FilePath $localePath -Lines $mergedLines
                }
            }
            'DefInjected' {
                $previousEnglish = Read-LanguageXml -FilePath $englishPath
                $currentEnglishEntries = Get-OrderedEntriesFromPairs -Pairs $output.Entries
                $comments = @()
                Write-LanguageXml -FilePath $englishPath -Entries $currentEnglishEntries -Comments $comments
                foreach ($language in $ResolvedLanguages) {
                    if ($language -eq 'English') {
                        continue
                    }
                    $localePath = Join-Path $outputRoot (Join-Path "Languages\$language" $output.OutputRelPath)
                    $existingLocale = Read-LanguageXml -FilePath $localePath
                    $mergedEntries = Merge-XmlEntries -CurrentEnglish $currentEnglishEntries -PreviousEnglish $(if ($previousEnglish) { $previousEnglish.Entries } else { $null }) -ExistingLocale $(if ($existingLocale) { $existingLocale.Entries } else { $null })
                    $localeComments = if ($existingLocale) { $existingLocale.Comments } else { @() }
                    Write-LanguageXml -FilePath $localePath -Entries $mergedEntries -Comments $localeComments
                }
            }
            default {
                throw "Unhandled output kind: $($output.Kind)"
            }
        }
    }

    Remove-EmptyDirectories -RootPath (Join-Path $outputRoot 'Languages')

    $reportPath = Join-Path $outputRoot 'Reports\last-sync-report.txt'
    $summaryLines = @(
        "Category: $($CategoryConfig.id)",
        "Source mods: $($categorySources.Mods.Count)",
        "Output files: $($outputs.Count)",
        "Languages: $($ResolvedLanguages -join ', ')"
    ) + @($report)
    Save-TextFile -FilePath $reportPath -Content (($summaryLines -join [Environment]::NewLine) + [Environment]::NewLine)
}

$config = Load-Config -FilePath $ConfigPath
$categoryMap = Get-CategoryMap -Config $config
if ($PSBoundParameters.ContainsKey('Category') -and -not [string]::IsNullOrWhiteSpace($Category)) {
    $Category = Resolve-CategoryAlias -CategoryName $Category
}
Import-TranslatableMembersFromRimWorldSource -RimWorldRoot $config.paths.RimWorldRoot
$resolvedLanguages = Get-LanguageList -Config $config -RequestedLanguages $Languages
$state = Load-State -Config $config

switch ($Command) {
    'refresh-lists' {
        Refresh-ModLists -Config $config -UpdateFipSource:$RefreshFipSource -UpdateFcpSources:$RefreshFcpSources
    }
    'sync' {
        if ([string]::IsNullOrWhiteSpace($Category)) {
            throw 'Category is required when Command=sync.'
        }
        if ($RefreshLists) {
            Refresh-ModLists -Config $config -UpdateFipSource:$RefreshFipSource -UpdateFcpSources:$RefreshFcpSources
        }
        $allSources = @{}
        $allSources[$Category] = Get-CategorySources -Config $config -CategoryConfig $categoryMap[$Category] -State $state
        foreach ($otherCategoryId in $categoryMap.Keys) {
            if ($otherCategoryId -eq $Category) {
                continue
            }
            $allSources[$otherCategoryId] = [pscustomobject]@{ Mods = @(); MissingMods = @() }
        }
        Sync-Category -Config $config -CategoryConfig $categoryMap[$Category] -ResolvedLanguages $resolvedLanguages -State $state -AllCategorySources $allSources
        Save-State -State $state
    }
    'sync-all' {
        if ($RefreshLists) {
            Refresh-ModLists -Config $config -UpdateFipSource:$RefreshFipSource -UpdateFcpSources:$RefreshFcpSources
        }
        $allSources = @{}
        foreach ($categoryId in ($categoryMap.Keys | Sort-Object)) {
            $allSources[$categoryId] = Get-CategorySources -Config $config -CategoryConfig $categoryMap[$categoryId] -State $state
        }
        foreach ($categoryId in ($categoryMap.Keys | Sort-Object)) {
            Write-Host "Syncing category $categoryId"
            Sync-Category -Config $config -CategoryConfig $categoryMap[$categoryId] -ResolvedLanguages $resolvedLanguages -State $state -AllCategorySources $allSources
        }
        Save-State -State $state
    }
}
