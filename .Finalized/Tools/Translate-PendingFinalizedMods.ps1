param(
    [string]$FinalizedRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$TranslateRoot = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path -Parent) '.Translate'),
    [string[]]$ModsFilter,
    [string[]]$LocalesFilter,
    [int]$MaxPasses = 1,
    [string]$FailureLogPath = (Join-Path $PSScriptRoot 'Translate-PendingFinalizedMods.failures.log'),
    [switch]$SkipValidation
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Generate-RequestedLocalizations.ps1') -LibraryMode
$repairScriptPath = Join-Path $PSScriptRoot 'Repair-FinalizedTranslations.ps1'

function Test-NeedsTranslation {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceFile,

        [Parameter(Mandatory = $true)]
        [string]$TargetFile
    )

    if (-not (Test-Path -LiteralPath $TargetFile)) {
        return $true
    }

    $sourceHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $SourceFile).Hash
    $targetHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $TargetFile).Hash
    $sourceHash -eq $targetHash
}

function Invoke-FileTranslation {
    param(
        [Parameter(Mandatory = $true)]
        [System.IO.FileInfo]$EnglishFile,

        [Parameter(Mandatory = $true)]
        [string]$TargetFile,

        [Parameter(Mandatory = $true)]
        [string]$Locale
    )

    if ($EnglishFile.Extension -eq '.xml') {
        Translate-LanguageDataFile -SourceFile $EnglishFile.FullName -TargetFile $TargetFile -Locale $Locale
        return
    }

    Translate-TextFile -SourceFile $EnglishFile.FullName -TargetFile $TargetFile -Locale $Locale
}

$selectedLocales = if ($LocalesFilter) {
    @($locales | Where-Object { $_ -in $LocalesFilter })
}
else {
    $locales
}

$mods = Get-ChildItem -Path $FinalizedRoot -Directory | Where-Object { $_.Name -ne 'Tools' }
if ($ModsFilter) {
    $mods = @($mods | Where-Object { $_.Name -in $ModsFilter })
}

$summary = New-Object System.Collections.Generic.List[object]
$failures = New-Object System.Collections.Generic.List[object]

if (Test-Path -LiteralPath $FailureLogPath) {
    Remove-Item -LiteralPath $FailureLogPath -Force
}

foreach ($mod in $mods) {
    $englishRoot = Join-Path $mod.FullName 'Languages\English'
    if (-not (Test-Path -LiteralPath $englishRoot)) {
        Write-Host "Skipping $($mod.Name): no English localization root"
        continue
    }

    $englishFiles = @(Get-ChildItem -Path $englishRoot -Recurse -File | Where-Object { $_.Extension -in '.xml', '.txt' })
    if (-not $englishFiles.Count) {
        Write-Host "Skipping $($mod.Name): no English localization files"
        continue
    }

    $translatedFiles = 0
    Write-Host "Processing $($mod.Name) ($($englishFiles.Count) English files)"

    for ($pass = 1; $pass -le $MaxPasses; $pass += 1) {
        $passTranslatedFiles = 0

        foreach ($locale in $selectedLocales) {
            $localeRoot = Join-Path $mod.FullName (Join-Path 'Languages' $locale)
            $translatedLocaleRoot = Join-Path $TranslateRoot (Join-Path $mod.Name (Join-Path 'Languages' $locale))
            $existingTranslationSet = Get-ExistingTranslationSet -SourceRoot $translatedLocaleRoot
            $localeTranslatedFiles = 0

            foreach ($englishFile in $englishFiles) {
                $relativePath = Get-RelativePath -BasePath $englishRoot -FullPath $englishFile.FullName
                if ($existingTranslationSet.ContainsKey($relativePath)) {
                    continue
                }

                $targetFile = Join-Path $localeRoot $relativePath
                if (-not (Test-NeedsTranslation -SourceFile $englishFile.FullName -TargetFile $targetFile)) {
                    continue
                }

                try {
                    Invoke-FileTranslation -EnglishFile $englishFile -TargetFile $targetFile -Locale $locale
                    $translatedFiles += 1
                    $passTranslatedFiles += 1
                    $localeTranslatedFiles += 1
                }
                catch {
                    $failure = [pscustomobject]@{
                        Mod = $mod.Name
                        Locale = $locale
                        File = $relativePath
                        Error = $_.Exception.Message
                    }
                    $failures.Add($failure) | Out-Null
                    Add-Content -LiteralPath $FailureLogPath -Value ("{0}`t{1}`t{2}`t{3}" -f $failure.Mod, $failure.Locale, $failure.File, $failure.Error)
                }
            }

            if ($localeTranslatedFiles -gt 0) {
                Write-Host "  pass $pass [$locale] translated $localeTranslatedFiles files"
            }
        }

        if ($passTranslatedFiles -eq 0) {
            break
        }
    }

    $summary.Add([pscustomobject]@{
        Mod = $mod.Name
        TranslatedFiles = $translatedFiles
    }) | Out-Null
}

$summary | Format-Table -AutoSize

if ($failures.Count -gt 0) {
    Write-Host ''
    Write-Host 'Failures:'
    $failures | Format-Table -AutoSize
    Write-Host "Failure log: $FailureLogPath"
}

if (-not $SkipValidation) {
    & $repairScriptPath -FinalizedRoot $FinalizedRoot -ModsFilter $ModsFilter -LocalesFilter $LocalesFilter -Apply
    & $repairScriptPath -FinalizedRoot $FinalizedRoot -ModsFilter $ModsFilter -LocalesFilter $LocalesFilter -FailOnIssues
}