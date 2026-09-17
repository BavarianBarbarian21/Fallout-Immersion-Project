[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$YesManExportRoot = '',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptRoot '..\..'))
$repoPrefix = $repoRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if ([string]::IsNullOrWhiteSpace($YesManExportRoot)) {
    $local = [Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)
    $localLow = [System.IO.Path]::GetFullPath((Join-Path $local '..\LocalLow'))
    $YesManExportRoot = Join-Path $localLow 'Ludeon Studios\RimWorld by Ludeon Studios\Config\FIP-YesMan\Exports'
}

$sourceEnglish = [System.IO.Path]::GetFullPath((Join-Path $YesManExportRoot 'FIP-English Language Pack'))
$repoEnglish = [System.IO.Path]::GetFullPath((Join-Path $repoRoot 'FIP-English Language Pack'))
if (-not $repoEnglish.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to write outside the repository: $repoEnglish"
}
if (-not [System.IO.File]::Exists((Join-Path $sourceEnglish 'About\About.xml'))) {
    throw "FIP - English Language Pack was not found at '$sourceEnglish'. Complete both Yes Man exports and build the English baseline first."
}

$translationMods = @(
    [pscustomobject]@{
        Folder = 'FIP-German Language Pack'
        PackageId = 'FIP.Translation.German'
        DisplayName = 'FIP - German Language Pack'
        Description = 'German language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('German')
    },
    [pscustomobject]@{
        Folder = 'FIP-Spanish Language Pack'
        PackageId = 'FIP.Translation.Spanish'
        DisplayName = 'FIP - Spanish Language Pack'
        Description = 'Spanish language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('Spanish')
    },
    [pscustomobject]@{
        Folder = 'FIP-French Language Pack'
        PackageId = 'FIP.Translation.French'
        DisplayName = 'FIP - French Language Pack'
        Description = 'French language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('French')
    },
    [pscustomobject]@{
        Folder = 'FIP-Brazilian Portuguese Language Pack'
        PackageId = 'FIP.Translation.PortugueseBrazilian'
        DisplayName = 'FIP - Brazilian Portuguese Language Pack'
        Description = 'Brazilian Portuguese language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('PortugueseBrazilian')
    },
    [pscustomobject]@{
        Folder = 'FIP-Polish Language Pack'
        PackageId = 'FIP.Translation.Polish'
        DisplayName = 'FIP - Polish Language Pack'
        Description = 'Polish language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('Polish')
    },
    [pscustomobject]@{
        Folder = 'FIP-Italian Language Pack'
        PackageId = 'FIP.Translation.Italian'
        DisplayName = 'FIP - Italian Language Pack'
        Description = 'Italian language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('Italian')
    },
    [pscustomobject]@{
        Folder = 'FIP-Ukrainian Language Pack'
        PackageId = 'FIP.Translation.Ukrainian'
        DisplayName = 'FIP - Ukrainian Language Pack'
        Description = 'Ukrainian language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('Ukrainian')
    },
    [pscustomobject]@{
        Folder = 'FIP-Dutch Language Pack'
        PackageId = 'FIP.Translation.Dutch'
        DisplayName = 'FIP - Dutch Language Pack'
        Description = 'Dutch language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('Dutch')
    },
    [pscustomobject]@{
        Folder = 'FIP-Czech Language Pack'
        PackageId = 'FIP.Translation.Czech'
        DisplayName = 'FIP - Czech Language Pack'
        Description = 'Czech language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('Czech')
    },
    [pscustomobject]@{
        Folder = 'FIP-Japanese Language Pack'
        PackageId = 'FIP.Translation.Japanese'
        DisplayName = 'FIP - Japanese Language Pack'
        Description = 'Japanese language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('Japanese')
    },
    [pscustomobject]@{
        Folder = 'FIP-Korean Language Pack'
        PackageId = 'FIP.Translation.Korean'
        DisplayName = 'FIP - Korean Language Pack'
        Description = 'Korean language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('Korean')
    },
    [pscustomobject]@{
        Folder = 'FIP-Simplified Chinese Language Pack'
        PackageId = 'FIP.Translation.ChineseSimplified'
        DisplayName = 'FIP - Simplified Chinese Language Pack'
        Description = 'Simplified Chinese language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('ChineseSimplified')
    },
    [pscustomobject]@{
        Folder = 'FIP-Traditional Chinese Language Pack'
        PackageId = 'FIP.Translation.ChineseTraditional'
        DisplayName = 'FIP - Traditional Chinese Language Pack'
        Description = 'Traditional Chinese language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('ChineseTraditional')
    },
    [pscustomobject]@{
        Folder = 'FIP-Russian Language Pack'
        PackageId = 'FIP.Translation.Russian'
        DisplayName = 'FIP - Russian Language Pack'
        Description = 'Russian language pack for the Fallout Immersion Project reference mod list. Generated from the canonical FIP - English Language Pack.'
        Languages = @('Russian')
    }
)

