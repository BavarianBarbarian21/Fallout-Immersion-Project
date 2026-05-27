[CmdletBinding()]
param(
    [ValidateSet('part1', 'part2', 'part3')]
    [string[]]$Categories = @('part1', 'part2', 'part3'),

    [switch]$RestartIfBusy
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$translationSyncRoot = Split-Path -Parent $PSScriptRoot
$toolsRoot = Split-Path -Parent $translationSyncRoot
$projectRoot = Split-Path -Parent $toolsRoot

$toolPath = Join-Path $translationSyncRoot 'Invoke-TranslationSync.ps1'
$templateRoot = Join-Path $translationSyncRoot 'language-template'
$statusPath = Join-Path $PSScriptRoot 'part1-part3-all-languages-status.txt'
$logPath = Join-Path $PSScriptRoot 'part1-part3-all-languages-log.txt'

function Set-Status {
    param([string[]]$Lines)

    Set-Content -LiteralPath $statusPath -Value $Lines -Encoding UTF8
}

if (-not (Test-Path -LiteralPath $toolPath)) {
    throw "TranslationSync entrypoint not found: $toolPath"
}

if (-not (Test-Path -LiteralPath $templateRoot)) {
    throw "Language template root not found: $templateRoot"
}

$languages = @(
    Get-ChildItem -LiteralPath $templateRoot -Directory |
        Select-Object -ExpandProperty Name |
        Sort-Object
)

if ($languages.Count -eq 0) {
    throw "No languages found under $templateRoot"
}

Remove-Item -LiteralPath $statusPath -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $logPath -ErrorAction SilentlyContinue

function Get-ActiveTranslationSyncProcesses {
    return @(
        Get-CimInstance Win32_Process |
            Where-Object {
                $_.Name -eq 'powershell.exe' -and
                $_.ProcessId -ne $PID -and
                (
                    $_.CommandLine -like '*Invoke-TranslationSync.ps1*' -or
                    $_.CommandLine -like '*Run-Part1To3-AllLanguages.ps1*'
                ) -and
                $_.CommandLine -notlike '*-Command powershell.exe*'
            }
    )
}

$activeSyncProcesses = @(Get-ActiveTranslationSyncProcesses)

if ($activeSyncProcesses.Count -gt 0) {
    $processText = $activeSyncProcesses |
        Select-Object ProcessId, CommandLine |
        Out-String -Width 4096
    Set-Content -LiteralPath $logPath -Value $processText -Encoding UTF8

    if (-not $RestartIfBusy) {
        Set-Status -Lines @(
            'blocked',
            'A TranslationSync process is already active.',
            ('Processes: ' + ($activeSyncProcesses.ProcessId -join ', '))
        )
        throw ('A TranslationSync process is already active. Refusing to start a duplicate run.' + [Environment]::NewLine + $processText.TrimEnd())
    }

    Set-Status -Lines @(
        'restarting',
        'Stopping existing TranslationSync processes before a clean rerun.',
        ('Processes: ' + ($activeSyncProcesses.ProcessId -join ', '))
    )

    foreach ($process in $activeSyncProcesses) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }

    Wait-Process -Id $activeSyncProcesses.ProcessId -Timeout 15 -ErrorAction SilentlyContinue

    $remainingProcesses = @(Get-ActiveTranslationSyncProcesses)
    if ($remainingProcesses.Count -gt 0) {
        $remainingText = $remainingProcesses |
            Select-Object ProcessId, CommandLine |
            Out-String -Width 4096
        Set-Content -LiteralPath $logPath -Value $remainingText -Encoding UTF8
        Set-Status -Lines @(
            'blocked',
            'Some TranslationSync processes could not be stopped.',
            ('Processes: ' + ($remainingProcesses.ProcessId -join ', '))
        )
        throw ('Unable to stop existing TranslationSync processes before the clean rerun.' + [Environment]::NewLine + $remainingText.TrimEnd())
    }
}

try {
    Set-ExecutionPolicy -Scope Process Bypass -Force

    $combinedOutput = New-Object 'System.Collections.Generic.List[string]'
    foreach ($category in $Categories) {
        Set-Status -Lines @(
            'running',
            ('Category: ' + $category),
            ('Languages: ' + ($languages -join ', ')),
            ('Started: ' + (Get-Date).ToString('s'))
        )

        $combinedOutput.Add(('=== {0} ===' -f $category)) | Out-Null
        $categoryOutput = & $toolPath -Command sync -Category $category -Languages $languages *>&1
        if ($categoryOutput) {
            foreach ($line in @($categoryOutput | Out-String -Width 4096).TrimEnd().Split([Environment]::NewLine)) {
                $combinedOutput.Add($line) | Out-Null
            }
        }
        else {
            $combinedOutput.Add('No command output.') | Out-Null
        }
        $combinedOutput.Add('') | Out-Null
    }

    Set-Content -LiteralPath $logPath -Value $combinedOutput -Encoding UTF8
    Set-Status -Lines @(
        'success',
        ('Categories: ' + ($Categories -join ', ')),
        ('Languages: ' + ($languages -join ', ')),
        ('Completed: ' + (Get-Date).ToString('s'))
    )
}
catch {
    $errorText = ($_ | Out-String) + [Environment]::NewLine + $_.ScriptStackTrace
    Set-Content -LiteralPath $logPath -Value $errorText -Encoding UTF8
    Set-Status -Lines $errorText
    throw
}