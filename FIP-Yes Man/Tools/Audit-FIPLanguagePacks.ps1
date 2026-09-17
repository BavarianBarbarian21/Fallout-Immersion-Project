[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EnglishPackRoot,
    [Parameter(Mandatory = $true)][string]$LanguagePacksRoot,
    [Parameter(Mandatory = $true)][string[]]$Languages
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
trap {
    Write-Error ($_.Exception.Message + [Environment]::NewLine + $_.ScriptStackTrace)
    exit 1
}
$strictUtf8 = [System.Text.UTF8Encoding]::new($false, $true)
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$englishRoot = Join-Path $EnglishPackRoot 'Languages\English'
$reportRoot = Join-Path $LanguagePacksRoot '_QA'
[void][System.IO.Directory]::CreateDirectory($reportRoot)
$specs = @{
    German = @{ Folder = 'FIP-German Language Pack'; Tag = 'de-DE' }
    French = @{ Folder = 'FIP-French Language Pack'; Tag = 'fr-FR' }
    Spanish = @{ Folder = 'FIP-Spanish Language Pack'; Tag = 'es-ES' }
    Japanese = @{ Folder = 'FIP-Japanese Language Pack'; Tag = 'ja-JP' }
    Korean = @{ Folder = 'FIP-Korean Language Pack'; Tag = 'ko-KR' }
    ChineseSimplified = @{ Folder = 'FIP-Simplified Chinese Language Pack'; Tag = 'zh-Hans' }
    ChineseTraditional = @{ Folder = 'FIP-Traditional Chinese Language Pack'; Tag = 'zh-Hant' }
    Russian = @{ Folder = 'FIP-Russian Language Pack'; Tag = 'ru-RU' }
    PortugueseBrazilian = @{ Folder = 'FIP-Brazilian Portuguese Language Pack'; Tag = 'pt-BR' }
    Polish = @{ Folder = 'FIP-Polish Language Pack'; Tag = 'pl-PL' }
    Italian = @{ Folder = 'FIP-Italian Language Pack'; Tag = 'it-IT' }
    Ukrainian = @{ Folder = 'FIP-Ukrainian Language Pack'; Tag = 'uk-UA' }
    Dutch = @{ Folder = 'FIP-Dutch Language Pack'; Tag = 'nl-NL' }
    Czech = @{ Folder = 'FIP-Czech Language Pack'; Tag = 'cs-CZ' }
}

function Relative-Path([string]$Root, [string]$Path) {
    return $Path.Substring($Root.Length).TrimStart('\', '/').Replace('\', '/')
}

function Read-Utf8Strict([string]$Path, [System.Collections.Generic.List[string]]$Errors) {
    try { return $strictUtf8.GetString([System.IO.File]::ReadAllBytes($Path)).TrimStart([char]0xFEFF) }
    catch { $Errors.Add("Invalid UTF-8: $Path"); return '' }
}

function Tokens([string]$Text) {
    return @([regex]::Matches($Text, '\r\n|\n|\r|\t|<[^<>]+>|\[[^\]]+\]|\{[^{}\r\n]+\}|%[a-zA-Z]|\\[nrt]') | ForEach-Object Value)
}

function Same-Sequence([object[]]$Left, [object[]]$Right) {
    if ($null -eq $Left) { $Left = [object[]]::new(0) }
    if ($null -eq $Right) { $Right = [object[]]::new(0) }
    if ($Left.Length -ne $Right.Length) { return $false }
    for ($index = 0; $index -lt $Left.Length; $index++) {
        if ([string]$Left[$index] -cne [string]$Right[$index]) { return $false }
    }
    return $true
}

$allErrors = 0
foreach ($language in $Languages) {
    if (-not $specs.ContainsKey($language)) { throw "Unknown language: $language" }
    $spec = $specs[$language]
    $targetRoot = Join-Path (Join-Path $LanguagePacksRoot $spec.Folder) ('Languages\' + $language)
    $errors = [System.Collections.Generic.List[string]]::new()
    $warnings = [System.Collections.Generic.List[string]]::new()
    $characters = [System.Collections.Generic.HashSet[char]]::new()
    $identities = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $checked = 0
    $identical = 0

    foreach ($sourceFile in Get-ChildItem -LiteralPath $englishRoot -Recurse -File | Where-Object Extension -iin @('.xml', '.txt') | Sort-Object FullName) {
        $relative = Relative-Path -Root $englishRoot -Path $sourceFile.FullName
        $targetFile = Join-Path $targetRoot $relative
        if (-not (Test-Path -LiteralPath $targetFile)) { $errors.Add("Missing file: $relative"); continue }
        $sourceText = Read-Utf8Strict -Path $sourceFile.FullName -Errors $errors
        $targetText = Read-Utf8Strict -Path $targetFile -Errors $errors
        foreach ($character in $targetText.ToCharArray()) { [void]$characters.Add($character) }
        # U+00C3 is also the legitimate uppercase Portuguese letter Ã. A broken
        # UTF-8 lead byte is followed by a non-ASCII character; Ã plus an ASCII
        # letter (for example NÃO) must therefore not be reported as mojibake.
        if ($targetText.Contains([char]0xFFFD) -or $targetText -cmatch '\u00C3[^\x00-\x7F]|\u00E2\u20AC') { $errors.Add("Mojibake or replacement character: $relative") }
        # Older Argos runs occasionally returned protected glossary sentinels
        # with altered whitespace. They are not RimWorld placeholders, so the
        # structural token comparison below cannot detect them on its own.
        if ($targetText -match '(?i)FIP\s*TERM\s*\d+|ZXQFIPTOKEN\d+ZXQ|FIP_TOKEN_?\d+') {
            $errors.Add("Unrestored translation protection marker: $relative")
        }

        if ($sourceFile.Extension -ieq '.xml') {
            try { [xml]$sourceDocument = $sourceText; [xml]$targetDocument = $targetText }
            catch { $errors.Add("Invalid XML: $relative ($($_.Exception.Message))"); continue }
            if ($targetText -notmatch '^\s*<\?xml[^>]+encoding=["'']utf-8["'']') { $warnings.Add("Missing explicit UTF-8 XML declaration: $relative") }
            $sourceElements = @($sourceDocument.DocumentElement.ChildNodes | Where-Object NodeType -eq Element)
            $targetElements = @($targetDocument.DocumentElement.ChildNodes | Where-Object NodeType -eq Element)
            $targetByKey = [System.Collections.Generic.Dictionary[string, System.Xml.XmlElement]]::new([StringComparer]::Ordinal)
            foreach ($element in $targetElements) {
                $identity = (Split-Path -Parent $relative) + '|' + $element.Name
                if (-not $identities.Add($identity)) { $errors.Add("Duplicate identity: $identity") }
                $targetByKey[$element.Name] = $element
            }
            foreach ($sourceElement in $sourceElements) {
                $checked++
                $targetElement = $null
                if (-not $targetByKey.TryGetValue([string]$sourceElement.Name, [ref]$targetElement)) { $errors.Add("Missing key: $relative :: $($sourceElement.Name)"); continue }
                $translated = [string]$targetElement.InnerText
                $sourceTokens = @(Tokens $sourceElement.InnerText)
                $targetTokens = @(Tokens $translated)
                if (-not (Same-Sequence $sourceTokens $targetTokens)) {
                    $sourceTokenText = ($sourceTokens | ForEach-Object { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$_)) }) -join ','
                    $targetTokenText = ($targetTokens | ForEach-Object { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$_)) }) -join ','
                    $errors.Add("Placeholder mismatch: $relative :: $($sourceElement.Name) [source=$sourceTokenText; target=$targetTokenText]")
                }
                if ($translated -ceq [string]$sourceElement.InnerText -and $translated -match '[A-Za-z]{3}') { $identical++ }
            }
        }
        else {
            $sourceLines = @($sourceText -split '\r?\n')
            $targetLines = @($targetText -split '\r?\n')
            if ($sourceLines.Count -ne $targetLines.Count) { $errors.Add("Line-count mismatch: $relative"); continue }
            for ($line = 0; $line -lt $sourceLines.Count; $line++) {
                if ([string]::IsNullOrWhiteSpace($sourceLines[$line])) { continue }
                $checked++
                if (-not (Same-Sequence (Tokens $sourceLines[$line]) (Tokens $targetLines[$line]))) { $errors.Add("Placeholder mismatch: $relative line $($line + 1)") }
                if ($targetLines[$line] -ceq $sourceLines[$line] -and $targetLines[$line] -match '[A-Za-z]{3}') { $identical++ }
            }
        }
    }

    $inventory = [string]::Concat(@($characters | Sort-Object))
    [System.IO.File]::WriteAllText((Join-Path $reportRoot ($language + '-characters.txt')), $inventory + [Environment]::NewLine, $utf8NoBom)
    $summary = [ordered]@{
        schemaVersion = 1; language = $language; bcp47 = $spec.Tag; checkedUnits = $checked
        identicalEnglishUnits = $identical; uniqueCharacters = $characters.Count
        errors = $errors.ToArray(); warnings = $warnings.ToArray(); generatedUtc = [DateTime]::UtcNow.ToString('O')
    }
    [System.IO.File]::WriteAllText((Join-Path $reportRoot ($language + '-qa.json')), ($summary | ConvertTo-Json -Depth 6) + [Environment]::NewLine, $utf8NoBom)
    $allErrors += $errors.Count
}

if ($allErrors -gt 0) { Write-Error "Language pack QA found $allErrors error(s)."; exit 1 }
Write-Host "Language pack QA passed for $(@($Languages).Count) language(s)." -ForegroundColor Green
exit 0
