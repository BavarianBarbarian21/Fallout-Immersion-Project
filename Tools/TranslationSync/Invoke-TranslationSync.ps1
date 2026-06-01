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

    [string]$ConfigPath,

    [string]$PlaysetModsRoot,

    [string]$PreviousEnglishRoot,

    [string[]]$Languages,

    [string[]]$IncludeMods,

    [switch]$RefreshLists,

    [switch]$RefreshFipSource,

    [switch]$RefreshFcpSources,

    [switch]$SkipEnglishBaseline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

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

$Languages = @(Normalize-ListArgument -Values $Languages)
$IncludeMods = @(Normalize-ListArgument -Values $IncludeMods)

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $scriptRoot = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
        $PSScriptRoot
    }
    else {
        Split-Path -Parent $MyInvocation.MyCommand.Path
    }

    $ConfigPath = Join-Path $scriptRoot 'config.json'
}

$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
[void][System.Reflection.Assembly]::LoadWithPartialName('System.Web.Extensions')
$JsonSerializer = [System.Web.Script.Serialization.JavaScriptSerializer]::new()
$JsonSerializer.MaxJsonLength = [int]::MaxValue

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

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return
    }

    $null = [System.IO.Directory]::CreateDirectory((Get-LongPath -Path $Path))
}

function Get-LongPath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $Path
    }

    if ($Path.StartsWith('\\?\')) {
        return $Path
    }

    if ($Path.StartsWith('\\')) {
        return '\\?\UNC\' + $Path.TrimStart('\\')
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return '\\?\' + [System.IO.Path]::GetFullPath($Path)
    }

    return $Path
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

    $resolvedPath = Get-LongPath -Path $FilePath
    if (-not [System.IO.File]::Exists($resolvedPath)) {
        return $null
    }

    $raw = [System.IO.File]::ReadAllText($resolvedPath, $Utf8NoBom)
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $null
    }

    return $JsonSerializer.DeserializeObject($raw)
}

function Resolve-PlaysetModsRoot {
    param(
        [string]$ConfigRoot,
        [string]$ConfiguredPath
    )

    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        return Get-AbsolutePath -BasePath $ConfigRoot -Path $ConfiguredPath
    }

    $sharedConfigPath = Get-AbsolutePath -BasePath $ConfigRoot -Path '../../Tools/TranslationSync/config.json'
    if (-not (Test-Path -LiteralPath $sharedConfigPath)) {
        return $null
    }

    $sharedConfig = Load-JsonFile -FilePath $sharedConfigPath
    if ($null -eq $sharedConfig -or -not $sharedConfig.ContainsKey('paths') -or $null -eq $sharedConfig.paths) {
        return $null
    }

    $sharedPlaysetPath = [string]$sharedConfig.paths.playsetModsRoot
    if ([string]::IsNullOrWhiteSpace($sharedPlaysetPath)) {
        return $null
    }

    return Get-AbsolutePath -BasePath (Split-Path -Parent $sharedConfigPath) -Path $sharedPlaysetPath
}

function Save-JsonFile {
    param(
        [string]$FilePath,
        [object]$Data,
        [int]$Depth = 100
    )

    Ensure-Directory -Path (Split-Path -Parent $FilePath)
    $json = $Data | ConvertTo-Json -Depth $Depth
    [System.IO.File]::WriteAllText((Get-LongPath -Path $FilePath), $json, $Utf8NoBom)
}

function Save-TextFile {
    param(
        [string]$FilePath,
        [string]$Content
    )

    Ensure-Directory -Path (Split-Path -Parent $FilePath)
    [System.IO.File]::WriteAllText((Get-LongPath -Path $FilePath), $Content, $Utf8NoBom)
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
    $paths.LocaleRulesPath = Get-AbsolutePath -BasePath $configRoot -Path $config.paths.localeRulesPath
    $paths.PlaysetModsRoot = Resolve-PlaysetModsRoot -ConfigRoot $configRoot -ConfiguredPath $(if ($PlaysetModsRoot) { $PlaysetModsRoot } else { [string]$config.paths.playsetModsRoot })
    $paths.StateRoot = Get-AbsolutePath -BasePath $configRoot -Path $config.paths.stateRoot
    $paths.ModListRoot = Get-AbsolutePath -BasePath $configRoot -Path $config.paths.modListRoot
    $config.paths = $paths

    foreach ($categoryEntry in $config.categories) {
        $categoryEntry.outputModPath = Get-AbsolutePath -BasePath $configRoot -Path $categoryEntry.outputModPath
        $categoryEntry.modListPath = Get-AbsolutePath -BasePath $configRoot -Path $categoryEntry.modListPath
    }

    if (-not $config.ContainsKey('translation') -or $null -eq $config.translation) {
        $config['translation'] = @{}
    }
    if (-not $config.translation.ContainsKey('provider') -or [string]::IsNullOrWhiteSpace([string]$config.translation.provider)) {
        $config.translation['provider'] = 'google-gtx'
    }
    if (-not $config.translation.ContainsKey('sourceLanguageCode') -or [string]::IsNullOrWhiteSpace([string]$config.translation.sourceLanguageCode)) {
        $config.translation['sourceLanguageCode'] = 'en'
    }
    if (-not $config.translation.ContainsKey('requestDelayMs')) {
        $config.translation['requestDelayMs'] = 0
    }
    if (-not $config.translation.ContainsKey('timeoutSeconds')) {
        $config.translation['timeoutSeconds'] = 30
    }

    return $config
}

function New-EmptyLocaleRules {
    return @{
        global = @{
            literalReplacements = @()
        }
        languages = @{}
    }
}

function Load-LocaleRules {
    param([hashtable]$Config)

    $rulesPath = $Config.paths.LocaleRulesPath
    if ([string]::IsNullOrWhiteSpace($rulesPath) -or -not (Test-Path -LiteralPath $rulesPath)) {
        return New-EmptyLocaleRules
    }

    $rules = Load-JsonFile -FilePath $rulesPath
    if ($null -eq $rules) {
        return New-EmptyLocaleRules
    }

    if (-not $rules.ContainsKey('global')) {
        $rules['global'] = @{ literalReplacements = @() }
    }
    elseif (-not $rules.global.ContainsKey('literalReplacements')) {
        $rules.global.literalReplacements = @()
    }

    if (-not $rules.ContainsKey('languages')) {
        $rules['languages'] = @{}
    }

    return $rules
}

function Get-LanguageLiteralReplacements {
    param(
        [string]$Language,
        [hashtable]$LocaleRules
    )

    if ($null -eq $LocaleRules) {
        return @()
    }

    $all = New-Object 'System.Collections.Generic.List[object]'
    foreach ($replacement in @($LocaleRules.global.literalReplacements)) {
        $all.Add($replacement) | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($Language) -and $LocaleRules.languages.ContainsKey($Language)) {
        $languageConfig = $LocaleRules.languages[$Language]
        if ($languageConfig.ContainsKey('literalReplacements')) {
            foreach ($replacement in @($languageConfig.literalReplacements)) {
                $all.Add($replacement) | Out-Null
            }
        }
    }

    return @($all.ToArray() | Sort-Object -Property @{ Expression = {
        if ($_.ContainsKey('from') -and -not [string]::IsNullOrWhiteSpace([string]$_.from)) {
            ([string]$_.from).Length
        }
        else {
            0
        }
    } } -Descending)
}

