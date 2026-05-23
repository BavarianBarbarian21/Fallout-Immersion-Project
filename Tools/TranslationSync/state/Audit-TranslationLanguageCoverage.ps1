param(
    [string]$Root = 'C:\Users\Matthias\Desktop\Fallout Immersion Project',
    [string]$ReportPath = 'C:\Users\Matthias\Desktop\Fallout Immersion Project\Tools\TranslationSync\state\translation-language-audit.json'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$parts = @(
    'FIP-Translation Part 1',
    'FIP-Translation Part 2',
    'FIP-Translation Part 3',
    'FIP-Translation Part 4'
)

function Get-FileHashString {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-PathPattern {
    param([string]$RelativePath)

    $segments = @($RelativePath -split '[\\/]')
    if ($segments.Count -eq 0) {
        return ''
    }

    switch ($segments[0]) {
        'DefInjected' {
            if ($segments.Count -ge 2) {
                return ('DefInjected\' + $segments[1])
            }
            return 'DefInjected'
        }
        'Strings' {
            if ($segments.Count -ge 2) {
                return ('Strings\' + $segments[1])
            }
            return 'Strings'
        }
        default {
            return $segments[0]
        }
    }
}

function Get-PatternSummary {
    param([string[]]$RelativePaths)

    return @(
        @($RelativePaths) |
            Group-Object { Get-PathPattern -RelativePath $_ } |
            Sort-Object Name |
            ForEach-Object {
                [pscustomobject]@{
                    Pattern = $_.Name
                    Count = $_.Count
                    Samples = @($_.Group | Sort-Object | Select-Object -First 5)
                }
            }
    )
}

$results = New-Object 'System.Collections.Generic.List[object]'

foreach ($part in $parts) {
    $langRoot = Join-Path (Join-Path $Root $part) 'Languages'
    $englishRoot = Join-Path $langRoot 'English'
    if (-not (Test-Path -LiteralPath $englishRoot)) {
        continue
    }

    $englishFiles = @(Get-ChildItem -LiteralPath $englishRoot -Recurse -File)
    $englishIndex = @{}
    foreach ($file in $englishFiles) {
        $relativePath = $file.FullName.Substring($englishRoot.Length + 1)
        $englishIndex[$relativePath] = Get-FileHashString -Path $file.FullName
    }

    $languages = @(Get-ChildItem -LiteralPath $langRoot -Directory | Where-Object { $_.Name -ne 'English' } | Sort-Object Name)
    foreach ($language in $languages) {
        $languageFiles = @(Get-ChildItem -LiteralPath $language.FullName -Recurse -File -ErrorAction SilentlyContinue)
        $languageIndex = @{}
        foreach ($file in $languageFiles) {
            $relativePath = $file.FullName.Substring($language.FullName.Length + 1)
            $languageIndex[$relativePath] = $file.FullName
        }

        $missingFiles = 0
        $identicalFiles = 0
        $differentFiles = 0
        $extraFiles = 0
        $missingRelativePaths = New-Object 'System.Collections.Generic.List[string]'
        $identicalRelativePaths = New-Object 'System.Collections.Generic.List[string]'
        $extraRelativePaths = New-Object 'System.Collections.Generic.List[string]'

        foreach ($relativePath in $englishIndex.Keys) {
            if (-not $languageIndex.ContainsKey($relativePath)) {
                $missingFiles++
                $missingRelativePaths.Add($relativePath) | Out-Null
                continue
            }

            $languageHash = Get-FileHashString -Path $languageIndex[$relativePath]
            if ($languageHash -eq $englishIndex[$relativePath]) {
                $identicalFiles++
                $identicalRelativePaths.Add($relativePath) | Out-Null
            }
            else {
                $differentFiles++
            }
        }

        foreach ($relativePath in $languageIndex.Keys) {
            if (-not $englishIndex.ContainsKey($relativePath)) {
                $extraFiles++
                $extraRelativePaths.Add($relativePath) | Out-Null
            }
        }

        $status = if ($differentFiles -eq 0 -and $missingFiles -eq 0 -and $identicalFiles -eq $englishIndex.Count) {
            'english-mirror'
        }
        elseif ($differentFiles -eq 0 -and $identicalFiles -gt 0 -and $missingFiles -gt 0) {
            'english-partial'
        }
        elseif ($differentFiles -gt 0 -and $missingFiles -eq 0) {
            'translated-complete-files'
        }
        elseif ($differentFiles -gt 0 -and $missingFiles -gt 0) {
            'translated-incomplete-files'
        }
        else {
            'empty-or-other'
        }

        $results.Add([pscustomobject]@{
                Part = $part
                Language = $language.Name
                EnglishFiles = $englishIndex.Count
                LanguageFiles = $languageFiles.Count
                MissingFiles = $missingFiles
                IdenticalFiles = $identicalFiles
                DifferentFiles = $differentFiles
                ExtraFiles = $extraFiles
                CoverageComplete = ($missingFiles -eq 0)
                Status = $status
                MissingByPattern = @(Get-PatternSummary -RelativePaths $missingRelativePaths.ToArray())
                IdenticalByPattern = @(Get-PatternSummary -RelativePaths $identicalRelativePaths.ToArray())
                ExtraByPattern = @(Get-PatternSummary -RelativePaths $extraRelativePaths.ToArray())
                MissingSamples = @($missingRelativePaths | Sort-Object | Select-Object -First 10)
                IdenticalSamples = @($identicalRelativePaths | Sort-Object | Select-Object -First 10)
                ExtraSamples = @($extraRelativePaths | Sort-Object | Select-Object -First 10)
            })
    }
}

$packSummary = @($results | Group-Object Part | Sort-Object Name | ForEach-Object {
        $group = $_.Group
        [pscustomobject]@{
            Part = $_.Name
            Languages = $group.Count
            CompleteCoverage = @($group | Where-Object { $_.CoverageComplete }).Count
            CompleteTranslated = @($group | Where-Object { $_.Status -eq 'translated-complete-files' }).Count
            IncompleteTranslated = @($group | Where-Object { $_.Status -eq 'translated-incomplete-files' }).Count
            EnglishMirror = @($group | Where-Object { $_.Status -eq 'english-mirror' }).Count
            EnglishPartial = @($group | Where-Object { $_.Status -eq 'english-partial' }).Count
        }
    })

$mirrorByPart = @($results | Where-Object { $_.Status -eq 'english-mirror' } | Group-Object Part | Sort-Object Name | ForEach-Object {
        [pscustomobject]@{
            Part = $_.Name
            Languages = @($_.Group | Sort-Object Language | Select-Object -ExpandProperty Language)
        }
    })

$allMirrorLanguages = @($results | Group-Object Language | Where-Object {
        $_.Count -eq $parts.Count -and @($_.Group | Where-Object { $_.Status -eq 'english-mirror' }).Count -eq $parts.Count
    } | Sort-Object Name | Select-Object -ExpandProperty Name)

$incompleteByPart = @($results | Where-Object { $_.MissingFiles -gt 0 } | Group-Object Part | Sort-Object Name | ForEach-Object {
        [pscustomobject]@{
            Part = $_.Name
            Languages = @($_.Group | Sort-Object Language | Select-Object Language, MissingFiles, DifferentFiles, Status)
        }
    })

$payload = [pscustomobject]@{
    GeneratedAt = (Get-Date).ToString('s')
    PackSummary = $packSummary
    MirrorByPart = $mirrorByPart
    AllMirrorLanguages = $allMirrorLanguages
    IncompleteByPart = $incompleteByPart
    Results = $results
}

$payload | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $ReportPath -Encoding UTF8

Write-Host '=== Pack Summary ==='
$packSummary | Format-Table -AutoSize

Write-Host ''
Write-Host '=== English Mirror Languages ==='
if ($mirrorByPart.Count -eq 0) {
    Write-Host 'None'
}
else {
    foreach ($entry in $mirrorByPart) {
        Write-Host ('{0}: {1}' -f $entry.Part, ($entry.Languages -join ', '))
    }
}

Write-Host ''
Write-Host '=== Languages That Are English Mirrors In All 4 Parts ==='
if ($allMirrorLanguages.Count -eq 0) {
    Write-Host 'None'
}
else {
    $allMirrorLanguages | ForEach-Object { Write-Host $_ }
}

Write-Host ''
Write-Host ('Report written: {0}' -f $ReportPath)