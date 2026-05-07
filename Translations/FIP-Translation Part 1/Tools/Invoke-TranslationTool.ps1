<#
.SYNOPSIS
    FIP Translation Pack multi-language generator.

.DESCRIPTION
    Reads English source files from all 3 packs, applies translations from
    translations\<lang>.json, and writes translated files to Languages\<lang>\.

    Commands:
      generate   Read English sources + translation JSON, write target language files.
                 If no translation JSON exists for a language, English text is used as fallback.
      validate   Check generated files for XML well-formedness, key completeness,
                 and placeholder integrity.

.PARAMETER Command
    One of: generate, validate

.PARAMETER Languages
    Optional list of language names to process. Defaults to all 28.

.EXAMPLE
    .\Invoke-TranslationTool.ps1 -Command generate
    .\Invoke-TranslationTool.ps1 -Command generate -Languages German,French
    .\Invoke-TranslationTool.ps1 -Command validate -Languages German
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('generate','validate')]
    [string]$Command,

    [string[]]$Languages
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# -- paths -----------------------------------------------------------------

$ScriptDir     = $PSScriptRoot
$RepoRoot      = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$TransDir      = Join-Path $ScriptDir 'translations'

$PackDefs = [ordered]@{
    Part1 = Join-Path $RepoRoot 'FIP-Translation Part 1'
    Part2 = Join-Path $RepoRoot 'FIP-Translation Part 2'
    Part3 = Join-Path $RepoRoot 'FIP-Translation Part 3'
}

$AllLanguages = @(
    'Catalan','ChineseSimplified','ChineseTraditional','Czech',
    'Danish','Dutch','Estonian','Finnish','French','German',
    'Greek','Hungarian','Italian','Japanese','Korean','Norwegian',
    'Polish','Portuguese','PortugueseBrazilian','Romanian',
    'Russian','Slovak','Spanish','SpanishLatin','Swedish',
    'Turkish','Ukrainian','Vietnamese'
)

if (-not $PSBoundParameters.ContainsKey('Languages') -or $Languages.Count -eq 0) {
    $Languages = $AllLanguages
}

$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

# -- helpers ----------------------------------------------------------------

function Ensure-Dir([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        $null = New-Item -Path $Path -ItemType Directory -Force
    }
}

function Load-TranslationMap([string]$Lang) {
    # Loads translations from:
    #   translations/<Lang>.json                    (single file)
    #   translations/<Lang>/Part1.json etc.         (per-pack splits)
    #   translations/<Lang>/Part1_keyed.json etc.   (per-pack-per-category)
    $map = @{}
    $files = @()

    $single = Join-Path $TransDir "$Lang.json"
    if (Test-Path -LiteralPath $single) { $files += $single }

    $langSubDir = Join-Path $TransDir $Lang
    if (Test-Path -LiteralPath $langSubDir) {
        $files += @(Get-ChildItem -LiteralPath $langSubDir -Filter '*.json' -Recurse | ForEach-Object { $_.FullName })
    }

    foreach ($file in $files) {
        $raw = [System.IO.File]::ReadAllText($file, $Utf8NoBom)
        $obj = $raw | ConvertFrom-Json
        foreach ($prop in ($obj | Get-Member -MemberType NoteProperty)) {
            $map[$prop.Name] = $obj.($prop.Name)
        }
    }
    return $map
}

