[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExportRoot,

    [ValidateSet('none', 'google-gtx', 'argos', 'test')]
    [string]$Provider = 'none',

    [string]$OfflinePythonPath = '',

    [string]$OfflineArgosPackagesDir = '',

    [string]$OutputRoot = '',

    [string[]]$Languages = @('ChineseSimplified'),

    [ValidateRange(250, 10000)]
    [int]$MaxBatchCharacters = 2500,

    [ValidateRange(1, 500)]
    [int]$MaxBatchItems = 100,

    [ValidateRange(0, 60000)]
    [int]$RequestDelayMs = 750,

    [switch]$EstimateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$Languages = @($Languages | ForEach-Object { @([string]$_ -split ',') } | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$startedUtc = [DateTime]::UtcNow
$statusRoot = Join-Path $ExportRoot 'Status'
$statusPath = Join-Path $statusRoot 'translation.status.json'
$cancelPath = Join-Path $statusRoot 'translation.cancel.request'
$workRoot = Join-Path $ExportRoot 'Translation'
$cachePath = Join-Path $workRoot 'translation-cache.json'
$sourceIndexPath = Join-Path $workRoot 'previous-source-index.json'
$updatePlanPath = Join-Path $workRoot 'last-update-plan.json'
$reportPath = Join-Path $workRoot 'translation-report.txt'
$offlineConfigPath = Join-Path $workRoot 'offline-provider.json'
$sourceRoot = Join-Path $ExportRoot 'FIP-English Language Pack'
$cache = @{}
$glossaryPath = Join-Path $PSScriptRoot 'translation-glossary.csv'
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    # Keep the final packs close to RimWorld's Config root. Putting them below
    # FIP-YesMan\Exports adds enough fixed path text to break namespaced Defs on
    # Windows PowerShell/.NET Framework's legacy MAX_PATH implementation.
    $yesManRoot = Split-Path -Parent $ExportRoot
    $configRoot = Split-Path -Parent $yesManRoot
    $OutputRoot = Join-Path $configRoot 'FIP-Languages'
}

$languageSpecs = @{
    German = @{ Folder = 'FIP-German Language Pack'; Display = 'FIP - German Language Pack'; PackageId = 'FIP.Translation.German'; Code = 'de'; ArgosCode = 'de'; Description = 'German language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    French = @{ Folder = 'FIP-French Language Pack'; Display = 'FIP - French Language Pack'; PackageId = 'FIP.Translation.French'; Code = 'fr'; ArgosCode = 'fr'; Description = 'French language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    Spanish = @{ Folder = 'FIP-Spanish Language Pack'; Display = 'FIP - Spanish Language Pack'; PackageId = 'FIP.Translation.Spanish'; Code = 'es'; ArgosCode = 'es'; Description = 'Spanish language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    Japanese = @{ Folder = 'FIP-Japanese Language Pack'; Display = 'FIP - Japanese Language Pack'; PackageId = 'FIP.Translation.Japanese'; Code = 'ja'; ArgosCode = 'ja'; Description = 'Japanese language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    Korean = @{ Folder = 'FIP-Korean Language Pack'; Display = 'FIP - Korean Language Pack'; PackageId = 'FIP.Translation.Korean'; Code = 'ko'; ArgosCode = 'ko'; Description = 'Korean language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    ChineseSimplified = @{ Folder = 'FIP-Simplified Chinese Language Pack'; Display = 'FIP - Simplified Chinese Language Pack'; PackageId = 'FIP.Translation.ChineseSimplified'; Code = 'zh-CN'; ArgosCode = 'zh'; Description = 'Simplified Chinese language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    ChineseTraditional = @{ Folder = 'FIP-Traditional Chinese Language Pack'; Display = 'FIP - Traditional Chinese Language Pack'; PackageId = 'FIP.Translation.ChineseTraditional'; Code = 'zh-TW'; ArgosCode = 'zh'; OpenCc = 's2t'; Description = 'Traditional Chinese language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    Russian = @{ Folder = 'FIP-Russian Language Pack'; Display = 'FIP - Russian Language Pack'; PackageId = 'FIP.Translation.Russian'; Code = 'ru'; ArgosCode = 'ru'; Description = 'Russian language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    PortugueseBrazilian = @{ Folder = 'FIP-Brazilian Portuguese Language Pack'; Display = 'FIP - Brazilian Portuguese Language Pack'; PackageId = 'FIP.Translation.PortugueseBrazilian'; Code = 'pt-BR'; ArgosCode = 'pt'; Description = 'Brazilian Portuguese language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    Polish = @{ Folder = 'FIP-Polish Language Pack'; Display = 'FIP - Polish Language Pack'; PackageId = 'FIP.Translation.Polish'; Code = 'pl'; ArgosCode = 'pl'; Description = 'Polish language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    Italian = @{ Folder = 'FIP-Italian Language Pack'; Display = 'FIP - Italian Language Pack'; PackageId = 'FIP.Translation.Italian'; Code = 'it'; ArgosCode = 'it'; Description = 'Italian language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    Ukrainian = @{ Folder = 'FIP-Ukrainian Language Pack'; Display = 'FIP - Ukrainian Language Pack'; PackageId = 'FIP.Translation.Ukrainian'; Code = 'uk'; ArgosCode = 'uk'; Description = 'Ukrainian language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    Dutch = @{ Folder = 'FIP-Dutch Language Pack'; Display = 'FIP - Dutch Language Pack'; PackageId = 'FIP.Translation.Dutch'; Code = 'nl'; ArgosCode = 'nl'; Description = 'Dutch language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
    Czech = @{ Folder = 'FIP-Czech Language Pack'; Display = 'FIP - Czech Language Pack'; PackageId = 'FIP.Translation.Czech'; Code = 'cs'; ArgosCode = 'cs'; Description = 'Czech language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.' }
}

function Ensure-Directory([string]$Path) {
    if (-not [string]::IsNullOrWhiteSpace($Path)) {
        [void][System.IO.Directory]::CreateDirectory($Path)
    }
}

function Save-JsonAtomic([string]$Path, [object]$Value) {
    Ensure-Directory (Split-Path -Parent $Path)
    $temporary = $Path + '.tmp'
    $json = $Value | ConvertTo-Json -Depth 12
    [System.IO.File]::WriteAllText($temporary, $json + [Environment]::NewLine, $utf8NoBom)
    if ([System.IO.File]::Exists($Path)) {
        $backup = $Path + '.bak'
        if ([System.IO.File]::Exists($backup)) { [System.IO.File]::Delete($backup) }
        [System.IO.File]::Replace($temporary, $Path, $backup)
        if ([System.IO.File]::Exists($backup)) { [System.IO.File]::Delete($backup) }
    }
    else {
        [System.IO.File]::Move($temporary, $Path)
    }
}

$status = [ordered]@{
    schemaVersion = 1
    id = 'translation'
    title = 'Sprachpakete uebersetzen: ' + ($Languages -join ', ')
    state = 'running'
    phase = 'Initialisierung'
    current = 0
    total = 0
    currentItem = ''
    message = ''
    error = ''
    startedUtc = $startedUtc.ToString('O')
    updatedUtc = $startedUtc.ToString('O')
    completedUtc = ''
    elapsedSeconds = 0.0
    etaSeconds = -1.0
    processId = $PID
    added = 0
    changed = 0
    removed = 0
    unchanged = 0
    apiRequests = 0
    cacheHits = 0
}

function Update-Status {
    param(
        [string]$Phase,
        [long]$Current = $status.current,
        [long]$Total = $status.total,
        [string]$CurrentItem = $status.currentItem,
        [string]$State = $status.state,
        [string]$Message = $status.message,
        [string]$ErrorText = $status.error
    )
    $now = [DateTime]::UtcNow
    $status.phase = $Phase
    $status.current = $Current
    $status.total = $Total
    $status.currentItem = $CurrentItem
    $status.state = $State
    $status.message = $Message
    $status.error = $ErrorText
    $status.updatedUtc = $now.ToString('O')
    $status.elapsedSeconds = [Math]::Max(0, ($now - $startedUtc).TotalSeconds)
    if ($State -eq 'running' -and $Current -gt 0 -and $Total -gt $Current) {
        $status.etaSeconds = ($status.elapsedSeconds / $Current) * ($Total - $Current)
    }
    elseif ($State -eq 'completed') {
        $status.etaSeconds = 0.0
        $status.completedUtc = $now.ToString('O')
    }
    else {
        $status.etaSeconds = -1.0
    }
    Save-JsonAtomic -Path $statusPath -Value $status
}

function Get-Hash([string]$Value) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Value)))).Replace('-', '')
    }
    finally {
        $sha.Dispose()
    }
}

function Protect-Text([string]$Text, [string]$Language, [object[]]$Glossary) {
    $tokens = [System.Collections.Generic.List[object]]::new()
    # Regex MatchEvaluator script blocks execute in child scopes under Windows
    # PowerShell. A mutable object keeps the counter shared across every match.
    $tokenState = [pscustomobject]@{ Index = 0 }
    $protected = $Text
    foreach ($entry in @($Glossary | Sort-Object { ([string]$_.source).Length } -Descending)) {
        $sourceTerm = [string]$entry.source
        if ([string]::IsNullOrWhiteSpace($sourceTerm)) { continue }
        $languageProperty = $entry.PSObject.Properties[$Language]
        $targetTerm = if ([string]$entry.keep_original -ieq 'true') {
            $sourceTerm
        }
        elseif ($null -ne $languageProperty) {
            [string]$languageProperty.Value
        }
        else {
            ''
        }
        if ([string]::IsNullOrWhiteSpace($targetTerm)) { continue }
        $protected = [regex]::Replace($protected, [regex]::Escape($sourceTerm), [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)
            $token = '__FIP_TERM_' + $tokenState.Index + '__'
            $tokens.Add([pscustomobject]@{ token = $token; value = $targetTerm })
            $tokenState.Index++
            return $token
        }, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    }
    $pattern = '\r\n|\n|\r|\t|<[^<>]+>|\[[^\]]+\]|\{[^{}\r\n]+\}|%[a-zA-Z]|\\[nrt]'
    $protected = [regex]::Replace($protected, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{
        param($match)
        $token = '__FIP_TOKEN_' + $tokenState.Index + '__'
        $tokens.Add([pscustomobject]@{ token = $token; value = $match.Value })
        $tokenState.Index++
        return $token
    })
    return [pscustomobject]@{ text = $protected; tokens = $tokens.ToArray() }
}

function Restore-Text([string]$Text, [object[]]$Tokens) {
    $result = $Text
    foreach ($token in $Tokens) {
        $kind = if ([string]$token.token -match 'TERM') { 'TERM' } else { 'TOKEN' }
        $normalized = [regex]::Replace($result, '(?i)_*FIP[ _]*' + $kind + '[ _]*' + [regex]::Escape(([string]$token.token -replace '\D', '')) + '_*', [string]$token.token)
        $result = $normalized.Replace([string]$token.token, [string]$token.value)
    }
    return $result
}

function Get-StructuralTokens([string]$Text) {
    return @([regex]::Matches($Text, '\r\n|\n|\r|\t|<[^<>]+>|\[[^\]]+\]|\{[^{}\r\n]+\}|%[a-zA-Z]|\\[nrt]') | ForEach-Object Value)
}

function Test-SameTokenSequence([object[]]$Left, [object[]]$Right) {
    if ($Left.Count -ne $Right.Count) { return $false }
    for ($index = 0; $index -lt $Left.Count; $index++) {
        if ([string]$Left[$index] -cne [string]$Right[$index]) { return $false }
    }
    return $true
}

function Test-UnrestoredProtectionMarker([string]$Text) {
    return $Text -match '(?i)FIP\s*TERM\s*\d+|ZXQFIPTOKEN\d+ZXQ|FIP_TOKEN_?\d+'
}

function Normalize-TranslatedText([string]$Source, [string]$Translation) {
    # Numeric settings and percentages are UI literals, not translatable prose.
    # Keeping them exact also prevents values such as "10% OFF" from being
    # misread as a RimWorld %O placeholder by the structural-token validator.
    if ($Source -match '^\s*[\d.,+\-–—/%:() ]+\s*$') { return $Source }

    $sourceTokens = @(Get-StructuralTokens $Source)
    $targetTokens = @(Get-StructuralTokens $Translation)
    if (Test-SameTokenSequence $sourceTokens $targetTokens) { return $Translation }

    $candidate = $Translation
    if ($targetTokens.Count -gt $sourceTokens.Count) {
        $removeState = [pscustomobject]@{ Position = 0 }
        $candidate = [regex]::Replace($candidate, '\r\n|\n|\r|\t|<[^<>]+>|\[[^\]]+\]|\{[^{}\r\n]+\}|%[a-zA-Z]|\\[nrt]', [System.Text.RegularExpressions.MatchEvaluator]{
            param($match)
            if ($removeState.Position -lt $sourceTokens.Count -and $match.Value -ceq [string]$sourceTokens[$removeState.Position]) {
                $removeState.Position++
                return $match.Value
            }
            return ''
        })
        if ($removeState.Position -ne $sourceTokens.Count) { return $Source }
        $targetTokens = @(Get-StructuralTokens $candidate)
    }

    if ($targetTokens.Count -ne $sourceTokens.Count) { return $Source }
    $replaceState = [pscustomobject]@{ Position = 0 }
    return [regex]::Replace($candidate, '\r\n|\n|\r|\t|<[^<>]+>|\[[^\]]+\]|\{[^{}\r\n]+\}|%[a-zA-Z]|\\[nrt]', [System.Text.RegularExpressions.MatchEvaluator]{
        param($match)
        $value = [string]$sourceTokens[$replaceState.Position]
        $replaceState.Position++
        return $value
    })
}

function Wait-RequestThrottle {
    if ($RequestDelayMs -le 0 -or $null -eq $script:lastProviderRequestUtc) { return }
    $elapsedMs = ([DateTime]::UtcNow - $script:lastProviderRequestUtc).TotalMilliseconds
    $remainingMs = $RequestDelayMs - $elapsedMs
    if ($remainingMs -gt 0) {
        Start-Sleep -Milliseconds ([int][Math]::Ceiling($remainingMs))
    }
}

function Quote-ProcessArgument([string]$Value) {
    return '"' + $Value.Replace('"', '\"') + '"'
}

function Start-OfflineTranslationProvider([string]$Language) {
    if ($Provider -ne 'argos') { return }
    Stop-OfflineTranslationProvider
    $effectiveOfflineConfigPath = if (Test-Path -LiteralPath $offlineConfigPath) { $offlineConfigPath } else { Join-Path $PSScriptRoot 'offline-provider.json' }
    if (([string]::IsNullOrWhiteSpace($OfflinePythonPath) -or [string]::IsNullOrWhiteSpace($OfflineArgosPackagesDir)) -and (Test-Path -LiteralPath $effectiveOfflineConfigPath)) {
        $offlineConfig = Get-Content -Raw -LiteralPath $effectiveOfflineConfigPath | ConvertFrom-Json
        if ([string]::IsNullOrWhiteSpace($OfflinePythonPath)) { $script:OfflinePythonPath = [string]$offlineConfig.pythonPath }
        if ([string]::IsNullOrWhiteSpace($OfflineArgosPackagesDir)) { $script:OfflineArgosPackagesDir = [string]$offlineConfig.argosPackagesDir }
    }
    if ([string]::IsNullOrWhiteSpace($OfflinePythonPath) -or -not (Test-Path -LiteralPath $OfflinePythonPath -PathType Leaf)) {
        throw "Lokale Python-Laufzeit nicht gefunden. Konfiguriere 'pythonPath' in $offlineConfigPath"
    }
    if ([string]::IsNullOrWhiteSpace($OfflineArgosPackagesDir) -or -not (Test-Path -LiteralPath $OfflineArgosPackagesDir -PathType Container)) {
        throw "Argos-Sprachmodell nicht gefunden. Konfiguriere 'argosPackagesDir' in $offlineConfigPath"
    }
    $providerScript = Join-Path $PSScriptRoot 'OfflineTranslationProvider.py'
    if (-not (Test-Path -LiteralPath $providerScript -PathType Leaf)) {
        throw "Offline-Provider fehlt: $providerScript"
    }

    $targetCode = [string]$languageSpecs[$Language].ArgosCode
    Update-Status -Phase ('Lokalen Uebersetzer laden: ' + $Language) -Message ('Argos Translate en-' + $targetCode + ' wird lokal initialisiert.')
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $OfflinePythonPath
    $startInfo.Arguments = ((Quote-ProcessArgument $providerScript), '--provider', 'argos', '--source', 'en', '--target', $targetCode, '--argos-packages-dir', (Quote-ProcessArgument $OfflineArgosPackagesDir)) -join ' '
    $languageSpec = $languageSpecs[$Language]
    if ($languageSpec.ContainsKey('OpenCc') -and -not [string]::IsNullOrWhiteSpace([string]$languageSpec.OpenCc)) {
        $startInfo.Arguments += ' --opencc-config ' + (Quote-ProcessArgument ([string]$languageSpec.OpenCc))
    }
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.EnvironmentVariables['PYTHONIOENCODING'] = 'utf-8'
    $script:offlineProviderProcess = [Diagnostics.Process]::new()
    $script:offlineProviderProcess.StartInfo = $startInfo
    if (-not $script:offlineProviderProcess.Start()) { throw 'Argos Translate konnte nicht gestartet werden.' }
    $readyLine = $script:offlineProviderProcess.StandardOutput.ReadLine()
    if ([string]::IsNullOrWhiteSpace($readyLine)) {
        throw 'Argos Translate wurde beendet, bevor das Modell bereit war.'
    }
    $ready = $readyLine | ConvertFrom-Json
    if (-not $ready.ready) { throw ('Argos Translate meldet keine Bereitschaft: ' + $readyLine) }
}

function Stop-OfflineTranslationProvider {
    if ($null -eq $script:offlineProviderProcess) { return }
    try { $script:offlineProviderProcess.StandardInput.Close() } catch {}
    try {
        if (-not $script:offlineProviderProcess.WaitForExit(3000)) { $script:offlineProviderProcess.Kill() }
    } catch {}
    try { $script:offlineProviderProcess.Dispose() } catch {}
    $script:offlineProviderProcess = $null
}

function Invoke-OfflineTranslationBatch([object[]]$Items) {
    if ($null -eq $script:offlineProviderProcess -or $script:offlineProviderProcess.HasExited) {
        throw 'Der lokale Argos-Prozess ist nicht aktiv.'
    }
    $request = @{ texts_b64 = @($Items | ForEach-Object { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes([string]$_.prepared.text)) }) } | ConvertTo-Json -Compress
    $script:offlineProviderProcess.StandardInput.WriteLine($request)
    $script:offlineProviderProcess.StandardInput.Flush()
    $responseLine = $script:offlineProviderProcess.StandardOutput.ReadLine()
    if ([string]::IsNullOrWhiteSpace($responseLine)) { throw 'Argos Translate hat keine Antwort geliefert.' }
    $response = $responseLine | ConvertFrom-Json
    if ($response.PSObject.Properties.Name -contains 'error') { throw ('Argos Translate: ' + [string]$response.error) }
    $translations = @($response.translations_b64 | ForEach-Object { [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String([string]$_)) })
    if ($translations.Count -ne $Items.Count) {
        throw ('Argos Translate lieferte ' + $translations.Count + ' statt ' + $Items.Count + ' Ergebnissen.')
    }
    return $translations
}

function Invoke-GoogleTranslation([string]$Text, [string]$TargetCode) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return $Text }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $uri = 'https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=' + [Uri]::EscapeDataString($TargetCode) + '&dt=t&q=' + [Uri]::EscapeDataString($Text)
    $lastError = $null
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        try {
            Wait-RequestThrottle
            $script:lastProviderRequestUtc = [DateTime]::UtcNow
            $response = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 60
            $builder = [Text.StringBuilder]::new()
            foreach ($segment in @($response[0])) {
                if ($segment -is [Array] -and $segment.Count -gt 0 -and $null -ne $segment[0]) {
                    [void]$builder.Append([string]$segment[0])
                }
            }
            if ($builder.Length -gt 0) { return $builder.ToString() }
            throw 'Leere Antwort des Uebersetzungsdienstes.'
        }
        catch {
            $lastError = $_
            if ($attempt -lt 8) {
                $isRateLimit = $_.Exception.Message -match '429|Too Many Requests|automated queries'
                try { $isRateLimit = $isRateLimit -or ([int]$_.Exception.Response.StatusCode -eq 429) } catch {}
                $delaySeconds = if ($isRateLimit) {
                    [Math]::Min(900, 30 * [Math]::Pow(2, $attempt - 1))
                }
                else {
                    [Math]::Min(60, [Math]::Pow(2, $attempt))
                }
                Update-Status -Phase ('Web-API retry ' + ($attempt + 1) + '/8') -Message ($_.Exception.Message + ' Neuer Versuch in ' + $delaySeconds + ' Sekunden.')
                Start-Sleep -Seconds $delaySeconds
            }
        }
    }
    throw $lastError
}

