param(
    [string]$TranslationRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$OutputRoot = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path -Parent) 'FIP-Translation Part 1')
)

$ErrorActionPreference = 'Stop'

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Reset-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        foreach ($child in @(Get-ChildItem -LiteralPath $Path -Force)) {
            if ($child.PSIsContainer) {
                try {
                    Remove-Item -LiteralPath $child.FullName -Recurse -Force -ErrorAction Stop
                }
                catch {
                    cmd.exe /d /c "if exist \"$($child.FullName)\" rd /s /q \"$($child.FullName)\"" | Out-Null
                    if (Test-Path -LiteralPath $child.FullName) {
                        throw
                    }
                }
                continue
            }

            Remove-Item -LiteralPath $child.FullName -Force
        }
        return
    }

    New-Item -ItemType Directory -Path $Path | Out-Null
}

function Get-RelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,

        [Parameter(Mandatory = $true)]
        [string]$FullPath
    )

    $base = [System.IO.Path]::GetFullPath($BasePath)
    if (-not $base.EndsWith([System.IO.Path]::DirectorySeparatorChar)) {
        $base += [System.IO.Path]::DirectorySeparatorChar
    }

    $baseUri = [System.Uri]$base
    $fullUri = [System.Uri]([System.IO.Path]::GetFullPath($FullPath))
    $relativeUri = $baseUri.MakeRelativeUri($fullUri)
    return [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('/', '\')
}

function Get-LanguageDataEntries {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $false
    $document.Load($FilePath)

    if (-not $document.DocumentElement -or $document.DocumentElement.Name -ne 'LanguageData') {
        throw "Unsupported XML format in $FilePath. Expected <LanguageData>."
    }

    $entries = New-Object 'System.Collections.Generic.List[System.Xml.XmlElement]'
    foreach ($childNode in @($document.DocumentElement.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })) {
        $entries.Add([System.Xml.XmlElement]$childNode.CloneNode($true)) | Out-Null
    }

    return $entries
}

function Merge-LanguageDataFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,

        [Parameter(Mandatory = $true)]
        [string]$SourcePath,

        [Parameter(Mandatory = $true)]
        [string]$SourceLabel,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[object]]$Conflicts
    )

    $sourceEntries = Get-LanguageDataEntries -FilePath $SourcePath

    if (-not (Test-Path -LiteralPath $DestinationPath)) {
        Ensure-Directory -Path (Split-Path -Parent $DestinationPath)
        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
        return
    }

    $destinationDocument = New-Object System.Xml.XmlDocument
    $destinationDocument.PreserveWhitespace = $false
    $destinationDocument.Load($DestinationPath)

    if (-not $destinationDocument.DocumentElement -or $destinationDocument.DocumentElement.Name -ne 'LanguageData') {
        throw "Unsupported XML format in $DestinationPath. Expected <LanguageData>."
    }

    $existingByName = @{}
    foreach ($childNode in @($destinationDocument.DocumentElement.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })) {
        if (-not $existingByName.ContainsKey($childNode.Name)) {
            $existingByName[$childNode.Name] = [System.Collections.Generic.List[System.Xml.XmlElement]]::new()
        }

        $existingByName[$childNode.Name].Add([System.Xml.XmlElement]$childNode) | Out-Null
    }

    foreach ($entry in $sourceEntries) {
        $matchingEntries = @()
        if ($existingByName.ContainsKey($entry.Name)) {
            $matchingEntries = @($existingByName[$entry.Name])
        }

        $alreadyPresent = $false
        foreach ($matchingEntry in $matchingEntries) {
            if ($matchingEntry.OuterXml -eq $entry.OuterXml) {
                $alreadyPresent = $true
                break
            }
        }

        if ($alreadyPresent) {
            continue
        }

        if ($matchingEntries.Count -gt 0) {
            $Conflicts.Add([pscustomobject]@{
                Type = 'LanguageDataKeyConflict'
                Destination = $DestinationPath
                Source = $SourcePath
                SourceLabel = $SourceLabel
                Key = $entry.Name
                Existing = $matchingEntries[0].OuterXml
                Incoming = $entry.OuterXml
            }) | Out-Null
            continue
        }

        $importedNode = $destinationDocument.ImportNode($entry, $true)
        $destinationDocument.DocumentElement.AppendChild($importedNode) | Out-Null

        if (-not $existingByName.ContainsKey($entry.Name)) {
            $existingByName[$entry.Name] = [System.Collections.Generic.List[System.Xml.XmlElement]]::new()
        }

        $existingByName[$entry.Name].Add([System.Xml.XmlElement]$importedNode) | Out-Null
    }

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.IndentChars = '    '
    $settings.NewLineChars = "`r`n"
    $settings.NewLineHandling = [System.Xml.NewLineHandling]::Replace
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)

    $writer = [System.Xml.XmlWriter]::Create($DestinationPath, $settings)
    try {
        $destinationDocument.Save($writer)
    }
    finally {
        $writer.Dispose()
    }
}

