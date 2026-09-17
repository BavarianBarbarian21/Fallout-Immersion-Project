[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [ValidateNotNullOrEmpty()]
    [string]$Language = 'Japanese',

    [string]$TargetModFolder = '',

    [string]$PackageId = '',

    [string]$DisplayName = '',

    [string]$YesManExportRoot = '',

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..'))

if ([string]::IsNullOrWhiteSpace($YesManExportRoot)) {
    $localLow = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $localLow = [System.IO.Path]::GetFullPath((Join-Path $localLow '..\LocalLow'))
    $YesManExportRoot = Join-Path $localLow 'Ludeon Studios\RimWorld by Ludeon Studios\Config\FIP-YesMan\Exports'
}

$sourceEnglish = [System.IO.Path]::GetFullPath((Join-Path $YesManExportRoot 'FIP-English Language Pack'))
$repoEnglish = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'FIP-English Language Pack'))

if ([string]::IsNullOrWhiteSpace($TargetModFolder)) {
    $TargetModFolder = switch ($Language) {
        'ChineseSimplified' { 'FIP-Simplified Chinese Language Pack' }
        'ChineseTraditional' { 'FIP-Traditional Chinese Language Pack' }
        'PortugueseBrazilian' { 'FIP-Brazilian Portuguese Language Pack' }
        default { 'FIP-' + $Language + ' Language Pack' }
    }
}
if ([string]::IsNullOrWhiteSpace($PackageId)) {
    $PackageId = 'FIP.Translation.' + ($Language -replace '[^A-Za-z0-9_.-]', '')
}
if ([string]::IsNullOrWhiteSpace($DisplayName)) {
    $DisplayName = switch ($Language) {
        'ChineseSimplified' { 'FIP - Simplified Chinese Language Pack' }
        'ChineseTraditional' { 'FIP - Traditional Chinese Language Pack' }
        'PortugueseBrazilian' { 'FIP - Brazilian Portuguese Language Pack' }
        default { 'FIP - ' + $Language + ' Language Pack' }
    }
}

$targetMod = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $TargetModFolder))
foreach ($target in @($repoEnglish, $targetMod)) {
    $repoPrefix = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $target.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside the repository: $target"
    }
}

if (-not [System.IO.File]::Exists((Join-Path $sourceEnglish 'About\About.xml'))) {
    throw "FIP - English Language Pack was not found at '$sourceEnglish'. Complete both Yes Man exports and build the English baseline first."
}

function Copy-CleanDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [Parameter(Mandatory = $true)][string]$Label
    )

    if ([System.IO.Directory]::Exists($Destination)) {
        if (-not $Force) {
            throw "$Label already exists at '$Destination'. Use -Force to replace this generated directory."
        }
        if ($PSCmdlet.ShouldProcess($Destination, "Remove existing generated $Label")) {
            [System.IO.Directory]::Delete($Destination, $true)
        }
    }

    if ($PSCmdlet.ShouldProcess($Destination, "Create $Label from $Source")) {
        [System.IO.Directory]::CreateDirectory($Destination) | Out-Null
        foreach ($directory in [System.IO.Directory]::GetDirectories($Source, '*', [System.IO.SearchOption]::AllDirectories)) {
            $relative = $directory.Substring($Source.Length).TrimStart('\', '/')
            [System.IO.Directory]::CreateDirectory((Join-Path $Destination $relative)) | Out-Null
        }
        foreach ($file in [System.IO.Directory]::GetFiles($Source, '*', [System.IO.SearchOption]::AllDirectories)) {
            $relative = $file.Substring($Source.Length).TrimStart('\', '/')
            $destinationFile = Join-Path $Destination $relative
            [System.IO.File]::Copy($file, $destinationFile, $true)
        }
    }
}

Copy-CleanDirectory -Source $sourceEnglish -Destination $repoEnglish -Label 'FIP - English Language Pack'
Copy-CleanDirectory -Source $repoEnglish -Destination $targetMod -Label $TargetModFolder

$englishLanguage = Join-Path $targetMod 'Languages\English'
$targetLanguage = Join-Path $targetMod (Join-Path 'Languages' $Language)
if (-not [System.IO.Directory]::Exists($englishLanguage)) {
    throw "The copied English source has no Languages\English directory: $repoEnglish"
}
if ([System.IO.Directory]::Exists($targetLanguage)) {
    throw "The target language directory already exists unexpectedly: $targetLanguage"
}
if ($PSCmdlet.ShouldProcess($targetLanguage, "Rename Languages\English to Languages\$Language")) {
    [System.IO.Directory]::Move($englishLanguage, $targetLanguage)
}

$aboutPath = Join-Path $targetMod 'About\About.xml'
[xml]$about = [System.IO.File]::ReadAllText($aboutPath, [System.Text.Encoding]::UTF8)
$about.ModMetaData.packageId = $PackageId
$about.ModMetaData.name = $DisplayName
$about.ModMetaData.description = "$Language language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack."
$settings = [System.Xml.XmlWriterSettings]::new()
$settings.Encoding = $utf8NoBom
$settings.Indent = $true
$settings.IndentChars = '  '
$settings.NewLineChars = "`n"
$settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
$writer = [System.Xml.XmlWriter]::Create($aboutPath, $settings)
try {
    $about.Save($writer)
}
finally {
    $writer.Dispose()
}

Write-Host "FIP - English Language Pack synchronized to: $repoEnglish" -ForegroundColor Green
Write-Host "$Language working copy created at: $targetMod" -ForegroundColor Green
Write-Host 'The copied language values are intentionally still English.' -ForegroundColor Yellow
