[CmdletBinding()]
param()

$translationRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
& (Join-Path $translationRoot 'Tools\Invoke-FipTranslationUpdater.ps1') -Category part3 -Mode LanguageTranslation