function Merge-TextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DestinationPath,

        [Parameter(Mandatory = $true)]
        [string]$SourcePath
    )

    if (-not (Test-Path -LiteralPath $DestinationPath)) {
        Ensure-Directory -Path (Split-Path -Parent $DestinationPath)
        Copy-Item -LiteralPath $SourcePath -Destination $DestinationPath -Force
        return
    }

    $existingLines = [System.Collections.Generic.List[string]]::new()
    $seen = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

    foreach ($path in @($DestinationPath, $SourcePath)) {
        foreach ($line in [System.IO.File]::ReadAllLines($path)) {
            if ($seen.Add($line)) {
                $existingLines.Add($line) | Out-Null
            }
        }
    }

    [System.IO.File]::WriteAllLines($DestinationPath, $existingLines, [System.Text.UTF8Encoding]::new($false))
}

function Write-Manifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,

        [Parameter(Mandatory = $true)]
        [string[]]$ModNames,

        [Parameter(Mandatory = $true)]
        [string[]]$Locales,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[object]]$Conflicts
    )

    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add('FIP-Translation Part 1 build manifest') | Out-Null
    $lines.Add('') | Out-Null
    $lines.Add('Source mods:') | Out-Null
    foreach ($modName in $ModNames) {
        $lines.Add("- $modName") | Out-Null
    }

    $lines.Add('') | Out-Null
    $lines.Add('Locales emitted:') | Out-Null
    foreach ($locale in $Locales) {
        $lines.Add("- $locale") | Out-Null
    }

    $lines.Add('') | Out-Null
    $lines.Add("Conflict count: $($Conflicts.Count)") | Out-Null
    foreach ($conflict in $Conflicts) {
        $lines.Add("- [$($conflict.Type)] $($conflict.Key) :: $($conflict.Destination) <= $($conflict.SourceLabel)") | Out-Null
    }

    Ensure-Directory -Path (Split-Path -Parent $ManifestPath)
    Set-Content -LiteralPath $ManifestPath -Value $lines -Encoding utf8
}

$sourceMods = @(Get-ChildItem -LiteralPath $TranslationRoot -Directory | Where-Object {
    Test-Path -LiteralPath (Join-Path $_.FullName 'Languages')
} | Sort-Object Name)

if (-not $sourceMods.Count) {
    throw "No source mods with Languages folders were found under $TranslationRoot."
}

$languagesRoot = Join-Path $OutputRoot 'Languages'
$manifestPath = Join-Path $OutputRoot 'BuildManifest.txt'
$conflicts = [System.Collections.Generic.List[object]]::new()
$localesEmitted = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

Ensure-Directory -Path $OutputRoot
Reset-Directory -Path $languagesRoot
Set-Content -LiteralPath $manifestPath -Value @(
    'FIP-Translation Part 1 build manifest',
    '',
    'Build status: in progress'
) -Encoding utf8

try {
    foreach ($mod in $sourceMods) {
        $modLanguagesRoot = Join-Path $mod.FullName 'Languages'
        $locales = @(Get-ChildItem -LiteralPath $modLanguagesRoot -Directory | Where-Object {
            $_.Name -ne 'English' -and $_.Name -ne 'Languages'
        } | Sort-Object Name)

        foreach ($locale in $locales) {
            $null = $localesEmitted.Add($locale.Name)
            $destinationLocaleRoot = Join-Path $languagesRoot $locale.Name

            foreach ($file in Get-ChildItem -LiteralPath $locale.FullName -Recurse -File | Sort-Object FullName) {
                $relativePath = Get-RelativePath -BasePath $locale.FullName -FullPath $file.FullName
                $destinationPath = Join-Path $destinationLocaleRoot $relativePath
                $sourceLabel = "$($mod.Name)/Languages/$($locale.Name)/$relativePath"

                if ($file.Extension -ieq '.xml') {
                    Merge-LanguageDataFile -DestinationPath $destinationPath -SourcePath $file.FullName -SourceLabel $sourceLabel -Conflicts $conflicts
                    continue
                }

                if ($file.Extension -ieq '.txt') {
                    Merge-TextFile -DestinationPath $destinationPath -SourcePath $file.FullName
                    continue
                }

                if (-not (Test-Path -LiteralPath $destinationPath)) {
                    Ensure-Directory -Path (Split-Path -Parent $destinationPath)
                    Copy-Item -LiteralPath $file.FullName -Destination $destinationPath -Force
                    continue
                }

                if ((Get-FileHash -Algorithm SHA256 -LiteralPath $destinationPath).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash) {
                    $conflicts.Add([pscustomobject]@{
                        Type = 'BinaryConflict'
                        Destination = $destinationPath
                        Source = $file.FullName
                        SourceLabel = $sourceLabel
                        Key = $relativePath
                    }) | Out-Null
                }
            }
        }
    }

    $localeList = @($localesEmitted | Sort-Object)

    Write-Manifest -ManifestPath $manifestPath -ModNames $sourceMods.Name -Locales $localeList -Conflicts $conflicts

    if ($conflicts.Count -gt 0) {
        Write-Warning "Build completed with $($conflicts.Count) conflicts. See $manifestPath for details."
    }
    else {
        Write-Host "Build completed successfully. Output: $OutputRoot"
    }
}
catch {
    Add-Content -LiteralPath $manifestPath -Value @(
        '',
        ('Build status: failed'),
        ("Failure: $($_.Exception.Message)")
    ) -Encoding utf8
    throw
}