function Assert-NotCancelled {
    if (Test-Path -LiteralPath $cancelPath) {
        Remove-Item -LiteralPath $cancelPath -Force
        throw [OperationCanceledException]::new('Uebersetzung wurde durch den Benutzer abgebrochen.')
    }
}

function Translate-Text([string]$Text, [string]$Language, [hashtable]$Cache) {
    if ([string]::IsNullOrWhiteSpace($Text) -or $Provider -eq 'none') { return $Text }
    $prepared = Protect-Text -Text $Text -Language $Language -Glossary $script:translationGlossary
    $termSignature = (@($prepared.tokens | Where-Object { [string]$_.token -match 'TERM' } | ForEach-Object { [string]$_.token + '=' + [string]$_.value }) -join '|')
    $cacheSchema = if ($Provider -eq 'argos' -and $prepared.tokens.Count -gt 0) { 'v6' } elseif ($Provider -eq 'argos') { 'v4' } else { 'v3' }
    if ($Provider -eq 'argos' -and $languageSpecs[$Language].ContainsKey('OpenCc')) { $cacheSchema += '-opencc-' + [string]$languageSpecs[$Language].OpenCc }
    $key = Get-Hash ($cacheSchema + '|' + $Provider + '|' + $Language + '|' + $Text + '|' + $termSignature)
    if ($Cache.ContainsKey($key)) {
        if (Test-UnrestoredProtectionMarker ([string]$Cache[$key])) {
            [void]$Cache.Remove($key)
        }
        else {
            $script:cacheHits++
            $status.cacheHits = $script:cacheHits
            $translated = Normalize-TranslatedText -Source $Text -Translation ([string]$Cache[$key])
            $Cache[$key] = $translated
            return $translated
        }
    }
    if ($Provider -eq 'test') {
        $translated = $prepared.text
    }
    else {
        $script:apiRequests++
        $status.apiRequests = $script:apiRequests
        $translated = Invoke-GoogleTranslation -Text $prepared.text -TargetCode $languageSpecs[$Language].Code
    }
    $translated = Restore-Text -Text $translated -Tokens $prepared.tokens
    $translated = Normalize-TranslatedText -Source $Text -Translation $translated
    $Cache[$key] = $translated
    return $translated
}

