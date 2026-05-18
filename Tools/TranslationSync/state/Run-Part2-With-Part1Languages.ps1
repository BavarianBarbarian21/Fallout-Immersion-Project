[CmdletBinding()]
param(
    [string[]]$IncludeMods,
    [switch]$PruneExtraLanguages
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$translationSyncRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Split-Path -Parent $translationSyncRoot
$projectRoot = Split-Path -Parent $toolsRoot

$toolPath = Join-Path $translationSyncRoot 'Invoke-TranslationSync.ps1'
$part1LanguagesRoot = Join-Path $projectRoot 'FIP-Translation Part 1\Languages'
$part2LanguagesRoot = Join-Path $projectRoot 'FIP-Translation Part 2\Languages'
$statusPath = Join-Path $PSScriptRoot 'part2-all-part1-languages-status.txt'
$logPath = Join-Path $PSScriptRoot 'part2-all-part1-languages-log.txt'

if (-not (Test-Path -LiteralPath $toolPath)) {
    throw "TranslationSync entrypoint not found: $toolPath"
}

if (-not (Test-Path -LiteralPath $part1LanguagesRoot)) {
    throw "Part 1 languages root not found: $part1LanguagesRoot"
}

$languages = @(
    Get-ChildItem -LiteralPath $part1LanguagesRoot -Directory |
        Select-Object -ExpandProperty Name |
        Sort-Object
)

if ($languages.Count -eq 0) {
    throw "No languages found under $part1LanguagesRoot"
}

Remove-Item -LiteralPath $statusPath -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $logPath -ErrorAction SilentlyContinue

try {
    Set-ExecutionPolicy -Scope Process Bypass -Force

    if ($PruneExtraLanguages -and (Test-Path -LiteralPath $part2LanguagesRoot)) {
        $existingPart2Languages = @(
            Get-ChildItem -LiteralPath $part2LanguagesRoot -Directory |
                Select-Object -ExpandProperty Name
        )

        foreach ($language in @($existingPart2Languages | Where-Object { $_ -notin $languages })) {
            Remove-Item -LiteralPath (Join-Path $part2LanguagesRoot $language) -Recurse -Force
        }
    }

    $invokeParams = @{
        Command = 'sync'
        Category = 'part2'
        Languages = $languages
    }

    if ($IncludeMods -and $IncludeMods.Count -gt 0) {
        $invokeParams.IncludeMods = $IncludeMods
    }

    $allOutput = & $toolPath @invokeParams *>&1
    $logText = if ($allOutput) {
        ($allOutput | Out-String -Width 4096)
    }
    else {
        'No command output.'
    }

    Set-Content -LiteralPath $logPath -Value $logText -Encoding UTF8
    Set-Content -LiteralPath $statusPath -Value @(
        'success',
        ('Languages: ' + ($languages -join ', ')),
        ('IncludeMods: ' + ($(if ($IncludeMods) { $IncludeMods -join ', ' } else { '<all>' })))
    ) -Encoding UTF8
}
catch {
    $errorText = ($_ | Out-String) + [Environment]::NewLine + $_.ScriptStackTrace
    Set-Content -LiteralPath $logPath -Value $errorText -Encoding UTF8
    Set-Content -LiteralPath $statusPath -Value $errorText -Encoding UTF8
    throw
}