function Assert-RepositoryTarget {
    param([Parameter(Mandatory = $true)][string]$Path)
    if (-not $Path.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to write outside the repository: $Path"
    }
}

function Copy-CleanDirectory {
    param(
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Destination,
        [Parameter(Mandatory = $true)][string]$Label
    )

    Assert-RepositoryTarget -Path $Destination
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
            [System.IO.File]::Copy($file, (Join-Path $Destination $relative), $true)
        }
    }
}

function Save-TranslationAbout {
    param(
        [Parameter(Mandatory = $true)][string]$ModRoot,
        [Parameter(Mandatory = $true)]$Specification
    )

    $aboutPath = Join-Path $ModRoot 'About\About.xml'
    [xml]$about = [System.IO.File]::ReadAllText($aboutPath, [System.Text.Encoding]::UTF8)
    $about.ModMetaData.packageId = $Specification.PackageId
    $about.ModMetaData.name = $Specification.DisplayName
    $about.ModMetaData.description = $Specification.Description

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
}

Copy-CleanDirectory -Source $sourceEnglish -Destination $repoEnglish -Label 'FIP - English Language Pack'

foreach ($specification in $translationMods) {
    $target = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $specification.Folder))
    Copy-CleanDirectory -Source $repoEnglish -Destination $target -Label $specification.Folder

    $englishLanguage = Join-Path $target 'Languages\English'
    if (-not [System.IO.Directory]::Exists($englishLanguage)) {
        throw "The copied English source has no Languages\English directory: $repoEnglish"
    }

    foreach ($language in $specification.Languages) {
        $languageTarget = Join-Path $target (Join-Path 'Languages' $language)
        if ($PSCmdlet.ShouldProcess($languageTarget, "Create Languages\$language from English")) {
            [System.IO.Directory]::CreateDirectory($languageTarget) | Out-Null
            foreach ($directory in [System.IO.Directory]::GetDirectories($englishLanguage, '*', [System.IO.SearchOption]::AllDirectories)) {
                $relative = $directory.Substring($englishLanguage.Length).TrimStart('\', '/')
                [System.IO.Directory]::CreateDirectory((Join-Path $languageTarget $relative)) | Out-Null
            }
            foreach ($file in [System.IO.Directory]::GetFiles($englishLanguage, '*', [System.IO.SearchOption]::AllDirectories)) {
                $relative = $file.Substring($englishLanguage.Length).TrimStart('\', '/')
                [System.IO.File]::Copy($file, (Join-Path $languageTarget $relative), $true)
            }
        }
    }

    if ($PSCmdlet.ShouldProcess($englishLanguage, 'Remove English directory from release translation copy')) {
        [System.IO.Directory]::Delete($englishLanguage, $true)
    }
    Save-TranslationAbout -ModRoot $target -Specification $specification
    Write-Host "Created $($specification.Folder): $($specification.Languages -join ', ')" -ForegroundColor Green
}

Write-Host "FIP - English Language Pack synchronized to: $repoEnglish" -ForegroundColor Green
Write-Host 'Every copied language value is intentionally still English.' -ForegroundColor Yellow
