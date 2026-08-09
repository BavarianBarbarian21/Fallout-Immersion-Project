param(
    [int]$Workers = 4,
    [switch]$NoLegacyImport
)

$ErrorActionPreference = 'Stop'
$toolRoot = $PSScriptRoot
$logRoot = Join-Path $toolRoot 'logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$logPath = Join-Path $logRoot 'translation-run.log'
$statusPath = Join-Path $toolRoot 'LAST_RUN_STATUS.txt'

$pythonCandidates = @(
    'C:\Users\Matthias\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe',
    (Get-Command python.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
    (Get-Command py.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1)
) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }

if (-not $pythonCandidates) {
    throw 'Python 3 could not be found. Edit Run-All.ps1 and add a Python 3 executable.'
}

$python = $pythonCandidates[0]
$arguments = @(
    (Join-Path $toolRoot 'translation_tool.py'),
    'all',
    '--workers',
    [string]$Workers
)
if ($NoLegacyImport) {
    $arguments += '--no-legacy-import'
}

@(
    'FIP Translation Next'
    'Status: RUNNING'
    ('Started: ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))
    ('Python: ' + $python)
    ('Log: ' + $logPath)
) | Set-Content -LiteralPath $statusPath -Encoding UTF8

& $python @arguments 2>&1 | Tee-Object -FilePath $logPath
$exitCode = $LASTEXITCODE

@(
    'FIP Translation Next'
    ('Status: ' + $(if ($exitCode -eq 0) { 'COMPLETED' } else { 'FAILED' }))
    ('Finished: ' + (Get-Date).ToString('yyyy-MM-dd HH:mm:ss zzz'))
    ('Exit code: ' + $exitCode)
    ('Log: ' + $logPath)
) | Set-Content -LiteralPath $statusPath -Encoding UTF8

exit $exitCode
