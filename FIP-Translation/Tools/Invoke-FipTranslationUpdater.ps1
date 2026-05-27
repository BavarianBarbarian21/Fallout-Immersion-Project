[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('part1', 'part2', 'part3', 'part4')]
    [string]$Category,

    [Parameter(Mandatory)]
    [ValidateSet('EnglishSync', 'LanguageTranslation')]
    [string]$Mode
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$toolsRoot = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}

$translationRoot = Split-Path -Parent $toolsRoot
$repoRoot = Split-Path -Parent $translationRoot
$toolPath = Join-Path $repoRoot 'Tools\TranslationSync\Invoke-TranslationSync.ps1'
$configPath = Join-Path $toolsRoot 'config.json'
$partNames = @{
    part1 = 'FIP-Translation Part 1'
    part2 = 'FIP-Translation Part 2'
    part3 = 'FIP-Translation Part 3'
    part4 = 'FIP-Translation Part 4'
}

$statusRoot = Join-Path $toolsRoot 'state\launcher-status'
$statusPath = Join-Path $statusRoot ("{0}-{1}.json" -f $Category, $Mode)
$outputRoot = Join-Path $translationRoot $partNames[$Category]
$englishRoot = Join-Path $outputRoot 'Languages\English'
$snapshotRoot = Join-Path $toolsRoot (Join-Path 'state\previous-english' $Category)

function Write-LauncherStatus {
    param(
        [string]$State,
        [string]$Message,
        [Nullable[int]]$ExitCode = $null
    )

    New-Item -ItemType Directory -Path $statusRoot -Force | Out-Null

    $payload = [ordered]@{
        category    = $Category
        mode        = $Mode
        label       = "{0} {1}" -f $Category.ToUpperInvariant(), $Mode
        state       = $State
        message     = $Message
        progressPath = (Join-Path $outputRoot 'Reports\current-sync-progress.txt')
        updatedAt   = (Get-Date).ToString('o')
    }

    if ($ExitCode -ne $null) {
        $payload.exitCode = [int]$ExitCode
    }

    $json = $payload | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText($statusPath, $json, [System.Text.UTF8Encoding]::new($false))
}

function Copy-DirectoryContents {
    param(
        [string]$SourcePath,
        [string]$DestinationPath
    )

    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $DestinationPath -Force | Out-Null

    if (-not (Test-Path -LiteralPath $SourcePath)) {
        return
    }

    foreach ($item in Get-ChildItem -LiteralPath $SourcePath -Force) {
        $targetPath = Join-Path $DestinationPath $item.Name
        Copy-Item -LiteralPath $item.FullName -Destination $targetPath -Recurse -Force
    }
}

function Invoke-TranslationTool {
    param([string[]]$Arguments)

    & powershell.exe @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Translation tool failed with exit code $LASTEXITCODE."
    }
}

try {
    Write-LauncherStatus -State 'running' -Message 'Updater started.'

    if ($Mode -eq 'EnglishSync') {
        Copy-DirectoryContents -SourcePath $englishRoot -DestinationPath $snapshotRoot

        $arguments = @(
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', $toolPath,
            '-ConfigPath', $configPath,
            '-Command', 'sync',
            '-Category', $Category,
            '-Languages', 'English',
            '-RefreshLists'
        )

        if ($Category -eq 'part2') {
            $arguments += '-RefreshFcpSources'
        }

        Invoke-TranslationTool -Arguments $arguments
        Write-LauncherStatus -State 'completed' -Message 'English Sync finished successfully.' -ExitCode 0
        return
    }

    if (-not (Test-Path -LiteralPath $englishRoot)) {
        throw 'English Sync must run first so the current English baseline exists.'
    }

    if (-not (Test-Path -LiteralPath $snapshotRoot)) {
        throw 'Previous English snapshot missing. Run English Sync first.'
    }

    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $toolPath,
        '-ConfigPath', $configPath,
        '-Command', 'sync',
        '-Category', $Category,
        '-SkipEnglishBaseline',
        '-PreviousEnglishRoot', $snapshotRoot
    )

    Invoke-TranslationTool -Arguments $arguments
    Write-LauncherStatus -State 'completed' -Message 'Language Translation finished successfully.' -ExitCode 0
}
catch {
    Write-LauncherStatus -State 'failed' -Message $_.Exception.Message -ExitCode 1
    throw
}