function Apply-LanguageRulesToText {
    param(
        [string]$Text,
        [string]$Language,
        [hashtable]$LocaleRules
    )

    if ([string]::IsNullOrEmpty($Text) -or [string]::IsNullOrWhiteSpace($Language) -or $Language -eq 'English') {
        return $Text
    }

    $result = $Text
    foreach ($replacement in @(Get-LanguageLiteralReplacements -Language $Language -LocaleRules $LocaleRules)) {
        $from = [string]$replacement.from
        if ([string]::IsNullOrWhiteSpace($from)) {
            continue
        }

        $to = if ($replacement.ContainsKey('to') -and $null -ne $replacement.to) {
            [string]$replacement.to
        }
        else {
            ''
        }

        $ignoreCase = $true
        if ($replacement.ContainsKey('ignoreCase')) {
            $ignoreCase = [bool]$replacement.ignoreCase
        }

        $options = [System.Text.RegularExpressions.RegexOptions]::None
        if ($ignoreCase) {
            $options = $options -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
        }

        $escapedPattern = [regex]::Escape($from)
        $result = [regex]::Replace(
            $result,
            $escapedPattern,
            [System.Text.RegularExpressions.MatchEvaluator]{
                param($match)
                $to
            },
            $options
        )
    }

    return $result
}

function Get-LanguageTranslationCode {
    param([string]$Language)

    switch ($Language) {
        'Catalan' { return 'ca' }
        'French' { return 'fr' }
        'German' { return 'de' }
        'Polish' { return 'pl' }
        'Russian' { return 'ru' }
        'Italian' { return 'it' }
        'Spanish' { return 'es' }
        'Czech' { return 'cs' }
        'Danish' { return 'da' }
        'Dutch' { return 'nl' }
        'Estonian' { return 'et' }
        'Greek' { return 'el' }
        'Hungarian' { return 'hu' }
        'Japanese' { return 'ja' }
        'Norwegian' { return 'no' }
        'Portuguese' { return 'pt' }
        'PortugueseBrazilian' { return 'pt-BR' }
        'ChineseSimplified' { return 'zh-CN' }
        'Slovak' { return 'sk' }
        'Swedish' { return 'sv' }
        'ChineseTraditional' { return 'zh-TW' }
        'Turkish' { return 'tr' }
        'Ukrainian' { return 'uk' }
        'Vietnamese' { return 'vi' }
        'Finnish' { return 'fi' }
        'Korean' { return 'ko' }
        'Romanian' { return 'ro' }
        'SpanishLatin' { return 'es-419' }
        default { return $null }
    }
}

function Get-TranslationCacheKey {
    param(
        [string]$Language,
        [string]$Text
    )

    $provider = if ($script:TranslationConfig -and $script:TranslationConfig.ContainsKey('provider')) {
        [string]$script:TranslationConfig.provider
    }
    else {
        'none'
    }

    $profile = ''
    if ($script:TranslationState -and $script:TranslationState.Data.translation.ContainsKey('profile')) {
        $profile = [string]$script:TranslationState.Data.translation.profile
    }

    $payload = '{0}`n{1}`n{2}`n{3}`n{4}' -f 'v7', $provider, $profile, $Language, $Text
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hash = $sha256.ComputeHash($bytes)
    }
    finally {
        $sha256.Dispose()
    }

    return ([System.BitConverter]::ToString($hash)).Replace('-', '')
}

function Protect-TextForTranslation {
    param(
        [string]$Text,
        [string]$Language,
        [hashtable]$LocaleRules
    )

    $protectedText = $Text
    $tokens = New-Object 'System.Collections.Generic.List[object]'
    $tokenIndex = 0

    foreach ($replacement in @(Get-LanguageLiteralReplacements -Language $Language -LocaleRules $LocaleRules)) {
        $from = [string]$replacement.from
        if ([string]::IsNullOrWhiteSpace($from)) {
            continue
        }

        $to = if ($replacement.ContainsKey('to') -and $null -ne $replacement.to) {
            [string]$replacement.to
        }
        else {
            ''
        }

        $ignoreCase = $true
        if ($replacement.ContainsKey('ignoreCase')) {
            $ignoreCase = [bool]$replacement.ignoreCase
        }

        $options = [System.Text.RegularExpressions.RegexOptions]::None
        if ($ignoreCase) {
            $options = $options -bor [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
        }

        $escapedPattern = [regex]::Escape($from)
        $protectedText = [regex]::Replace(
            $protectedText,
            $escapedPattern,
            [System.Text.RegularExpressions.MatchEvaluator]{
                param($match)
                $token = '__FIP_TERM_{0}__' -f $tokenIndex
                $tokens.Add([pscustomobject]@{ Token = $token; Value = $to }) | Out-Null
                $tokenIndex++
                return $token
            },
            $options
        )
    }

    $provider = if ($script:TranslationConfig -and $script:TranslationConfig.ContainsKey('provider')) {
        [string]$script:TranslationConfig.provider
    }
    else {
        'none'
    }

    if ($provider -eq 'google-gtx') {
        return [pscustomobject]@{
            Text   = $protectedText
            Tokens = $tokens.ToArray()
        }
    }

    $placeholderPattern = '\r\n|\n|\r|\t|\[[^\]]+\]|\{[^{}\r\n]+\}'
    $protectedText = [regex]::Replace(
        $protectedText,
        $placeholderPattern,
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)
            $token = '__FIP_TOKEN_{0}__' -f $tokenIndex
            $tokens.Add([pscustomobject]@{ Token = $token; Value = $match.Value }) | Out-Null
            $tokenIndex++
            return $token
        }
    )

    return [pscustomobject]@{
        Text   = $protectedText
        Tokens = $tokens.ToArray()
    }
}

function Restore-TextAfterTranslation {
    param(
        [string]$Text,
        [object[]]$Tokens
    )

    $restored = $Text
    foreach ($token in @($Tokens | Sort-Object { ([string]$_.Token).Length } -Descending)) {
        $restored = $restored.Replace([string]$token.Token, [string]$token.Value)
    }
    return $restored
}

function Normalize-TranslatedProtectionTokens {
    param([string]$Text)

    return [regex]::Replace(
        $Text,
        '(?:__\s*)?FIP(?:\s*_|\s+)?(?<kind>TOKEN|TERM)(?:\s*_|\s+)?(?<index>\d+)(?:\s*__)?',
        [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)
            return '__FIP_{0}_{1}__' -f $match.Groups['kind'].Value.ToUpperInvariant(), $match.Groups['index'].Value
        },
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )
}

function Restore-SourcePlaceholders {
    param(
        [string]$SourceText,
        [string]$TranslatedText
    )

    if ([string]::IsNullOrEmpty($SourceText) -or [string]::IsNullOrEmpty($TranslatedText)) {
        return $TranslatedText
    }

    $placeholderPattern = '\[[^\]]+\]|\{[^{}\r\n]+\}'
    $sourceMatches = [regex]::Matches($SourceText, $placeholderPattern)
    if ($sourceMatches.Count -eq 0) {
        return $TranslatedText
    }

    $translatedMatches = [regex]::Matches($TranslatedText, $placeholderPattern)
    if ($translatedMatches.Count -ne $sourceMatches.Count) {
        return $TranslatedText
    }

    $builder = [System.Text.StringBuilder]::new()
    $position = 0
    for ($index = 0; $index -lt $translatedMatches.Count; $index++) {
        $match = $translatedMatches[$index]
        [void]$builder.Append($TranslatedText.Substring($position, $match.Index - $position))
        [void]$builder.Append($sourceMatches[$index].Value)
        $position = $match.Index + $match.Length
    }

    [void]$builder.Append($TranslatedText.Substring($position))
    return $builder.ToString()
}

