param(
    [string]$FinalizedRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$TranslateRoot = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path -Parent) '.Translate'),
    [string[]]$ModsFilter,
    [string[]]$LocalesFilter
)

$ErrorActionPreference = 'Stop'

$logSuffix = if ($ModsFilter) { ($ModsFilter -join '_') } else { 'all' }
$logPath = Join-Path $PSScriptRoot ("Translate-FinalizedMods.$logSuffix.log")
Set-Content -LiteralPath $logPath -Value @('Runner start') -Encoding utf8

. (Join-Path $PSScriptRoot 'Generate-RequestedLocalizations.ps1') -FinalizedRoot $FinalizedRoot -TranslateRoot $TranslateRoot -LibraryMode

try {
    $mods = Get-ChildItem -Path $FinalizedRoot -Directory | Where-Object {
        $_.Name -ne 'Tools' -and
        ((-not $ModsFilter) -or ($_.Name -in $ModsFilter))
    }
    $selectedLocales = if ($LocalesFilter) { @($locales | Where-Object { $_ -in $LocalesFilter }) } else { $locales }

    foreach ($mod in $mods) {
        $modLanguagesRoot = Join-Path $mod.FullName 'Languages'
        $englishRoot = Join-Path $modLanguagesRoot 'English'
        $translateLanguagesRoot = Join-Path (Join-Path $TranslateRoot $mod.Name) 'Languages'

        if (-not (Test-Path -LiteralPath $englishRoot)) {
            Add-Content -LiteralPath $logPath -Value "$($mod.Name): no English root"
            Write-Host "$($mod.Name): no English language files"
            continue
        }

        $englishFiles = @(Get-ChildItem -Path $englishRoot -Recurse -File | Where-Object { $_.Extension -in '.xml', '.txt' })
        $translatedFileCount = 0

        foreach ($locale in $selectedLocales) {
            $localeRoot = Join-Path $modLanguagesRoot $locale
            $translatedLocaleRoot = Join-Path $translateLanguagesRoot $locale
            $existingTranslationSet = Get-ExistingTranslationSet -SourceRoot $translatedLocaleRoot
            Add-Content -LiteralPath $logPath -Value "$($mod.Name) [$locale]: englishFiles=$($englishFiles.Count)"

            foreach ($englishFile in $englishFiles) {
                $relativePath = Get-RelativePath -BasePath $englishRoot -FullPath $englishFile.FullName
                if ($existingTranslationSet.ContainsKey($relativePath)) {
                    Add-Content -LiteralPath $logPath -Value "SKIP $($mod.Name) [$locale] $relativePath"
                    continue
                }

                $targetFile = Join-Path $localeRoot $relativePath
                Add-Content -LiteralPath $logPath -Value "TRANSLATE $($mod.Name) [$locale] $relativePath"
                if ($englishFile.Extension -eq '.xml') {
                    Translate-LanguageDataFile -SourceFile $englishFile.FullName -TargetFile $targetFile -Locale $locale
                    $translatedFileCount += 1
                    continue
                }

                Translate-TextFile -SourceFile $englishFile.FullName -TargetFile $targetFile -Locale $locale
                $translatedFileCount += 1
            }
        }

        Add-Content -LiteralPath $logPath -Value "$($mod.Name): translated $translatedFileCount files"
        Write-Host "$($mod.Name): translated $translatedFileCount files"
    }

    Add-Content -LiteralPath $logPath -Value 'Runner complete'
}
catch {
    Add-Content -LiteralPath $logPath -Value 'ERROR:'
    Add-Content -LiteralPath $logPath -Value ($_ | Out-String)
    throw
}