function Invoke-PreparedTranslationBatch([object[]]$Items, [string]$Language, [hashtable]$Cache) {
    if ($Items.Count -eq 0) { return @() }

    if ($Provider -eq 'argos') {
        $script:apiRequests++
        $status.apiRequests = $script:apiRequests
        $offlineTranslations = @(Invoke-OfflineTranslationBatch -Items $Items)
        $offlineResults = [System.Collections.Generic.List[string]]::new()
        for ($offlineIndex = 0; $offlineIndex -lt $Items.Count; $offlineIndex++) {
            $offlineTranslated = Restore-Text -Text ([string]$offlineTranslations[$offlineIndex]) -Tokens $Items[$offlineIndex].prepared.tokens
            $offlineTranslated = Normalize-TranslatedText -Source ([string]$Items[$offlineIndex].source) -Translation $offlineTranslated
            $Cache[[string]$Items[$offlineIndex].cacheKey] = $offlineTranslated
            $offlineResults.Add($offlineTranslated)
        }
        return $offlineResults.ToArray()
    }

    $markers = [System.Collections.Generic.List[string]]::new()
    $builder = [Text.StringBuilder]::new()
    for ($index = 0; $index -lt $Items.Count; $index++) {
        if ($index -gt 0) {
            $marker = '__FIP_BATCH_BREAK_' + $index.ToString('D6') + '__'
            $markers.Add($marker)
            [void]$builder.Append($marker)
        }
        [void]$builder.Append([string]$Items[$index].prepared.text)
    }

    if ($Provider -eq 'test') {
        $script:apiRequests++
        $status.apiRequests = $script:apiRequests
        $translatedBatch = $builder.ToString()
    }
    else {
        $script:apiRequests++
        $status.apiRequests = $script:apiRequests
        $translatedBatch = Invoke-GoogleTranslation -Text $builder.ToString() -TargetCode $languageSpecs[$Language].Code
    }

    $normalized = $translatedBatch
    foreach ($marker in $markers) {
        $number = [regex]::Match($marker, '\d+').Value
        $fuzzyMarker = '(?i)_+\s*FIP[ _]*BATCH[ _]*BREAK[ _]*0*' + [regex]::Escape(([int]$number).ToString()) + '\s*_+'
        $normalized = [regex]::Replace($normalized, $fuzzyMarker, $marker)
    }
    $markerPattern = '__FIP_BATCH_BREAK_\d{6}__'
    $parts = @([regex]::Split($normalized, $markerPattern))

    if ($parts.Count -ne $Items.Count) {
        if ($Items.Count -eq 1) {
            throw 'Der Uebersetzungsdienst hat die Batch-Markierung eines einzelnen Eintrags beschaedigt.'
        }
        $middle = [int][Math]::Floor($Items.Count / 2)
        $left = @(Invoke-PreparedTranslationBatch -Items @($Items[0..($middle - 1)]) -Language $Language -Cache $Cache)
        $right = @(Invoke-PreparedTranslationBatch -Items @($Items[$middle..($Items.Count - 1)]) -Language $Language -Cache $Cache)
        return @($left + $right)
    }

    $results = [System.Collections.Generic.List[string]]::new()
    for ($index = 0; $index -lt $Items.Count; $index++) {
        $translated = Restore-Text -Text ([string]$parts[$index]) -Tokens $Items[$index].prepared.tokens
        $translated = Normalize-TranslatedText -Source ([string]$Items[$index].source) -Translation $translated
        $Cache[[string]$Items[$index].cacheKey] = $translated
        $results.Add($translated)
    }
    return $results.ToArray()
}