function Split-TextForTranslation {
    param(
        [string]$Text,
        [int]$MaxLength = 400
    )

    if ([string]::IsNullOrEmpty($Text) -or $Text.Length -le $MaxLength) {
        return @($Text)
    }

    $chunks = New-Object 'System.Collections.Generic.List[string]'
    $remaining = $Text.Trim()
    while ($remaining.Length -gt $MaxLength) {
        $splitIndex = $remaining.LastIndexOf(' ', $MaxLength)
        if ($splitIndex -lt 0 -or $splitIndex -lt [Math]::Floor($MaxLength / 2)) {
            $splitIndex = $MaxLength
        }

        $chunk = $remaining.Substring(0, $splitIndex).Trim()
        if (-not [string]::IsNullOrWhiteSpace($chunk)) {
            $chunks.Add($chunk) | Out-Null
        }
        $remaining = $remaining.Substring($splitIndex).TrimStart()
    }

    if (-not [string]::IsNullOrWhiteSpace($remaining)) {
        $chunks.Add($remaining) | Out-Null
    }

    return $chunks.ToArray()
}

function Test-LocaleValueNeedsRefresh {
    param(
        [string]$CurrentEnglish,
        [string]$ExistingLocale
    )

    if ($script:TranslationForceRefreshAllLocales) {
        return $true
    }

    if ([string]::IsNullOrWhiteSpace($ExistingLocale)) {
        return $true
    }
    if ($ExistingLocale -eq $CurrentEnglish) {
        return $true
    }
    if ($ExistingLocale -match 'QUERY LENGTH LIMIT EXCEEDED') {
        return $true
    }
    if ($ExistingLocale -match 'FIP\s*(?:TOKEN|TERM)\s*\d+\s*X') {
        return $true
    }

    $placeholderPattern = '\[[^\]]+\]|\{[^{}\r\n]+\}'
    foreach ($match in [regex]::Matches($CurrentEnglish, $placeholderPattern)) {
        if (-not $ExistingLocale.Contains($match.Value)) {
            return $true
        }
    }

    return $false
}

