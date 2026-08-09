param(
    [int]$Workers = 4
)

$ErrorActionPreference = 'Stop'
$toolRoot = $PSScriptRoot
$runner = Join-Path $toolRoot 'Run-All.ps1'
$logRoot = Join-Path $toolRoot 'logs'
$launchStatus = Join-Path $toolRoot 'DETACHED_RUN.txt'

New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$argumentLine = (
    '-NoProfile -ExecutionPolicy Bypass -File "' +
    $runner +
    '" -Workers ' +
    [string]$Workers
)
$process = Start-Process `
    -FilePath 'powershell.exe' `
    -ArgumentList $argumentLine `
    -WindowStyle Hidden `
    -PassThru

@(
    'FIP Translation Next - detached run'
    'Status: LAUNCHED'
    ('Launched: ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))
    ('Process ID: ' + $process.Id)
    ('Runner: ' + $runner)
    ('Live status: ' + (Join-Path $toolRoot 'LAST_RUN_STATUS.txt'))
    ('Progress: ' + (Join-Path $logRoot 'progress.txt'))
    ('Terminal log: ' + (Join-Path $logRoot 'translation-run.log'))
) | Set-Content -LiteralPath $launchStatus -Encoding UTF8

Write-Output ('Detached translation run launched with process ID ' + $process.Id)
Write-Output ('Status file: ' + $launchStatus)