function Translate-Texts([string[]]$Texts, [string]$Language, [hashtable]$Cache, [long]$CurrentBase, [long]$Total, [string]$CurrentItem) {
    $results = [string[]]::new($Texts.Count)
    $pending = [System.Collections.Generic.List[object]]::new()
    $pendingByCacheKey = @{}
    $resolvedCount = 0

    for ($index = 0; $index -lt $Texts.Count; $index++) {
        $text = [string]$Texts[$index]
        if ([string]::IsNullOrWhiteSpace($text) -or $Provider -eq 'none') {
            $results[$index] = $text
            $resolvedCount++
            continue
        }
        $prepared = Protect-Text -Text $text -Language $Language -Glossary $script:translationGlossary
        $termSignature = (@($prepared.tokens | Where-Object { [string]$_.token -match 'TERM' } | ForEach-Object { [string]$_.token + '=' + [string]$_.value }) -join '|')
        $cacheSchema = if ($Provider -eq 'argos' -and $prepared.tokens.Count -gt 0) { 'v6' } elseif ($Provider -eq 'argos') { 'v4' } else { 'v3' }
        if ($Provider -eq 'argos' -and $languageSpecs[$Language].ContainsKey('OpenCc')) { $cacheSchema += '-opencc-' + [string]$languageSpecs[$Language].OpenCc }
        $cacheKey = Get-Hash ($cacheSchema + '|' + $Provider + '|' + $Language + '|' + $text + '|' + $termSignature)
        if ($Cache.ContainsKey($cacheKey)) {
            if (Test-UnrestoredProtectionMarker ([string]$Cache[$cacheKey])) {
                [void]$Cache.Remove($cacheKey)
            }
            else {
                $script:cacheHits++
                $status.cacheHits = $script:cacheHits
                $results[$index] = Normalize-TranslatedText -Source $text -Translation ([string]$Cache[$cacheKey])
                $Cache[$cacheKey] = $results[$index]
                $resolvedCount++
                continue
            }
        }
        if ($pendingByCacheKey.ContainsKey($cacheKey)) {
            $pendingByCacheKey[$cacheKey].indexes.Add($index)
            continue
        }
        $indexes = [System.Collections.Generic.List[int]]::new()
        $indexes.Add($index)
        $item = [pscustomobject]@{ indexes = $indexes; prepared = $prepared; cacheKey = $cacheKey; source = $text }
        $pending.Add($item)
        $pendingByCacheKey[$cacheKey] = $item
    }

    $position = 0
    while ($position -lt $pending.Count) {
        Assert-NotCancelled
        $batch = [System.Collections.Generic.List[object]]::new()
        $batchCharacters = 0
        while ($position -lt $pending.Count -and $batch.Count -lt $MaxBatchItems) {
            $candidate = $pending[$position]
            $separatorCharacters = if ($batch.Count -eq 0) { 0 } else { 32 }
            $candidateCharacters = ([string]$candidate.prepared.text).Length + $separatorCharacters
            if ($batch.Count -gt 0 -and ($batchCharacters + $candidateCharacters) -gt $MaxBatchCharacters) { break }
            $batch.Add($candidate)
            $batchCharacters += $candidateCharacters
            $position++
        }
        Update-Status -Phase ('Uebersetze ' + $Language + ' (Batch)') -Current ($CurrentBase + $resolvedCount) -Total $Total -CurrentItem ($CurrentItem + ' :: ' + $batch.Count + ' Texte')
        $translations = @(Invoke-PreparedTranslationBatch -Items $batch.ToArray() -Language $Language -Cache $Cache)
        for ($batchIndex = 0; $batchIndex -lt $batch.Count; $batchIndex++) {
            foreach ($resultIndex in $batch[$batchIndex].indexes) {
                $results[[int]$resultIndex] = [string]$translations[$batchIndex]
                $resolvedCount++
            }
        }
        Update-Status -Phase ('Uebersetze ' + $Language + ' (Batch)') -Current ($CurrentBase + $resolvedCount) -Total $Total -CurrentItem $CurrentItem
    }
    return $results
}

