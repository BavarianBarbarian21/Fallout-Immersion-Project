param(
    [Parameter(Mandatory = $true)]
    [string]$ModName,
    [string]$FinalizedRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$TranslateRoot = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path -Parent) '.Translate'),
    [string[]]$LocalesFilter
)

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'Generate-RequestedLocalizations.ps1') -LibraryMode

$mod = Get-Item (Join-Path $FinalizedRoot $ModName)
$englishRoot = Join-Path (Join-Path $mod.FullName 'Languages') 'English'

if (-not (Test-Path -LiteralPath $englishRoot)) {
    throw "No English language root found for $ModName"
}

$englishFiles = @(Get-ChildItem -Path $englishRoot -Recurse -File | Where-Object { $_.Extension -in '.xml', '.txt' })
$selectedLocales = if ($LocalesFilter) { @($locales | Where-Object { $_ -in $LocalesFilter }) } else { $locales }

$translatedFiles = 0

foreach ($locale in $selectedLocales) {
    $localeRoot = Join-Path (Join-Path $mod.FullName 'Languages') $locale
    $translatedLocaleRoot = Join-Path (Join-Path (Join-Path $TranslateRoot $mod.Name) 'Languages') $locale
    $existingTranslationSet = Get-ExistingTranslationSet -SourceRoot $translatedLocaleRoot

    foreach ($englishFile in $englishFiles) {
        $relativePath = Get-RelativePath -BasePath $englishRoot -FullPath $englishFile.FullName
        if ($existingTranslationSet.ContainsKey($relativePath)) {
            continue
        }

        $targetFile = Join-Path $localeRoot $relativePath
        if ($englishFile.Extension -eq '.xml') {
            Translate-LanguageDataFile -SourceFile $englishFile.FullName -TargetFile $targetFile -Locale $locale
        }
        else {
            Translate-TextFile -SourceFile $englishFile.FullName -TargetFile $targetFile -Locale $locale
        }

        $translatedFiles += 1
    }
}

Write-Output "$ModName translated files: $translatedFiles"