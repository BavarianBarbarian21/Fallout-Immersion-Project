[CmdletBinding()]
param(
    [string]$RepoRoot,
    [string]$ReportPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$toolsRoot = if (-not [string]::IsNullOrWhiteSpace($PSScriptRoot)) {
    $PSScriptRoot
}
else {
    Split-Path -Parent $MyInvocation.MyCommand.Path
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Split-Path -Parent $toolsRoot
}

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $toolsRoot 'fip-file-naming-report.txt'
}

function Get-NormalizedToken {
    param([string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value)) {
        return ''
    }

    return (($Value -replace '^FIP-', '') -replace '[^A-Za-z0-9]', '').ToLowerInvariant()
}

function Test-IsInfrastructurePath {
    param([string]$RelativePath)

    $normalized = $RelativePath.Replace('/', '\')
    return (
        $normalized -ieq 'LoadFolders.xml' -or
        $normalized -like 'About\*' -or
        $normalized -like 'LoadFolders\*' -or
        $normalized -like 'Languages\*'
    )
}

function Test-IsAllowedGenericFile {
    param([string]$FileName)

    return $FileName -in @(
        'About.xml',
        'PublishedFileId.txt',
        'Preview.png',
        'Icon.png',
        'LoadFolders.xml'
    )
}

$modFolders = @(Get-ChildItem -LiteralPath $RepoRoot -Directory | Where-Object {
    $_.Name -like 'FIP-*' -and $_.Name -ne 'FIP-Translation'
} | Sort-Object Name)
$lines = New-Object 'System.Collections.Generic.List[string]'
$lines.Add(('Repo root: {0}' -f $RepoRoot)) | Out-Null
$lines.Add(('Checked mods: {0}' -f $modFolders.Count)) | Out-Null
$lines.Add(('Generated: {0}' -f (Get-Date).ToString('o'))) | Out-Null
$lines.Add('') | Out-Null

foreach ($mod in $modFolders) {
    $expectedToken = Get-NormalizedToken -Value $mod.Name
    $issues = New-Object 'System.Collections.Generic.List[string]'

    foreach ($file in Get-ChildItem -LiteralPath $mod.FullName -File -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName) {
        $relativePath = $file.FullName.Substring($mod.FullName.Length).TrimStart('\')
        if (Test-IsInfrastructurePath -RelativePath $relativePath) {
            continue
        }

        if (Test-IsAllowedGenericFile -FileName $file.Name) {
            continue
        }

        $baseToken = Get-NormalizedToken -Value ([System.IO.Path]::GetFileNameWithoutExtension($file.Name))
        if ([string]::IsNullOrWhiteSpace($baseToken)) {
            continue
        }

        if (-not $baseToken.StartsWith($expectedToken)) {
            $issues.Add($relativePath) | Out-Null
        }
    }

    $lines.Add(('[{0}] expected token: {1}' -f $mod.Name, $expectedToken)) | Out-Null
    if ($issues.Count -eq 0) {
        $lines.Add('OK') | Out-Null
    }
    else {
        $lines.Add(('Potential naming mismatches: {0}' -f $issues.Count)) | Out-Null
        foreach ($issue in $issues) {
            $lines.Add(('  ' + $issue)) | Out-Null
        }
    }
    $lines.Add('') | Out-Null
}

[System.IO.File]::WriteAllLines($ReportPath, $lines, [System.Text.UTF8Encoding]::new($false))
Write-Host ('Wrote naming audit report to ' + $ReportPath)