function ConvertTo-Hashtable($Object) {
    $result = @{}
    if ($null -eq $Object) { return $result }
    foreach ($property in $Object.PSObject.Properties) { $result[$property.Name] = [string]$property.Value }
    return $result
}

function Get-TranslationUnits([string]$EnglishRoot) {
    $units = [System.Collections.Generic.List[object]]::new()
    foreach ($file in Get-ChildItem -LiteralPath $EnglishRoot -Recurse -File | Sort-Object FullName) {
        $relative = $file.FullName.Substring($EnglishRoot.Length).TrimStart('\', '/')
        if ($file.Extension -ieq '.xml') {
            $document = [xml]::new()
            $document.PreserveWhitespace = $true
            try { $document.Load($file.FullName) }
            catch { throw "Ungueltige XML-Quelldatei '$relative': $($_.Exception.Message)" }
            foreach ($element in @($document.DocumentElement.ChildNodes | Where-Object NodeType -eq Element)) {
                $nodeIndex = 0
                foreach ($textNode in @($element.SelectNodes('.//text()'))) {
                    if (-not [string]::IsNullOrWhiteSpace($textNode.Value)) {
                        $identity = 'xml|' + $relative.Replace('\', '/') + '|' + $element.Name + '|' + $nodeIndex
                        $units.Add([pscustomobject]@{ identity = $identity; kind = 'xml'; file = $relative; key = $element.Name; source = $textNode.Value })
                    }
                    $nodeIndex++
                }
            }
        }
        elseif ($file.Extension -ieq '.txt') {
            $lines = [System.IO.File]::ReadAllLines($file.FullName, [Text.Encoding]::UTF8)
            for ($line = 0; $line -lt $lines.Length; $line++) {
                if (-not [string]::IsNullOrWhiteSpace($lines[$line])) {
                    $identity = 'text|' + $relative.Replace('\', '/') + '|' + $line
                    $units.Add([pscustomobject]@{ identity = $identity; kind = 'text'; file = $relative; key = [string]$line; source = $lines[$line] })
                }
            }
        }
    }
    return $units.ToArray()
}

function Copy-SourceShell([string]$Destination, [hashtable]$Spec, [string]$Language) {
    if (-not [System.IO.Directory]::Exists($Destination)) {
        [void][System.IO.Directory]::CreateDirectory($Destination)
    }
    foreach ($directory in [System.IO.Directory]::GetDirectories($sourceRoot, '*', [System.IO.SearchOption]::AllDirectories)) {
        $relative = $directory.Substring($sourceRoot.Length).TrimStart('\', '/')
        if ($relative.Equals('Languages\English', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.StartsWith('Languages\English\', [StringComparison]::OrdinalIgnoreCase)) { continue }
        [void][System.IO.Directory]::CreateDirectory((Join-Path $Destination $relative))
    }
    foreach ($file in [System.IO.Directory]::GetFiles($sourceRoot, '*', [System.IO.SearchOption]::AllDirectories)) {
        $relative = $file.Substring($sourceRoot.Length).TrimStart('\', '/')
        if ($relative.StartsWith('Languages\English\', [StringComparison]::OrdinalIgnoreCase)) { continue }
        if (-not $relative.Contains('\') -and $relative -iin @('english-delta.snapshot.json', 'manifest.json', 'report.txt')) { continue }
        $target = Join-Path $Destination $relative
        Ensure-Directory (Split-Path -Parent $target)
        [System.IO.File]::Copy($file, $target, $true)
    }
    $languageTarget = Join-Path $Destination ('Languages\' + $Language)
    Ensure-Directory $languageTarget

    $aboutPath = Join-Path $Destination 'About\About.xml'
    if (Test-Path -LiteralPath $aboutPath) {
        [xml]$about = [System.IO.File]::ReadAllText($aboutPath, [Text.Encoding]::UTF8)
        $about.ModMetaData.packageId = $Spec.PackageId
        $about.ModMetaData.name = $Spec.Display
        $about.ModMetaData.description = $Spec.Description
        $about.Save($aboutPath)
    }
}

function Write-TranslatedFile([string]$SourceFile, [string]$TargetFile, [string]$Language, [hashtable]$Cache, [ref]$Done, [long]$Total) {
    Ensure-Directory (Split-Path -Parent $TargetFile)
    if ([System.IO.Path]::GetExtension($SourceFile) -ieq '.xml') {
        $document = [xml]::new()
        $document.PreserveWhitespace = $true
        $document.Load($SourceFile)
        $textNodes = @($document.SelectNodes('/*/*//text()') | Where-Object { -not [string]::IsNullOrWhiteSpace($_.Value) })
        $sourceTexts = @($textNodes | ForEach-Object { [string]$_.Value })
        $translatedTexts = @(Translate-Texts -Texts $sourceTexts -Language $Language -Cache $Cache -CurrentBase $Done.Value -Total $Total -CurrentItem (Split-Path -Leaf $SourceFile))
        for ($index = 0; $index -lt $textNodes.Count; $index++) {
            $textNodes[$index].Value = $translatedTexts[$index]
        }
        $Done.Value += $textNodes.Count
        $settings = [Xml.XmlWriterSettings]::new()
        $settings.Encoding = $utf8NoBom
        $settings.Indent = $true
        $settings.NewLineChars = "`n"
        $settings.NewLineHandling = [Xml.NewLineHandling]::Replace
        $writer = [Xml.XmlWriter]::Create($TargetFile + '.tmp', $settings)
        try { $document.Save($writer) } finally { $writer.Dispose() }
        [System.IO.File]::Copy($TargetFile + '.tmp', $TargetFile, $true)
        [System.IO.File]::Delete($TargetFile + '.tmp')
    }
    else {
        $lines = [System.IO.File]::ReadAllLines($SourceFile, [Text.Encoding]::UTF8)
        $lineIndexes = @(for ($line = 0; $line -lt $lines.Length; $line++) { if (-not [string]::IsNullOrWhiteSpace($lines[$line])) { $line } })
        $sourceTexts = @($lineIndexes | ForEach-Object { [string]$lines[$_] })
        $translatedTexts = @(Translate-Texts -Texts $sourceTexts -Language $Language -Cache $Cache -CurrentBase $Done.Value -Total $Total -CurrentItem (Split-Path -Leaf $SourceFile))
        for ($index = 0; $index -lt $lineIndexes.Count; $index++) {
            $translatedLine = [string]$translatedTexts[$index]
            $lines[$lineIndexes[$index]] = if ([string]::IsNullOrWhiteSpace($translatedLine)) { $sourceTexts[$index] } else { $translatedLine }
        }
        $Done.Value += $lineIndexes.Count
        $sourceContent = [System.IO.File]::ReadAllText($SourceFile, [Text.Encoding]::UTF8)
        $newLine = if ($sourceContent.Contains("`r`n")) { "`r`n" } else { "`n" }
        $targetContent = [string]::Join($newLine, $lines)
        if ($sourceContent.EndsWith("`n") -or $sourceContent.EndsWith("`r")) { $targetContent += $newLine }
        [System.IO.File]::WriteAllText($TargetFile, $targetContent, $utf8NoBom)
    }
}

try {
    Update-Status -Phase 'Englische Baseline pruefen'
    $englishRoot = Join-Path $sourceRoot 'Languages\English'
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'About\About.xml')) -or -not (Test-Path -LiteralPath $englishRoot)) {
        throw "Keine vollstaendige englische Baseline gefunden: $sourceRoot"
    }
    $unknown = @($Languages | Where-Object { -not $languageSpecs.ContainsKey($_) })
    if ($unknown.Count -gt 0) { throw 'Unbekannte Sprache(n): ' + ($unknown -join ', ') }
    $script:translationGlossary = if (Test-Path -LiteralPath $glossaryPath) { @(Import-Csv -LiteralPath $glossaryPath -Encoding UTF8) } else { @() }
    $script:apiRequests = 0L
    $script:cacheHits = 0L
    $script:lastProviderRequestUtc = $null
    $script:offlineProviderProcess = $null

    Update-Status -Phase 'Uebersetzungsvolumen zaehlen'
    $units = @(Get-TranslationUnits -EnglishRoot $englishRoot)
    $total = [long]$units.Count * $Languages.Count
    $characters = 0L
    foreach ($unit in $units) { $characters += [long]$unit.source.Length }
    $currentSourceIndex = [ordered]@{}
    foreach ($unit in $units) { $currentSourceIndex[$unit.identity] = Get-Hash ([string]$unit.source) }
    $previousSourceIndex = @{}
    if (Test-Path -LiteralPath $sourceIndexPath) { $previousSourceIndex = ConvertTo-Hashtable (Get-Content -Raw -LiteralPath $sourceIndexPath | ConvertFrom-Json) }
    $added = [System.Collections.Generic.List[string]]::new()
    $changed = [System.Collections.Generic.List[string]]::new()
    $removed = [System.Collections.Generic.List[string]]::new()
    $unchanged = 0
    foreach ($identity in $currentSourceIndex.Keys) {
        if (-not $previousSourceIndex.ContainsKey($identity)) { $added.Add($identity); continue }
        if ([string]$previousSourceIndex[$identity] -cne [string]$currentSourceIndex[$identity]) { $changed.Add($identity) } else { $unchanged++ }
    }
    foreach ($identity in $previousSourceIndex.Keys) { if (-not $currentSourceIndex.Contains($identity)) { $removed.Add($identity) } }
    $status.added = $added.Count
    $status.changed = $changed.Count
    $status.removed = $removed.Count
    $status.unchanged = $unchanged
    $updatePlan = [ordered]@{
        schemaVersion = 1; generatedUtc = [DateTime]::UtcNow.ToString('O'); sourceUnits = $units.Count
        addedCount = $added.Count; changedCount = $changed.Count; removedCount = $removed.Count; unchangedCount = $unchanged
        added = $added.ToArray(); changed = $changed.ToArray(); removed = $removed.ToArray()
    }
    Save-JsonAtomic -Path $updatePlanPath -Value $updatePlan
    Ensure-Directory $workRoot
    $estimate = @(
        'FIP translation estimate',
        'Generated UTC: ' + [DateTime]::UtcNow.ToString('O'),
        'Provider: ' + $Provider,
        'Languages: ' + ($Languages -join ', '),
        'Source units: ' + $units.Count,
        'Source characters: ' + $characters,
        'Total language units: ' + $total,
        'Estimated total source characters sent without cache: ' + ($characters * $Languages.Count),
        'New source units: ' + $added.Count,
        'Changed source units: ' + $changed.Count,
        'Removed source units: ' + $removed.Count,
        'Unchanged source units: ' + $unchanged
    ) -join [Environment]::NewLine
    [System.IO.File]::WriteAllText($reportPath, $estimate + [Environment]::NewLine, $utf8NoBom)
    if ($EstimateOnly) {
        Update-Status -Phase 'Volumenschaetzung abgeschlossen' -Current $total -Total $total -State 'completed' -Message ('Schaetzung fertig: ' + $units.Count + ' Texte, ' + $characters + ' Zeichen je vollstaendiger Sprachrunde.')
        exit 0
    }
    if ($Provider -eq 'none') {
        throw 'Kein Uebersetzungsanbieter konfiguriert. Nutze -EstimateOnly fuer eine reine Volumenschaetzung oder waehle einen Provider.'
    }

    if (Test-Path -LiteralPath $cachePath) {
        # Windows PowerShell 5.1 otherwise reads UTF-8 without a BOM through the
        # active ANSI code page. That silently turns valid translations into
        # mojibake when the cache is loaded and written again.
        $cache = ConvertTo-Hashtable (Get-Content -Raw -LiteralPath $cachePath -Encoding UTF8 | ConvertFrom-Json)
    }
    $outputParent = Split-Path -Parent $OutputRoot
    Ensure-Directory $outputParent
    $generationRoot = Join-Path $outputParent '.FIP-Lang.tmp'
    if (Test-Path -LiteralPath $generationRoot) { Remove-Item -LiteralPath $generationRoot -Recurse -Force }
    Ensure-Directory $generationRoot
    if (Test-Path -LiteralPath $OutputRoot) {
        foreach ($directory in [System.IO.Directory]::GetDirectories($OutputRoot, '*', [System.IO.SearchOption]::AllDirectories)) {
            $relative = $directory.Substring($OutputRoot.Length).TrimStart('\', '/')
            Ensure-Directory (Join-Path $generationRoot $relative)
        }
        foreach ($file in [System.IO.Directory]::GetFiles($OutputRoot, '*', [System.IO.SearchOption]::AllDirectories)) {
            $relative = $file.Substring($OutputRoot.Length).TrimStart('\', '/')
            $target = Join-Path $generationRoot $relative
            Ensure-Directory (Split-Path -Parent $target)
            [System.IO.File]::Copy($file, $target, $true)
        }
    }
    $done = 0L
    $filesSinceCacheSave = 0
    foreach ($language in $Languages) {
        $spec = $languageSpecs[$language]
        $targetRoot = Join-Path $generationRoot $spec.Folder
        if (Test-Path -LiteralPath $targetRoot) { Remove-Item -LiteralPath $targetRoot -Recurse -Force }
        Start-OfflineTranslationProvider -Language $language
        Copy-SourceShell -Destination $targetRoot -Spec $spec -Language $language
        foreach ($sourceFile in Get-ChildItem -LiteralPath $englishRoot -Recurse -File | Where-Object { $_.Extension -iin @('.xml', '.txt') } | Sort-Object FullName) {
            $relative = $sourceFile.FullName.Substring($englishRoot.Length).TrimStart('\', '/')
            $targetFile = Join-Path (Join-Path $targetRoot ('Languages\' + $language)) $relative
            Write-TranslatedFile -SourceFile $sourceFile.FullName -TargetFile $targetFile -Language $language -Cache $cache -Done ([ref]$done) -Total $total
            $filesSinceCacheSave++
            if ($filesSinceCacheSave -ge 10) {
                Save-JsonAtomic -Path $cachePath -Value $cache
                $filesSinceCacheSave = 0
            }
        }
        Stop-OfflineTranslationProvider
    }
    Save-JsonAtomic -Path $cachePath -Value $cache
    $auditScript = Join-Path $PSScriptRoot 'Audit-FIPLanguagePacks.ps1'
    if (Test-Path -LiteralPath $auditScript) {
        Update-Status -Phase 'Sprachpakete validieren' -Current $total -Total $total
        & $auditScript -EnglishPackRoot $sourceRoot -LanguagePacksRoot $generationRoot -Languages $Languages
        if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw 'Die Sprachpaket-QA hat Fehler gefunden.' }
    }
    Update-Status -Phase 'Gepruefte Sprachpakete aktivieren' -Current $total -Total $total
    $previousOutput = Join-Path $outputParent '.FIP-Lang.prev'
    if (Test-Path -LiteralPath $previousOutput) { Remove-Item -LiteralPath $previousOutput -Recurse -Force }
    if (Test-Path -LiteralPath $OutputRoot) { Move-Item -LiteralPath $OutputRoot -Destination $previousOutput }
    try {
        Move-Item -LiteralPath $generationRoot -Destination $OutputRoot
        if (Test-Path -LiteralPath $previousOutput) { Remove-Item -LiteralPath $previousOutput -Recurse -Force }
    }
    catch {
        if (-not (Test-Path -LiteralPath $OutputRoot) -and (Test-Path -LiteralPath $previousOutput)) { Move-Item -LiteralPath $previousOutput -Destination $OutputRoot }
        throw
    }
    Save-JsonAtomic -Path $sourceIndexPath -Value $currentSourceIndex
    $status.apiRequests = $script:apiRequests
    $status.cacheHits = $script:cacheHits
    Update-Status -Phase 'Uebersetzung abgeschlossen' -Current $total -Total $total -State 'completed' -Message ('Alle ' + $Languages.Count + ' Sprachpakete wurden erzeugt: ' + $OutputRoot)
}
catch [OperationCanceledException] {
    # The first run has no cache file yet. Always persist the in-memory cache so
    # a user-requested stop is genuinely resumable from the first request on.
    Save-JsonAtomic -Path $cachePath -Value $cache
    $status.completedUtc = [DateTime]::UtcNow.ToString('O')
    Update-Status -Phase 'Uebersetzung abgebrochen' -State 'cancelled' -Message $_.Exception.Message
    exit 2
}
catch {
    # Network and provider failures must not discard translations already
    # completed during this run either.
    if ($cache.Count -gt 0) { Save-JsonAtomic -Path $cachePath -Value $cache }
    $status.completedUtc = [DateTime]::UtcNow.ToString('O')
    Update-Status -Phase 'Uebersetzung fehlgeschlagen' -State 'failed' -Message $_.Exception.Message -ErrorText ($_ | Out-String)
    exit 1
}
finally {
    Stop-OfflineTranslationProvider
}