function Split-StructuredTranslationText {
    param([string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $null
    }

    foreach ($separator in @('&gt;', '->')) {
        $index = $Text.IndexOf($separator, [System.StringComparison]::Ordinal)
        if ($index -le 0) {
            continue
        }

        $prefix = $Text.Substring(0, $index + $separator.Length)
        if ($prefix.Contains(' ')) {
            continue
        }

        $suffix = $Text.Substring($index + $separator.Length)
        if ([string]::IsNullOrWhiteSpace($suffix)) {
            continue
        }

        if ($suffix -match '[A-Za-z]') {
            return [pscustomobject]@{
                Prefix = $prefix
                Suffix = $suffix
            }
        }
    }

    return $null
}

function Invoke-ConfiguredTranslation {
    param(
        [string]$Text,
        [string]$Language,
        [hashtable]$LocaleRules
    )

    if ([string]::IsNullOrEmpty($Text) -or [string]::IsNullOrWhiteSpace($Language) -or $Language -eq 'English') {
        return $Text
    }

    $structuredText = Split-StructuredTranslationText -Text $Text
    if ($null -ne $structuredText) {
        return $structuredText.Prefix + (Invoke-ConfiguredTranslation -Text $structuredText.Suffix -Language $Language -LocaleRules $LocaleRules)
    }

    $provider = if ($script:TranslationConfig -and $script:TranslationConfig.ContainsKey('provider')) {
        [string]$script:TranslationConfig.provider
    }
    else {
        'none'
    }

    if ([string]::IsNullOrWhiteSpace($provider) -or $provider -eq 'none') {
        return Apply-LanguageRulesToText -Text $Text -Language $Language -LocaleRules $LocaleRules
    }

    $targetCode = Get-LanguageTranslationCode -Language $Language
    if ([string]::IsNullOrWhiteSpace($targetCode)) {
        return Apply-LanguageRulesToText -Text $Text -Language $Language -LocaleRules $LocaleRules
    }

    $cacheKey = Get-TranslationCacheKey -Language $Language -Text $Text
    $translationCache = $script:TranslationState.Data.translationCache
    if ($translationCache.ContainsKey($cacheKey)) {
        return [string]$translationCache[$cacheKey].translated
    }

    $prepared = Protect-TextForTranslation -Text $Text -Language $Language -LocaleRules $LocaleRules
    if ([string]::IsNullOrWhiteSpace($prepared.Text)) {
        return Restore-TextAfterTranslation -Text $prepared.Text -Tokens $prepared.Tokens
    }

    $translated = $prepared.Text
    try {
        switch ($provider) {
            'google-gtx' {
                [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
                $sourceCode = [string]$script:TranslationConfig.sourceLanguageCode
                $timeoutSeconds = [int]$script:TranslationConfig.timeoutSeconds
                $translatedChunks = New-Object 'System.Collections.Generic.List[string]'
                foreach ($chunk in @(Split-TextForTranslation -Text $prepared.Text -MaxLength 3000)) {
                    $uri = 'https://translate.googleapis.com/translate_a/single?client=gtx&sl=' + [System.Uri]::EscapeDataString($sourceCode) + '&tl=' + [System.Uri]::EscapeDataString($targetCode) + '&dt=t&q=' + [System.Uri]::EscapeDataString($chunk)
                    $response = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec $timeoutSeconds
                    $translatedChunk = $chunk
                    if ($null -ne $response -and $response.Count -gt 0 -and $null -ne $response[0]) {
                        $builder = [System.Text.StringBuilder]::new()
                        foreach ($segment in @($response[0])) {
                            if ($segment -is [System.Array] -and $segment.Length -gt 0 -and $null -ne $segment[0]) {
                                [void]$builder.Append([string]$segment[0])
                            }
                        }

                        if ($builder.Length -gt 0) {
                            $translatedChunk = $builder.ToString()
                        }
                    }
                    $translatedChunks.Add($translatedChunk) | Out-Null
                }

                if ($translatedChunks.Count -gt 0) {
                    $translated = $translatedChunks -join ' '
                }
            }
            'mymemory' {
                [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
                $sourceCode = [string]$script:TranslationConfig.sourceLanguageCode
                $timeoutSeconds = [int]$script:TranslationConfig.timeoutSeconds
                $translatedChunks = New-Object 'System.Collections.Generic.List[string]'
                foreach ($chunk in @(Split-TextForTranslation -Text $prepared.Text -MaxLength 400)) {
                    $uri = 'https://api.mymemory.translated.net/get?q=' + [System.Uri]::EscapeDataString($chunk) + '&langpair=' + [System.Uri]::EscapeDataString($sourceCode) + '|' + [System.Uri]::EscapeDataString($targetCode)
                    $response = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec $timeoutSeconds
                    $translatedChunk = $chunk
                    if ($null -ne $response -and $response.PSObject.Properties.Match('responseData').Count -gt 0 -and $null -ne $response.responseData -and $response.responseData.PSObject.Properties.Match('translatedText').Count -gt 0) {
                        $translatedChunk = [string]$response.responseData.translatedText
                    }
                    $translatedChunks.Add($translatedChunk) | Out-Null
                }

                if ($translatedChunks.Count -gt 0) {
                    $translated = $translatedChunks -join ' '
                }
            }
            default {
                throw "Unknown translation provider '$provider'."
            }
        }
    }
    catch {
        $translated = $prepared.Text
    }

    $translated = [System.Web.HttpUtility]::HtmlDecode($translated)
    $translated = Normalize-TranslatedProtectionTokens -Text $translated
    $translated = Restore-TextAfterTranslation -Text $translated -Tokens $prepared.Tokens
    $translated = Restore-SourcePlaceholders -SourceText $Text -TranslatedText $translated
    $translated = Apply-LanguageRulesToText -Text $translated -Language $Language -LocaleRules $LocaleRules

    $translationCache[$cacheKey] = @{
        language   = $Language
        source     = $Text
        translated = $translated
    }

    $delayMs = [int]$script:TranslationConfig.requestDelayMs
    if ($delayMs -gt 0) {
        [System.Threading.Thread]::Sleep($delayMs)
    }

    return $translated
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
        return $selected.ToArray()
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
        $subResults = @(Get-TranslatablePairs -Element $child -PathPrefix $childPath -IsStringListParent $childIsStringList)
        foreach ($subResult in $subResults) {
            $results.Add($subResult) | Out-Null
        }
    }

    return $results.ToArray()
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
    if ($null -eq $doc -or $null -eq $doc.DocumentElement -or $doc.DocumentElement.Name -ne 'Defs') {
        return @()
    }

    $defsRoot = $doc.DocumentElement
    if ($defsRoot -isnot [System.Xml.XmlElement]) {
        Write-Warning "Skipping unsupported defs root in $FilePath"
        return @()
    }

    try {
        $defNodes = @($defsRoot.ChildNodes)
    }
    catch {
        Write-Warning "Skipping unsupported defs structure in $FilePath"
        return @()
    }

    $byType = @{}
    foreach ($defNode in $defNodes) {
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

        $pairs = @(Get-TranslatablePairs -Element $defNode -PathPrefix $defNameNode.InnerText.Trim())
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

    return $groups.ToArray()
}

function Read-LanguageXml {
    param([string]$FilePath)

    $resolvedPath = Get-LongPath -Path $FilePath
    if (-not [System.IO.File]::Exists($resolvedPath)) {
        return $null
    }

    $document = [System.Xml.XmlDocument]::new()
    $document.PreserveWhitespace = $true
    try {
        $bytes = [System.IO.File]::ReadAllBytes($resolvedPath)
        $strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)

        try {
            $content = $strictUtf8.GetString($bytes)
        }
        catch {
            # Some upstream language files are saved as Windows-1252 despite declaring UTF-8.
            $content = [System.Text.Encoding]::GetEncoding(1252).GetString($bytes)
        }

        if ($content.Length -gt 0 -and $content[0] -eq [char]0xFEFF) {
            $content = $content.Substring(1)
        }

        $document.LoadXml($content)
    }
    catch {
        return $null
    }

    if ($null -eq $document.DocumentElement -or $document.DocumentElement.Name -notin @('LanguageData', 'LanguagData')) {
        return $null
    }

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
        Comments = $comments.ToArray()
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

    $resolvedPath = Get-LongPath -Path $FilePath
    if (-not [System.IO.File]::Exists($resolvedPath)) {
        return $null
    }

    return @([System.IO.File]::ReadAllLines($resolvedPath, [System.Text.Encoding]::UTF8))
}

function Write-NamesFile {
    param(
        [string]$FilePath,
        [string[]]$Lines
    )

    Ensure-Directory -Path (Split-Path -Parent $FilePath)
    [System.IO.File]::WriteAllLines((Get-LongPath -Path $FilePath), $Lines, $Utf8NoBom)
}

function Get-OrderedEntriesFromPairs {
    param([object[]]$Pairs)

    $ordered = New-Object 'System.Collections.Specialized.OrderedDictionary'
    foreach ($pair in $Pairs) {
        $ordered[$pair.Key] = $pair.Text
    }
    return $ordered
}

function Convert-OrderedEntriesToPairs {
    param([System.Collections.Specialized.OrderedDictionary]$Entries)

    $pairs = New-Object 'System.Collections.Generic.List[object]'
    if ($null -eq $Entries) {
        return $pairs.ToArray()
    }

    foreach ($key in $Entries.Keys) {
        $pairs.Add([pscustomobject]@{
            Key  = [string]$key
            Text = [string]$Entries[$key]
        }) | Out-Null
    }

    return $pairs.ToArray()
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
    if (-not $state.ContainsKey('translationCache')) {
        $state.translationCache = @{}
    }
    if (-not $state.ContainsKey('translation')) {
        $state.translation = @{}
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
        PackageId = $null
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
            PackageId = if ([string]::IsNullOrWhiteSpace($packageId)) { $null } else { [string]$packageId }
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
            if ([string]::IsNullOrWhiteSpace($meta.PackageId)) {
                continue
            }

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
    param(
        [string]$FilePath,
        [switch]$LoadAfterOnly
    )

    $packageIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    try {
        [xml]$about = [System.IO.File]::ReadAllText($FilePath, [System.Text.Encoding]::UTF8)
    }
    catch {
        return $packageIds
    }

    $nodePaths = if ($LoadAfterOnly) {
        @('/ModMetaData/loadAfter/li')
    }
    else {
        @(
            '/ModMetaData/modDependencies/li/packageId',
            '/ModMetaData/modDependenciesByVersion/li/packageId',
            '/ModMetaData/loadAfter/li'
        )
    }

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
            $aboutIds = Get-RequiredPackageIdsFromAboutFile -FilePath $aboutPath -LoadAfterOnly
            foreach ($packageId in $aboutIds) {
                if (-not (Test-IsIgnoredPackageId -PackageId $packageId -Config $Config)) {
                    [void]$referencedPackageIds.Add($packageId)
                }
            }
        }
    }

    $lookupRoots = @($Config.paths.PlaysetModsRoot)
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

    $categoryMap = Get-CategoryMap -Config $Config
    $excludedNames = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    $excludedPackageIds = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($categoryId in @('fip', 'fcp', 'compatible')) {
        $sourceCategory = $categoryMap[$categoryId]
        $searchRoots = Get-CategorySearchRoots -Config $Config -CategoryConfig $sourceCategory
        foreach ($modName in Get-ModNamesFromList -FilePath $sourceCategory.modListPath) {
            if ([string]::IsNullOrWhiteSpace($modName)) {
                continue
            }

            [void]$excludedNames.Add($modName)
            $modRoot = Resolve-ModRoot -ModName $modName -SearchRoots $searchRoots
            if ($null -eq $modRoot) {
                continue
            }

            $meta = Get-AboutMetadata -ModRoot $modRoot
            if (-not [string]::IsNullOrWhiteSpace($meta.PackageId)) {
                [void]$excludedPackageIds.Add($meta.PackageId)
            }
        }
    }

    $remaining = @(Get-ChildItem -LiteralPath $Config.paths.PlaysetModsRoot -Directory | Where-Object {
        if ($excludedNames.Contains($_.Name)) {
            return $false
        }

        if (-not (Test-Path -LiteralPath (Join-Path $_.FullName 'About\About.xml'))) {
            return $false
        }

        $meta = Get-AboutMetadata -ModRoot $_.FullName
        if ([string]::IsNullOrWhiteSpace($meta.PackageId)) {
            return $true
        }

        if (Test-IsIgnoredPackageId -PackageId $meta.PackageId -Config $Config) {
            return $false
        }

        return -not $excludedPackageIds.Contains($meta.PackageId)
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
            if (-not [string]::IsNullOrWhiteSpace($Config.paths.PlaysetModsRoot)) {
                $roots.Add($Config.paths.PlaysetModsRoot) | Out-Null
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

function Get-SourceLanguageFiles {
    param(
        [string]$ModRoot,
        [string]$RelativePattern,
        [string]$Filter
    )

    $languageRootMarker = '\Languages\English\'
    return @(Get-ChildItem -LiteralPath $ModRoot -File -Recurse -Filter $Filter -ErrorAction SilentlyContinue | Where-Object {
        $fullName = $_.FullName
        $markerIndex = $fullName.IndexOf($languageRootMarker, [System.StringComparison]::OrdinalIgnoreCase)
        if ($markerIndex -lt 0) {
            return $false
        }

        $languageRelativePath = $fullName.Substring($markerIndex + $languageRootMarker.Length)
        return $languageRelativePath -like $RelativePattern
    } | Sort-Object FullName)
}

function Add-KeyedOutputsFromMod {
    param(
        [string]$ModRoot,
        [string]$OutputStem,
        [System.Collections.Generic.List[object]]$Outputs
    )

    foreach ($file in Get-SourceLanguageFiles -ModRoot $ModRoot -RelativePattern 'Keyed\*.xml' -Filter '*.xml') {
        $outputs.Add([pscustomobject]@{
            Kind          = 'Keyed'
            OutputRelPath = ('Keyed/{0}__{1}' -f $OutputStem, $file.Name).Replace('/', '\')
            SourcePath    = $file.FullName
        }) | Out-Null
    }
}

function Add-NamesOutputsFromMod {
    param(
        [string]$ModRoot,
        [string]$OutputStem,
        [System.Collections.Generic.List[object]]$Outputs
    )

    foreach ($file in Get-SourceLanguageFiles -ModRoot $ModRoot -RelativePattern 'Strings\Names\*.txt' -Filter '*.txt') {
        $outputs.Add([pscustomobject]@{
            Kind          = 'Names'
            OutputRelPath = ('Strings/Names/{0}__{1}' -f $OutputStem, $file.Name).Replace('/', '\')
            SourcePath    = $file.FullName
        }) | Out-Null
    }
}

function Add-ExistingDefInjectedOutputsFromMod {
    param(
        [string]$ModRoot,
        [System.Collections.Generic.List[object]]$Outputs
    )

    foreach ($file in Get-SourceLanguageFiles -ModRoot $ModRoot -RelativePattern 'DefInjected\*\*.xml' -Filter '*.xml') {
        $englishRoot = $file.Directory.Parent.Parent.FullName
        $relativePath = Get-RelativePathFragment -BasePath $englishRoot -FullPath $file.FullName
        $relativePath = $relativePath.Replace('/', '\')
        $defType = Split-Path -Leaf (Split-Path -Parent $relativePath)
        if ([string]::IsNullOrWhiteSpace($defType)) {
            continue
        }

        $parsed = Read-LanguageXml -FilePath $file.FullName
        if ($null -eq $parsed -or $parsed.Entries.Count -eq 0) {
            continue
        }

        $outputs.Add([pscustomobject]@{
            Kind          = 'DefInjected'
            OutputRelPath = ('DefInjected/{0}/FIP-Translation_{0}.xml' -f $defType).Replace('/', '\')
            Entries       = @(Convert-OrderedEntriesToPairs -Entries $parsed.Entries)
        }) | Out-Null
    }
}

function Add-DefInjectedOutputsFromMod {
    param(
        [string]$ModRoot,
        [System.Collections.Generic.List[object]]$Outputs,
        [pscustomobject]$State
    )

    $defDirectories = @(Get-ChildItem -LiteralPath $ModRoot -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -eq 'Defs'
    })

    foreach ($defDir in $defDirectories) {
        foreach ($file in Get-ChildItem -LiteralPath $defDir.FullName -Filter '*.xml' -File -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName) {
            $stamp = Get-DirectoryStamp -FilePath $file.FullName
            $cacheRecord = $null
            if ($State.Data.defCache.ContainsKey($file.FullName)) {
                $candidate = $State.Data.defCache[$file.FullName]
                if ($candidate.stamp -eq $stamp) {
                    $cacheRecord = $candidate
                }
            }

            $groups = if ($null -ne $cacheRecord) {
                [object[]]$cacheRecord.groups
            }
            else {
                $parsedGroups = @(Extract-DefFile -FilePath $file.FullName)
                $serializedGroups = New-Object 'System.Collections.Generic.List[object]'
                foreach ($group in $parsedGroups) {
                    $serializedEntries = New-Object 'System.Collections.Generic.List[object]'
                    foreach ($entry in $group.Entries) {
                        $serializedEntries.Add(@{ Key = $entry.Key; Text = $entry.Text }) | Out-Null
                    }
                    $serializedGroups.Add(@{ DefType = $group.DefType; Entries = $serializedEntries.ToArray() }) | Out-Null
                }
                $State.Data.defCache[$file.FullName] = @{
                    stamp  = $stamp
                    groups = $serializedGroups.ToArray()
                }
                $serializedGroups.ToArray()
            }

            foreach ($group in $groups) {
                if ($group.Entries.Count -eq 0) {
                    continue
                }
                $outputs.Add([pscustomobject]@{
                    Kind          = 'DefInjected'
                    OutputRelPath = ('DefInjected/{0}/FIP-Translation_{0}.xml' -f $group.DefType).Replace('/', '\')
                    Entries       = @($group.Entries)
                }) | Out-Null
            }
        }
    }
}

function Merge-OutputCollection {
    param([System.Collections.Generic.List[object]]$Outputs)

    $mergedOutputs = New-Object 'System.Collections.Generic.List[object]'
    $mergedIndex = @{}

    foreach ($output in $Outputs) {
        $key = '{0}|{1}' -f $output.Kind, $output.OutputRelPath
        if (-not $mergedIndex.ContainsKey($key)) {
            if ($output.Kind -eq 'DefInjected') {
                $entryMap = New-Object 'System.Collections.Specialized.OrderedDictionary'
                foreach ($entry in @($output.Entries)) {
                    $entryMap[[string]$entry.Key] = [string]$entry.Text
                }

                $aggregate = [pscustomobject]@{
                    Kind          = $output.Kind
                    OutputRelPath = $output.OutputRelPath
                    EntryMap      = $entryMap
                }
                $mergedIndex[$key] = $aggregate
                $mergedOutputs.Add($aggregate) | Out-Null
                continue
            }

            $mergedIndex[$key] = $output
            $mergedOutputs.Add($output) | Out-Null
            continue
        }

        if ($output.Kind -eq 'DefInjected') {
            $aggregate = $mergedIndex[$key]
            foreach ($entry in @($output.Entries)) {
                $aggregate.EntryMap[[string]$entry.Key] = [string]$entry.Text
            }
        }
    }

    $finalOutputs = New-Object 'System.Collections.Generic.List[object]'
    foreach ($output in $mergedOutputs) {
        if ($output.Kind -eq 'DefInjected') {
            $finalOutputs.Add([pscustomobject]@{
                Kind          = $output.Kind
                OutputRelPath = $output.OutputRelPath
                Entries       = @(Convert-OrderedEntriesToPairs -Entries $output.EntryMap)
            }) | Out-Null
            continue
        }

        $finalOutputs.Add($output) | Out-Null
    }

    return $finalOutputs.ToArray()
}

function Get-CategorySources {
    param(
        [hashtable]$Config,
        [hashtable]$CategoryConfig,
        [pscustomobject]$State
    )

    $modNames = Get-ModNamesFromList -FilePath $CategoryConfig.modListPath
    if ($IncludeMods -and $IncludeMods.Count -gt 0) {
        $requestedMods = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
        foreach ($modName in $IncludeMods) {
            if (-not [string]::IsNullOrWhiteSpace($modName)) {
                [void]$requestedMods.Add($modName)
            }
        }
        $modNames = @($modNames | Where-Object { $requestedMods.Contains($_) })
    }
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
        $beforeKeyed = $outputs.Count
        Add-KeyedOutputsFromMod -ModRoot $modRoot -OutputStem $modName -Outputs $outputs
        $keyedCount = $outputs.Count - $beforeKeyed
        $beforeNames = $outputs.Count
        Add-NamesOutputsFromMod -ModRoot $modRoot -OutputStem $modName -Outputs $outputs
        $namesCount = $outputs.Count - $beforeNames
        $beforeExistingDefInjected = $outputs.Count
        Add-ExistingDefInjectedOutputsFromMod -ModRoot $modRoot -Outputs $outputs
        $existingDefInjectedCount = $outputs.Count - $beforeExistingDefInjected
        $beforeExtractedDefInjected = $outputs.Count
        Add-DefInjectedOutputsFromMod -ModRoot $modRoot -Outputs $outputs -State $State
        $extractedDefInjectedCount = $outputs.Count - $beforeExtractedDefInjected

        $uniqueOutputs = @(Merge-OutputCollection -Outputs $outputs)

        $resolvedMods.Add([pscustomobject]@{
            FolderName = $modName
            ModRoot    = $modRoot
            Name       = $metadata.Name
            PackageId  = $metadata.PackageId
            KeyedCount = $keyedCount
            NamesCount = $namesCount
            ExistingDefInjectedCount = $existingDefInjectedCount
            ExtractedDefInjectedCount = $extractedDefInjectedCount
            Outputs    = @($uniqueOutputs)
        }) | Out-Null
    }

    return [pscustomobject]@{
        Mods       = $resolvedMods.ToArray()
        MissingMods = $missingMods.ToArray()
    }
}

function Merge-XmlEntries {
    param(
        [System.Collections.Specialized.OrderedDictionary]$CurrentEnglish,
        [System.Collections.Specialized.OrderedDictionary]$PreviousEnglish,
        [System.Collections.Specialized.OrderedDictionary]$ExistingLocale,
        [string]$Language,
        [hashtable]$LocaleRules
    )

    $merged = New-Object 'System.Collections.Specialized.OrderedDictionary'
    foreach ($key in $CurrentEnglish.Keys) {
        $currentValue = [string]$CurrentEnglish[$key]
        $hasPrevious = $null -ne $PreviousEnglish -and $PreviousEnglish.Contains($key)
        $hasLocale = $null -ne $ExistingLocale -and $ExistingLocale.Contains($key)
        $existingLocaleValue = if ($hasLocale) { [string]$ExistingLocale[$key] } else { $null }
        $hasUsefulLocale = $hasLocale -and -not (Test-LocaleValueNeedsRefresh -CurrentEnglish $currentValue -ExistingLocale $existingLocaleValue)

        if ($hasPrevious -and $hasUsefulLocale -and [string]$PreviousEnglish[$key] -eq $currentValue) {
            $merged[$key] = $existingLocaleValue
        }
        else {
            $merged[$key] = Invoke-ConfiguredTranslation -Text $currentValue -Language $Language -LocaleRules $LocaleRules
        }
    }
    return $merged
}

function Merge-NameLines {
    param(
        [string[]]$CurrentEnglish,
        [string[]]$PreviousEnglish,
        [string[]]$ExistingLocale,
        [string]$Language,
        [hashtable]$LocaleRules
    )

    $merged = New-Object 'System.Collections.Generic.List[string]'
    for ($index = 0; $index -lt $CurrentEnglish.Count; $index++) {
        $currentValue = $CurrentEnglish[$index]
        $keepLocale = $false
        if ($null -ne $PreviousEnglish -and $index -lt $PreviousEnglish.Count -and $null -ne $ExistingLocale -and $index -lt $ExistingLocale.Count) {
            $existingLocaleValue = $ExistingLocale[$index]
            if ($PreviousEnglish[$index] -eq $currentValue -and -not (Test-LocaleValueNeedsRefresh -CurrentEnglish $currentValue -ExistingLocale $existingLocaleValue)) {
                $keepLocale = $true
            }
        }
        if ($keepLocale) {
            $merged.Add($ExistingLocale[$index]) | Out-Null
        }
        else {
            $merged.Add((Invoke-ConfiguredTranslation -Text $currentValue -Language $Language -LocaleRules $LocaleRules)) | Out-Null
        }
    }
    return $merged.ToArray()
}

function Test-FileContentEquals {
    param(
        [string]$FilePath,
        [string]$Content
    )

    $resolvedPath = Get-LongPath -Path $FilePath
    if (-not [System.IO.File]::Exists($resolvedPath)) {
        return $false
    }

    $existing = [System.IO.File]::ReadAllText($resolvedPath, $Utf8NoBom)
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

    [void]$builder.AppendLine('  <modDependencies>')
    foreach ($item in $dependencyItems) {
        [void]$builder.AppendLine('    <li>')
        [void]$builder.AppendLine("      <packageId>$($item.PackageId)</packageId>")
        [void]$builder.AppendLine("      <displayName>$([System.Security.SecurityElement]::Escape($item.DisplayName))</displayName>")
        [void]$builder.AppendLine('    </li>')
    }
    [void]$builder.AppendLine('  </modDependencies>')

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

function Write-SyncProgress {
    param(
        [string]$ProgressPath,
        [string]$CategoryId,
        [string[]]$Languages,
        [int]$OutputsQueued,
        [int]$CompletedOutputs,
        [string]$Status,
        [datetime]$StartedAt,
        [string]$CurrentOutput,
        [string]$LastCompleted,
        [string]$LastError,
        [Nullable[int]]$Errors = $null
    )

    $percent = if ($OutputsQueued -gt 0) {
        [math]::Min(100, [math]::Max(0, [math]::Floor(($CompletedOutputs * 100.0) / $OutputsQueued)))
    }
    else {
        0
    }

    $lines = @(
        "Category: $CategoryId",
        "Languages: $($Languages -join ', ')",
        "Outputs queued: $OutputsQueued",
        "Completed outputs: $CompletedOutputs",
        "Progress percent: $percent",
        "Started at: $($StartedAt.ToString('o'))",
        "Status: $Status",
        "Current output: $(if ([string]::IsNullOrWhiteSpace($CurrentOutput)) { 'none' } else { $CurrentOutput })",
        "Last completed: $(if ([string]::IsNullOrWhiteSpace($LastCompleted)) { 'none' } else { $LastCompleted })"
    )

    if (-not [string]::IsNullOrWhiteSpace($LastError)) {
        $lines += "Last error: $LastError"
    }

    if ($Errors -ne $null) {
        $lines += "Errors: $Errors"
    }

    Save-TextFile -FilePath $ProgressPath -Content (($lines -join [Environment]::NewLine) + [Environment]::NewLine)
}

function Sync-Category {
    param(
        [hashtable]$Config,
        [hashtable]$CategoryConfig,
        [string[]]$ResolvedLanguages,
        [pscustomobject]$State,
        [hashtable]$AllCategorySources,
        [hashtable]$LocaleRules,
        [string]$PreviousEnglishRoot,
        [switch]$SkipEnglishBaseline
    )

    if ($SkipEnglishBaseline -and [string]::IsNullOrWhiteSpace($PreviousEnglishRoot)) {
        throw 'PreviousEnglishRoot is required when SkipEnglishBaseline is set.'
    }

    $categorySources = $AllCategorySources[$CategoryConfig.id]
    $outputRoot = $CategoryConfig.outputModPath
    $englishRoot = Join-Path $outputRoot 'Languages\English'
    $cleanupLanguages = if ($SkipEnglishBaseline) {
        @($ResolvedLanguages | Where-Object { $_ -ne 'English' })
    }
    else {
        $ResolvedLanguages
    }
    Ensure-Directory -Path $englishRoot

    $isPartialSync = ($IncludeMods -and $IncludeMods.Count -gt 0)

    $aboutXml = Build-AboutXml -Config $Config -CategoryConfig $CategoryConfig -CategoryMods $categorySources.Mods -AllCategorySources $AllCategorySources
    $aboutPath = Join-Path $outputRoot 'About\About.xml'
    if (-not $isPartialSync -or -not (Test-Path -LiteralPath $aboutPath)) {
        Save-TextFile -FilePath $aboutPath -Content $aboutXml
    }

    $report = New-Object 'System.Collections.Generic.List[string]'
    if ($categorySources.MissingMods.Count -gt 0) {
        foreach ($missing in $categorySources.MissingMods) {
            $report.Add("Missing mod root for list entry: $missing") | Out-Null
        }
    }
    foreach ($mod in $categorySources.Mods) {
        $report.Add(("{0}: Keyed={1}, Names={2}, ExistingDefInjected={3}, ExtractedDefInjected={4}, UniqueOutputs={5}" -f $mod.FolderName, $mod.KeyedCount, $mod.NamesCount, $mod.ExistingDefInjectedCount, $mod.ExtractedDefInjectedCount, $mod.Outputs.Count)) | Out-Null
    }

    $outputs = New-Object 'System.Collections.Generic.List[object]'
    foreach ($mod in $categorySources.Mods) {
        foreach ($output in $mod.Outputs) {
            $outputs.Add($output) | Out-Null
        }
    }

    $outputErrors = New-Object 'System.Collections.Generic.List[string]'
    $progressPath = Join-Path $outputRoot 'Reports\current-sync-progress.txt'
    $startedAt = Get-Date
    $completedOutputs = 0
    $lastCompletedOutput = ''
    Write-SyncProgress -ProgressPath $progressPath -CategoryId $CategoryConfig.id -Languages $ResolvedLanguages -OutputsQueued $outputs.Count -CompletedOutputs 0 -Status 'running' -StartedAt $startedAt -CurrentOutput '' -LastCompleted '' -LastError ''

    $expectedPaths = @($outputs | Select-Object -ExpandProperty OutputRelPath -Unique)
    if (-not $isPartialSync) {
        Remove-StaleOutputFiles -OutputRoot $outputRoot -Languages $cleanupLanguages -ExpectedRelativePaths $expectedPaths
    }

    foreach ($output in $outputs) {
        try {
            $currentIndex = $completedOutputs + 1
            Write-Host ("[{0}/{1}] Translating {2}" -f $currentIndex, $outputs.Count, $output.OutputRelPath)
            Write-SyncProgress -ProgressPath $progressPath -CategoryId $CategoryConfig.id -Languages $ResolvedLanguages -OutputsQueued $outputs.Count -CompletedOutputs $completedOutputs -Status 'running' -StartedAt $startedAt -CurrentOutput $output.OutputRelPath -LastCompleted $lastCompletedOutput -LastError ''
            $englishPath = Join-Path $englishRoot $output.OutputRelPath
            switch ($output.Kind) {
                'Keyed' {
                    $previousEnglishPath = if ($SkipEnglishBaseline) { Join-Path $PreviousEnglishRoot $output.OutputRelPath } else { $englishPath }
                    $previousEnglish = Read-LanguageXml -FilePath $previousEnglishPath
                    if ($SkipEnglishBaseline) {
                        if (-not (Test-Path -LiteralPath $englishPath)) {
                            throw "English baseline missing for $($output.OutputRelPath). Run English Sync first."
                        }
                        $currentEnglish = Read-LanguageXml -FilePath $englishPath
                    }
                    else {
                        $currentEnglish = Read-LanguageXml -FilePath $output.SourcePath
                        if ($null -eq $currentEnglish) {
                            throw "Unable to parse source Keyed file: $($output.SourcePath)"
                        }
                        Write-LanguageXml -FilePath $englishPath -Entries $currentEnglish.Entries -Comments @()
                    }
                    foreach ($language in $ResolvedLanguages) {
                        if ($language -eq 'English') {
                            continue
                        }

                        $localePath = Join-Path $outputRoot (Join-Path "Languages\$language" $output.OutputRelPath)
                        $existingLocale = Read-LanguageXml -FilePath $localePath
                        $mergedEntries = Merge-XmlEntries -CurrentEnglish $currentEnglish.Entries -PreviousEnglish $(if ($previousEnglish) { $previousEnglish.Entries } else { $null }) -ExistingLocale $(if ($existingLocale) { $existingLocale.Entries } else { $null }) -Language $language -LocaleRules $LocaleRules
                        Write-LanguageXml -FilePath $localePath -Entries $mergedEntries -Comments @()
                    }
                }
                'Names' {
                    $previousEnglishPath = if ($SkipEnglishBaseline) { Join-Path $PreviousEnglishRoot $output.OutputRelPath } else { $englishPath }
                    $previousEnglish = Read-NamesFile -FilePath $previousEnglishPath
                    if ($SkipEnglishBaseline) {
                        if (-not (Test-Path -LiteralPath $englishPath)) {
                            throw "English baseline missing for $($output.OutputRelPath). Run English Sync first."
                        }
                        $currentEnglishLines = Read-NamesFile -FilePath $englishPath
                    }
                    else {
                        $currentEnglishLines = Read-NamesFile -FilePath $output.SourcePath
                        Write-NamesFile -FilePath $englishPath -Lines $currentEnglishLines
                    }
                    foreach ($language in $ResolvedLanguages) {
                        if ($language -eq 'English') {
                            continue
                        }
                        $localePath = Join-Path $outputRoot (Join-Path "Languages\$language" $output.OutputRelPath)
                        $existingLocale = Read-NamesFile -FilePath $localePath
                        $mergedLines = Merge-NameLines -CurrentEnglish $currentEnglishLines -PreviousEnglish $previousEnglish -ExistingLocale $existingLocale -Language $language -LocaleRules $LocaleRules
                        Write-NamesFile -FilePath $localePath -Lines $mergedLines
                    }
                }
                'DefInjected' {
                    $previousEnglishPath = if ($SkipEnglishBaseline) { Join-Path $PreviousEnglishRoot $output.OutputRelPath } else { $englishPath }
                    $previousEnglish = Read-LanguageXml -FilePath $previousEnglishPath
                    if ($SkipEnglishBaseline) {
                        if (-not (Test-Path -LiteralPath $englishPath)) {
                            throw "English baseline missing for $($output.OutputRelPath). Run English Sync first."
                        }
                        $currentEnglish = Read-LanguageXml -FilePath $englishPath
                        $currentEnglishEntries = $currentEnglish.Entries
                    }
                    else {
                        $currentEnglishEntries = Get-OrderedEntriesFromPairs -Pairs $output.Entries
                        Write-LanguageXml -FilePath $englishPath -Entries $currentEnglishEntries -Comments @()
                    }
                    foreach ($language in $ResolvedLanguages) {
                        if ($language -eq 'English') {
                            continue
                        }
                        $localePath = Join-Path $outputRoot (Join-Path "Languages\$language" $output.OutputRelPath)
                        $existingLocale = Read-LanguageXml -FilePath $localePath
                        $mergedEntries = Merge-XmlEntries -CurrentEnglish $currentEnglishEntries -PreviousEnglish $(if ($previousEnglish) { $previousEnglish.Entries } else { $null }) -ExistingLocale $(if ($existingLocale) { $existingLocale.Entries } else { $null }) -Language $language -LocaleRules $LocaleRules
                        Write-LanguageXml -FilePath $localePath -Entries $mergedEntries -Comments @()
                    }
                }
                default {
                    throw "Unhandled output kind: $($output.Kind)"
                }
            }

            $completedOutputs++
            $lastCompletedOutput = $output.OutputRelPath
            Write-SyncProgress -ProgressPath $progressPath -CategoryId $CategoryConfig.id -Languages $ResolvedLanguages -OutputsQueued $outputs.Count -CompletedOutputs $completedOutputs -Status 'running' -StartedAt $startedAt -CurrentOutput '' -LastCompleted $lastCompletedOutput -LastError ''
        }
        catch {
            $outputErrors.Add(('Error in {0}: {1}' -f $output.OutputRelPath, $_.Exception.Message)) | Out-Null
            Write-Warning ("Failed {0}: {1}" -f $output.OutputRelPath, $_.Exception.Message)
            Write-SyncProgress -ProgressPath $progressPath -CategoryId $CategoryConfig.id -Languages $ResolvedLanguages -OutputsQueued $outputs.Count -CompletedOutputs $completedOutputs -Status 'running-with-errors' -StartedAt $startedAt -CurrentOutput $output.OutputRelPath -LastCompleted $lastCompletedOutput -LastError $_.Exception.Message -Errors $outputErrors.Count
        }
    }

    $reportPath = Join-Path $outputRoot 'Reports\last-sync-report.txt'
    $summaryLines = @(
        "Category: $($CategoryConfig.id)",
        "Source mods: $($categorySources.Mods.Count)",
        "Output files: $($outputs.Count)",
        "Languages: $($ResolvedLanguages -join ', ')"
    ) + $report.ToArray() + $outputErrors.ToArray()
    Save-TextFile -FilePath $reportPath -Content (($summaryLines -join [Environment]::NewLine) + [Environment]::NewLine)
    Write-SyncProgress -ProgressPath $progressPath -CategoryId $CategoryConfig.id -Languages $ResolvedLanguages -OutputsQueued $outputs.Count -CompletedOutputs $completedOutputs -Status 'completed' -StartedAt $startedAt -CurrentOutput '' -LastCompleted $lastCompletedOutput -LastError '' -Errors $outputErrors.Count

    try {
        Remove-EmptyDirectories -RootPath (Join-Path $outputRoot 'Languages')
    }
    catch {
        Write-Warning ('Failed to prune empty language directories under {0}: {1}' -f $outputRoot, $_.Exception.Message)
    }
}

$config = Load-Config -FilePath $ConfigPath
$categoryMap = Get-CategoryMap -Config $config
if ($PSBoundParameters.ContainsKey('Category') -and -not [string]::IsNullOrWhiteSpace($Category)) {
    $Category = Resolve-CategoryAlias -CategoryName $Category
}
if (-not [string]::IsNullOrWhiteSpace($PreviousEnglishRoot)) {
    $PreviousEnglishRoot = Get-AbsolutePath -BasePath $config.__ConfigRoot -Path $PreviousEnglishRoot
}
Import-TranslatableMembersFromRimWorldSource -RimWorldRoot $config.paths.RimWorldRoot
$resolvedLanguages = Get-LanguageList -Config $config -RequestedLanguages $Languages
$localeRules = Load-LocaleRules -Config $config
$state = Load-State -Config $config
$script:TranslationConfig = $config.translation
$script:TranslationState = $state
$currentTranslationProvider = if ($script:TranslationConfig.ContainsKey('provider')) {
    [string]$script:TranslationConfig.provider
}
else {
    'none'
}
$currentTranslationProfile = '{0}|{1}|{2}' -f $currentTranslationProvider, [string]$script:TranslationConfig.sourceLanguageCode, 'raw-placeholders-with-shape-restore-v4'
$previousTranslationProvider = if ($state.Data.translation.ContainsKey('provider')) {
    [string]$state.Data.translation.provider
}
else {
    ''
}
$previousTranslationProfile = if ($state.Data.translation.ContainsKey('profile')) {
    [string]$state.Data.translation.profile
}
else {
    ''
}
$script:TranslationForceRefreshAllLocales = $false
if (-not [string]::IsNullOrWhiteSpace($currentTranslationProfile) -and $currentTranslationProvider -ne 'none') {
    if ([string]::IsNullOrWhiteSpace($previousTranslationProfile)) {
        if ($state.Data.translationCache.Count -gt 0) {
            $script:TranslationForceRefreshAllLocales = $true
        }
    }
    elseif ($previousTranslationProfile -ne $currentTranslationProfile) {
        $script:TranslationForceRefreshAllLocales = $true
    }
}
$state.Data.translation.provider = $currentTranslationProvider
$state.Data.translation.profile = $currentTranslationProfile
$state.Data.translation.sourceLanguageCode = [string]$script:TranslationConfig.sourceLanguageCode

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
        Sync-Category -Config $config -CategoryConfig $categoryMap[$Category] -ResolvedLanguages $resolvedLanguages -State $state -AllCategorySources $allSources -LocaleRules $localeRules -PreviousEnglishRoot $PreviousEnglishRoot -SkipEnglishBaseline:$SkipEnglishBaseline
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
            Sync-Category -Config $config -CategoryConfig $categoryMap[$categoryId] -ResolvedLanguages $resolvedLanguages -State $state -AllCategorySources $allSources -LocaleRules $localeRules -PreviousEnglishRoot $PreviousEnglishRoot -SkipEnglishBaseline:$SkipEnglishBaseline
        }
        Save-State -State $state
    }
}