function Process-XmlFile {
    param(
        [string]$SourcePath,
        [string]$DestPath,
        [string]$PackId,
        [string]$RelPath,
        [hashtable]$TransMap
    )

    $raw = [System.IO.File]::ReadAllText($SourcePath, [System.Text.Encoding]::UTF8)

    # Use regex to replace element inner text while preserving XML structure
    #   Pattern: <KeyName>...text...</KeyName>
    # But we need to be careful with elements that have inner XML tags
    # Safer approach: parse with XmlDocument, modify InnerXml, write back

    $doc = [System.Xml.XmlDocument]::new()
    $doc.PreserveWhitespace = $true
    $doc.Load($SourcePath)

    $root = $doc.DocumentElement
    if ($null -eq $root) { return }

    $changed = $false
    foreach ($node in $root.ChildNodes) {
        if ($node.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
        $sid = "$PackId|$RelPath|$($node.LocalName)"
        if ($TransMap.ContainsKey($sid)) {
            $node.InnerXml = $TransMap[$sid]
            $changed = $true
        }
    }

    Ensure-Dir (Split-Path -Parent $DestPath)

    if ($changed) {
        $settings = [System.Xml.XmlWriterSettings]::new()
        $settings.Encoding = $Utf8NoBom
        $settings.Indent = $false  # preserve original whitespace
        $settings.OmitXmlDeclaration = $false
        $writer = [System.Xml.XmlWriter]::Create($DestPath, $settings)
        try { $doc.Save($writer) } finally { $writer.Close() }
    } else {
        # No translations for this file -- copy English verbatim
        [System.IO.File]::Copy($SourcePath, $DestPath, $true)
    }
}

function Process-NamesFile {
    param(
        [string]$SourcePath,
        [string]$DestPath,
        [string]$PackId,
        [string]$RelPath,
        [hashtable]$TransMap
    )

    $lines = [System.IO.File]::ReadAllLines($SourcePath, [System.Text.Encoding]::UTF8)
    $outLines = [System.Collections.Generic.List[string]]::new()
    $idx = 0
    $changed = $false

    foreach ($line in $lines) {
        if ($line.Trim() -eq '') { $outLines.Add($line); continue }
        $sid = "$PackId|$RelPath|$idx"
        if ($TransMap.ContainsKey($sid)) {
            $outLines.Add($TransMap[$sid])
            $changed = $true
        } else {
            $outLines.Add($line)
        }
        $idx++
    }

    Ensure-Dir (Split-Path -Parent $DestPath)

    if ($changed) {
        [System.IO.File]::WriteAllLines($DestPath, $outLines.ToArray(), $Utf8NoBom)
    } else {
        [System.IO.File]::Copy($SourcePath, $DestPath, $true)
    }
}

# -- generate ---------------------------------------------------------------

function Invoke-Generate {
    Write-Host "Generating translations..."

    foreach ($lang in $Languages) {
        Write-Host "  Loading $lang..." -NoNewline
        $transMap = Load-TranslationMap $lang
        $appliedCount = $transMap.Count
        $totalFiles = 0

        foreach ($packId in $PackDefs.Keys) {
            $packPath = $PackDefs[$packId]
            $engDir = Join-Path $packPath 'Languages\English'
            $langDir = Join-Path $packPath "Languages\$lang"

            if (-not (Test-Path -LiteralPath $engDir)) { continue }

            # -- Keyed --
            $keyedDir = Join-Path $engDir 'Keyed'
            if (Test-Path -LiteralPath $keyedDir) {
                foreach ($f in Get-ChildItem -LiteralPath $keyedDir -Filter '*.xml') {
                    $rel = $f.FullName.Substring($engDir.Length + 1).Replace('\', '/')
                    $dest = Join-Path $langDir $rel.Replace('/', '\')
                    Process-XmlFile -SourcePath $f.FullName -DestPath $dest `
                                    -PackId $packId -RelPath $rel -TransMap $transMap
                    $totalFiles++
                }
            }

            # -- DefInjected --
            $diDir = Join-Path $engDir 'DefInjected'
            if (Test-Path -LiteralPath $diDir) {
                foreach ($sub in Get-ChildItem -LiteralPath $diDir -Directory) {
                    foreach ($f in Get-ChildItem -LiteralPath $sub.FullName -Filter '*.xml') {
                        $rel = $f.FullName.Substring($engDir.Length + 1).Replace('\', '/')
                        $dest = Join-Path $langDir $rel.Replace('/', '\')
                        Process-XmlFile -SourcePath $f.FullName -DestPath $dest `
                                        -PackId $packId -RelPath $rel -TransMap $transMap
                        $totalFiles++
                    }
                }
            }

            # -- Names --
            $namesDir = Join-Path $engDir 'Strings\Names'
            if (Test-Path -LiteralPath $namesDir) {
                foreach ($f in Get-ChildItem -LiteralPath $namesDir -Filter '*.txt') {
                    $rel = $f.FullName.Substring($engDir.Length + 1).Replace('\', '/')
                    $dest = Join-Path $langDir $rel.Replace('/', '\')
                    Process-NamesFile -SourcePath $f.FullName -DestPath $dest `
                                      -PackId $packId -RelPath $rel -TransMap $transMap
                    $totalFiles++
                }
            }
        }

        Write-Host " $totalFiles files ($appliedCount translated entries)"
    }

    Write-Host "Done."
}

# -- validate ---------------------------------------------------------------

function Invoke-Validate {
    Write-Host "Validating translations..."
    $placeholderRe = [regex]'\{[^}]+\}'
    $errorList = [System.Collections.Generic.List[string]]::new()
    $warnList = [System.Collections.Generic.List[string]]::new()
    $checked = 0

    foreach ($lang in $Languages) {
        foreach ($packId in $PackDefs.Keys) {
            $packPath = $PackDefs[$packId]
            $engDir = Join-Path $packPath 'Languages\English'
            $langDir = Join-Path $packPath "Languages\$lang"

            if (-not (Test-Path -LiteralPath $engDir)) { continue }
            if (-not (Test-Path -LiteralPath $langDir)) {
                $warnList.Add("$packId/${lang}: language directory missing")
                continue
            }

            # -- XML files --
            foreach ($category in @('Keyed', 'DefInjected')) {
                $engCatDir = Join-Path $engDir $category
                if (-not (Test-Path -LiteralPath $engCatDir)) { continue }

                $xmlFiles = if ($category -eq 'Keyed') {
                    Get-ChildItem -LiteralPath $engCatDir -Filter '*.xml'
                } else {
                    Get-ChildItem -LiteralPath $engCatDir -Recurse -Filter '*.xml'
                }

                foreach ($ef in $xmlFiles) {
                    $rel = $ef.FullName.Substring($engDir.Length + 1)
                    $tf = Join-Path $langDir $rel

                    if (-not (Test-Path -LiteralPath $tf)) {
                        $errorList.Add("MISSING: $packId/$lang/$($rel.Replace('\','/'))")
                        continue
                    }
                    $checked++

                    # Well-formedness
                    try {
                        $tdoc = [System.Xml.XmlDocument]::new()
                        $tdoc.Load($tf)
                    } catch {
                        $errorList.Add("XML ERROR: $packId/$lang/$rel - $_")
                        continue
                    }

                    # Key completeness
                    $edoc = [System.Xml.XmlDocument]::new()
                    $edoc.Load($ef.FullName)

                    $engKeys = [System.Collections.Generic.HashSet[string]]::new()
                    foreach ($n in $edoc.DocumentElement.ChildNodes) {
                        if ($n.NodeType -eq [System.Xml.XmlNodeType]::Element) {
                            $null = $engKeys.Add($n.LocalName)
                        }
                    }
                    $transKeys = [System.Collections.Generic.HashSet[string]]::new()
                    foreach ($n in $tdoc.DocumentElement.ChildNodes) {
                        if ($n.NodeType -eq [System.Xml.XmlNodeType]::Element) {
                            $null = $transKeys.Add($n.LocalName)
                        }
                    }
                    $missing = @($engKeys | Where-Object { -not $transKeys.Contains($_) })
                    if ($missing.Count -gt 0) {
                        $errorList.Add("MISSING KEYS $packId/$lang/$($rel.Replace('\','/')): $($missing -join ', ')")
                    }

                    # Placeholder integrity
                    foreach ($en in $edoc.DocumentElement.ChildNodes) {
                        if ($en.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
                        $engPH = @($placeholderRe.Matches($en.InnerXml) | ForEach-Object { $_.Value } | Sort-Object -Unique)
                        if ($engPH.Count -eq 0) { continue }
                        $tn = $tdoc.DocumentElement.SelectSingleNode($en.LocalName)
                        if ($null -eq $tn) { continue }
                        $transPH = @($placeholderRe.Matches($tn.InnerXml) | ForEach-Object { $_.Value } | Sort-Object -Unique)
                        $diff = Compare-Object $engPH $transPH -PassThru -ErrorAction SilentlyContinue
                        if ($diff) {
                            $errorList.Add("PLACEHOLDER $packId/$lang/$($rel.Replace('\','/'))/$($en.LocalName)")
                        }
                    }
                }
            }

            # -- Names --
            $engNamesDir = Join-Path $engDir 'Strings\Names'
            if (Test-Path -LiteralPath $engNamesDir) {
                foreach ($ef in Get-ChildItem -LiteralPath $engNamesDir -Filter '*.txt') {
                    $rel = $ef.FullName.Substring($engDir.Length + 1)
                    $tf = Join-Path $langDir $rel
                    if (-not (Test-Path -LiteralPath $tf)) {
                        $errorList.Add("MISSING: $packId/$lang/$($rel.Replace('\','/'))")
                        continue
                    }
                    $checked++
                    $ec = @([System.IO.File]::ReadAllLines($ef.FullName, [System.Text.Encoding]::UTF8) | Where-Object { $_.Trim() -ne '' }).Count
                    $tc = @([System.IO.File]::ReadAllLines($tf, [System.Text.Encoding]::UTF8) | Where-Object { $_.Trim() -ne '' }).Count
                    if ($ec -ne $tc) {
                        $errorList.Add("NAME COUNT $packId/$lang/$($rel.Replace('\','/')): eng=$ec trans=$tc")
                    }
                }
            }
        }
    }

    Write-Host "Validated $checked files across $($Languages.Count) languages"

    if ($warnList.Count -gt 0) {
        Write-Host "`nWarnings ($($warnList.Count)):"
        $warnList | Select-Object -First 20 | ForEach-Object { Write-Host "  WARN: $_" }
        if ($warnList.Count -gt 20) { Write-Host "  ... and $($warnList.Count - 20) more" }
    }

    if ($errorList.Count -gt 0) {
        Write-Host "`nErrors ($($errorList.Count)):" -ForegroundColor Red
        $errorList | Select-Object -First 30 | ForEach-Object { Write-Host "  ERR: $_" -ForegroundColor Red }
        if ($errorList.Count -gt 30) { Write-Host "  ... and $($errorList.Count - 30) more" -ForegroundColor Red }
        exit 1
    } else {
        Write-Host "`nAll checks passed." -ForegroundColor Green
    }
}

# -- dispatch ---------------------------------------------------------------

switch ($Command) {
    'generate' { Invoke-Generate }
    'validate' { Invoke-Validate }
}
