<#
.SYNOPSIS
    FIP Translation Pack multi-language tool.

.DESCRIPTION
    Commands:
      extract          Scan all 3 packs' English/ dirs, write master_strings.json
      generate-skeleton Copy English files into all 28 language folders as baseline
      apply            Read translations/<lang>.json and update target language files
      validate         Check output files for XML well-formedness, key completeness,
                       and placeholder integrity

.PARAMETER Command
    One of: extract, generate-skeleton, apply, validate

.PARAMETER Languages
    Optional list of language names to process. Defaults to all 28.
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('extract','generate-skeleton','apply','validate')]
    [string]$Command,

    [string[]]$Languages
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# -- paths --------------------------------------------------------------------

$ScriptDir     = $PSScriptRoot
$RepoRoot      = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$ManifestPath  = Join-Path $ScriptDir 'master_strings.json'
$TransDir      = Join-Path $ScriptDir 'translations'

$PackDefs = [ordered]@{
    Part1 = Join-Path $RepoRoot 'FIP-Translation Part 1'
    Part2 = Join-Path $RepoRoot 'FIP-Translation Part 2'
    Part3 = Join-Path $RepoRoot 'FIP-Translation Part 3'
}

$AllLanguages = @(
    'Catalan'
    'ChineseSimplified'
    'ChineseTraditional'
    'Czech'
    'Danish'
    'Dutch'
    'Estonian'
    'Finnish'
    'French'
    'German'
    'Greek'
    'Hungarian'
    'Italian'
    'Japanese'
    'Korean'
    'Norwegian'
    'Polish'
    'Portuguese'
    'PortugueseBrazilian'
    'Romanian'
    'Russian'
    'Slovak'
    'Spanish'
    'SpanishLatin'
    'Swedish'
    'Turkish'
    'Ukrainian'
    'Vietnamese'
)

if (-not $PSBoundParameters.ContainsKey('Languages') -or $Languages.Count -eq 0) {
    $Languages = $AllLanguages
}

# -- helpers ------------------------------------------------------------------

function Ensure-Directory([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        $null = New-Item -Path $Path -ItemType Directory -Force
    }
}

function Read-XmlEntries([string]$FilePath) {
    <#
    .SYNOPSIS Read a LanguageData XML file, return array of @{Key; Text} objects.
    #>
    $entries = [System.Collections.Generic.List[hashtable]]::new()
    $comments = [System.Collections.Generic.List[string]]::new()

    [xml]$doc = Get-Content -LiteralPath $FilePath -Raw -Encoding UTF8
    $root = $doc.DocumentElement
    if ($null -eq $root) { return @{ Entries = $entries; Comments = $comments } }

    foreach ($node in $root.ChildNodes) {
        if ($node.NodeType -eq [System.Xml.XmlNodeType]::Comment) {
            $comments.Add($node.Value.Trim())
            continue
        }
        if ($node.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }

        $text = $node.InnerXml  # preserves child elements as raw XML
        $entries.Add(@{ Key = $node.LocalName; Text = $text })
    }
    return @{ Entries = $entries; Comments = $comments }
}

function Read-NamesFile([string]$FilePath) {
    <# Read a .txt names file, return array of name strings. #>
    $lines = Get-Content -LiteralPath $FilePath -Encoding UTF8
    return @($lines | Where-Object { $_.Trim() -ne '' })
}

function Write-XmlFile {
    param(
        [string]$FilePath,
        [System.Collections.Generic.List[hashtable]]$Entries,
        [System.Collections.Generic.List[string]]$Comments
    )
    Ensure-Directory (Split-Path -Parent $FilePath)

    $sb = [System.Text.StringBuilder]::new()
    $null = $sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    $null = $sb.AppendLine('<LanguageData>')

    if ($Comments -and $Comments.Count -gt 0) {
        foreach ($c in $Comments) {
            $null = $sb.AppendLine("  <!-- $c -->")
        }
        $null = $sb.AppendLine()
    }

    foreach ($e in $Entries) {
        $null = $sb.AppendLine("  <$($e.Key)>$($e.Text)</$($e.Key)>")
    }

    $null = $sb.AppendLine('</LanguageData>')

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($FilePath, $sb.ToString(), $utf8NoBom)
}

function Write-NamesFile {
    param([string]$FilePath, [string[]]$Names)
    Ensure-Directory (Split-Path -Parent $FilePath)
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($FilePath, ($Names -join "`n") + "`n", $utf8NoBom)
}

# -- extract ------------------------------------------------------------------

function Invoke-Extract {
    Write-Host "Extracting English strings from all packs..."

    $manifest = [ordered]@{
        packs = [ordered]@{}
        stats = [ordered]@{}
    }

    $totalStrings = 0
    $totalFiles   = 0

    foreach ($packId in $PackDefs.Keys) {
        $packPath = $PackDefs[$packId]
        $engDir   = Join-Path $packPath 'Languages\English'

        if (-not (Test-Path -LiteralPath $engDir)) {
            Write-Host "  Skipping $packId - no English\ at $engDir"
            continue
        }

        $packData = [ordered]@{
            keyed       = [ordered]@{}
            definjected = [ordered]@{}
            names       = [ordered]@{}
        }

        # Keyed
        $keyedDir = Join-Path $engDir 'Keyed'
        if (Test-Path -LiteralPath $keyedDir) {
            foreach ($f in Get-ChildItem -LiteralPath $keyedDir -Filter '*.xml' | Sort-Object Name) {
                $rel = $f.FullName.Substring($engDir.Length + 1).Replace('\', '/')
                $data = Read-XmlEntries $f.FullName
                if ($data.Entries.Count -gt 0) {
                    $packData.keyed[$rel] = [ordered]@{
                        entries  = @($data.Entries | ForEach-Object { [ordered]@{ key = $_.Key; text = $_.Text } })
                        comments = @($data.Comments)
                    }
                    $totalStrings += $data.Entries.Count
                    $totalFiles++
                }
            }
        }

        # DefInjected
        $diDir = Join-Path $engDir 'DefInjected'
        if (Test-Path -LiteralPath $diDir) {
            foreach ($sub in Get-ChildItem -LiteralPath $diDir -Directory | Sort-Object Name) {
                foreach ($f in Get-ChildItem -LiteralPath $sub.FullName -Filter '*.xml' | Sort-Object Name) {
                    $rel = $f.FullName.Substring($engDir.Length + 1).Replace('\', '/')
                    $data = Read-XmlEntries $f.FullName
                    if ($data.Entries.Count -gt 0) {
                        $packData.definjected[$rel] = [ordered]@{
                            entries = @($data.Entries | ForEach-Object { [ordered]@{ key = $_.Key; text = $_.Text } })
                        }
                        $totalStrings += $data.Entries.Count
                        $totalFiles++
                    }
                }
            }
        }

        # Names
        $namesDir = Join-Path $engDir 'Strings\Names'
        if (Test-Path -LiteralPath $namesDir) {
            foreach ($f in Get-ChildItem -LiteralPath $namesDir -Filter '*.txt' | Sort-Object Name) {
                $rel = $f.FullName.Substring($engDir.Length + 1).Replace('\', '/')
                $names = Read-NamesFile $f.FullName
                if ($names.Count -gt 0) {
                    $packData.names[$rel] = @($names)
                    $totalStrings += $names.Count
                    $totalFiles++
                }
            }
        }

        $manifest.packs[$packId] = $packData
    }

    # Stats
    $packStats = [ordered]@{}
    foreach ($pId_ in $manifest.packs.Keys) {
        $p = $manifest.packs[$pId_]
        $packStats[$pId_] = [ordered]@{
            keyed_files       = $p.keyed.Count
            definjected_files = $p.definjected.Count
            names_files       = $p.names.Count
            keyed_strings     = ($p.keyed.Values | ForEach-Object { $_.entries.Count } | Measure-Object -Sum).Sum
            definjected_strings = ($p.definjected.Values | ForEach-Object { $_.entries.Count } | Measure-Object -Sum).Sum
            names_strings     = ($p.names.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum
        }
    }
    $manifest.stats = [ordered]@{
        total_strings = $totalStrings
        total_files   = $totalFiles
        packs         = $packStats
    }

    $json = $manifest | ConvertTo-Json -Depth 10 -Compress:$false
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($ManifestPath, $json, $utf8NoBom)

    Write-Host "Extracted $totalStrings strings from $totalFiles files -> $ManifestPath"
    foreach ($pId_ in $packStats.Keys) {
        $s = $packStats[$pId_]
        Write-Host ('  {0}: {1} keyed, {2} definjected, {3} names ({4}+{5}+{6} strings)' -f $pId_, $s.keyed_files, $s.definjected_files, $s.names_files, $s.keyed_strings, $s.definjected_strings, $s.names_strings)
    }
}

# -- generate-skeleton --------------------------------------------------------

function Invoke-GenerateSkeleton {
    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        throw "master_strings.json not found. Run 'extract' first."
    }

    $manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

    foreach ($lang in $Languages) {
        $filesWritten = 0

        foreach ($packId in @($PackDefs.Keys)) {
            $packPath = $PackDefs[$packId]
            $langDir  = Join-Path $packPath "Languages\$lang"
            $packObj  = $manifest.packs.$packId

            if ($null -eq $packObj) { continue }

            # Keyed
            foreach ($rel in @(($packObj.keyed | Get-Member -MemberType NoteProperty).Name)) {
                $fileData = $packObj.keyed.$rel
                $outPath  = Join-Path $langDir $rel.Replace('/', '\')

                $entryList = [System.Collections.Generic.List[hashtable]]::new()
                foreach ($e in $fileData.entries) {
                    $entryList.Add(@{ Key = $e.key; Text = $e.text })
                }

                $commentList = [System.Collections.Generic.List[string]]::new()
                if ($fileData.comments) {
                    foreach ($c in $fileData.comments) { $commentList.Add($c) }
                }

                Write-XmlFile -FilePath $outPath -Entries $entryList -Comments $commentList
                $filesWritten++
            }

            # DefInjected
            foreach ($rel in @(($packObj.definjected | Get-Member -MemberType NoteProperty).Name)) {
                $fileData = $packObj.definjected.$rel
                $outPath  = Join-Path $langDir $rel.Replace('/', '\')

                $entryList = [System.Collections.Generic.List[hashtable]]::new()
                foreach ($e in $fileData.entries) {
                    $entryList.Add(@{ Key = $e.key; Text = $e.text })
                }

                $commentList = [System.Collections.Generic.List[string]]::new()
                Write-XmlFile -FilePath $outPath -Entries $entryList -Comments $commentList
                $filesWritten++
            }

            # Names
            foreach ($rel in @(($packObj.names | Get-Member -MemberType NoteProperty).Name)) {
                $names   = @($packObj.names.$rel)
                $outPath = Join-Path $langDir $rel.Replace('/', '\')
                Write-NamesFile -FilePath $outPath -Names $names
                $filesWritten++
            }
        }

        Write-Host "  $lang : $filesWritten files (English baseline)"
    }

    Write-Host "Skeleton generation complete."
}

# -- apply --------------------------------------------------------------------

function Invoke-Apply {
    <#
    .SYNOPSIS Apply translations from translations/<lang>.json to language files.
    .DESCRIPTION
        Each JSON is a flat object: { "Part1|Keyed/File.xml|KeyName": "translated text", ... }
        For Names files: { "Part1|Strings/Names/File.txt|0": "translated name", ... }
    #>
    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        throw "master_strings.json not found. Run 'extract' first."
    }

    $manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

    foreach ($lang in $Languages) {
        $transFile = Join-Path $TransDir "$lang.json"
        if (-not (Test-Path -LiteralPath $transFile)) {
            Write-Host "  Skipping $lang - no translations\$lang.json"
            continue
        }

        $trans = Get-Content -LiteralPath $transFile -Raw -Encoding UTF8 | ConvertFrom-Json
        $transMap = @{}
        foreach ($prop in ($trans | Get-Member -MemberType NoteProperty)) {
            $transMap[$prop.Name] = $trans.$($prop.Name)
        }

        $applied = 0
        $filesUpdated = 0

        foreach ($packId in @($PackDefs.Keys)) {
            $packPath = $PackDefs[$packId]
            $langDir  = Join-Path $packPath "Languages\$lang"
            $packObj  = $manifest.packs.$packId
            if ($null -eq $packObj) { continue }

            # Keyed + DefInjected
            foreach ($category in @('keyed', 'definjected')) {
                $catObj = $packObj.$category
                if ($null -eq $catObj) { continue }

                foreach ($rel in @(($catObj | Get-Member -MemberType NoteProperty).Name)) {
                    $fileData = $catObj.$rel
                    $outPath  = Join-Path $langDir $rel.Replace('/', '\')
                    $changed  = $false

                    $entryList = [System.Collections.Generic.List[hashtable]]::new()
                    foreach ($e in $fileData.entries) {
                        $sid = "$packId|$rel|$($e.key)"
                        if ($transMap.ContainsKey($sid)) {
                            $entryList.Add(@{ Key = $e.key; Text = $transMap[$sid] })
                            $applied++
                            $changed = $true
                        } else {
                            $entryList.Add(@{ Key = $e.key; Text = $e.text })
                        }
                    }

                    if ($changed) {
                        $commentList = [System.Collections.Generic.List[string]]::new()
                        if ($category -eq 'keyed' -and $fileData.comments) {
                            foreach ($c in $fileData.comments) { $commentList.Add($c) }
                        }
                        Write-XmlFile -FilePath $outPath -Entries $entryList -Comments $commentList
                        $filesUpdated++
                    }
                }
            }

            # Names
            $namesObj = $packObj.names
            if ($null -ne $namesObj) {
                foreach ($rel in @(($namesObj | Get-Member -MemberType NoteProperty).Name)) {
                    $names   = @($namesObj.$rel)
                    $outPath = Join-Path $langDir $rel.Replace('/', '\')
                    $changed = $false
                    $translated = @()

                    for ($i = 0; $i -lt $names.Count; $i++) {
                        $sid = "$packId|$rel|$i"
                        if ($transMap.ContainsKey($sid)) {
                            $translated += $transMap[$sid]
                            $applied++
                            $changed = $true
                        } else {
                            $translated += $names[$i]
                        }
                    }

                    if ($changed) {
                        Write-NamesFile -FilePath $outPath -Names $translated
                        $filesUpdated++
                    }
                }
            }
        }

        Write-Host "  $lang : applied $applied translations across $filesUpdated files"
    }

    Write-Host "Apply complete."
}

# -- validate -----------------------------------------------------------------

function Invoke-Validate {
    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        throw "master_strings.json not found. Run 'extract' first."
    }

    $manifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $errors   = [System.Collections.Generic.List[string]]::new()
    $warnings = [System.Collections.Generic.List[string]]::new()
    $checked  = 0

    $placeholderRe = [regex]'\{[^}]+\}'

    foreach ($lang in $Languages) {
        foreach ($packId in @($PackDefs.Keys)) {
            $packPath = $PackDefs[$packId]
            $langDir  = Join-Path $packPath "Languages\$lang"
            $packObj  = $manifest.packs.$packId
            if ($null -eq $packObj) { continue }

            if (-not (Test-Path -LiteralPath $langDir)) {
                $warnings.Add("$packId/$lang : language directory missing")
                continue
            }

            # Keyed + DefInjected
            foreach ($category in @('keyed', 'definjected')) {
                $catObj = $packObj.$category
                if ($null -eq $catObj) { continue }

                foreach ($rel in @(($catObj | Get-Member -MemberType NoteProperty).Name)) {
                    $fileData = $catObj.$rel
                    $outPath  = Join-Path $langDir $rel.Replace('/', '\')

                    if (-not (Test-Path -LiteralPath $outPath)) {
                        $errors.Add("MISSING: $packId/$lang/$rel")
                        continue
                    }
                    $checked++

                    # Well-formedness
                    try {
                        [xml]$doc = Get-Content -LiteralPath $outPath -Raw -Encoding UTF8
                    } catch {
                        $errors.Add("XML ERROR: $outPath - $_")
                        continue
                    }

                    # Key completeness
                    $engKeys = [System.Collections.Generic.HashSet[string]]::new()
                    foreach ($e in $fileData.entries) { $null = $engKeys.Add($e.key) }

                    $transKeys = [System.Collections.Generic.HashSet[string]]::new()
                    foreach ($node in $doc.DocumentElement.ChildNodes) {
                        if ($node.NodeType -eq [System.Xml.XmlNodeType]::Element) {
                            $null = $transKeys.Add($node.LocalName)
                        }
                    }

                    $missing = @($engKeys | Where-Object { -not $transKeys.Contains($_) })
                    if ($missing.Count -gt 0) {
                        $errors.Add("MISSING KEYS $packId/$lang/$rel : $($missing -join ', ')")
                    }

                    # Placeholder integrity
                    foreach ($e in $fileData.entries) {
                        $engPH = @($placeholderRe.Matches($e.text) | ForEach-Object { $_.Value }) | Sort-Object -Unique
                        if ($engPH.Count -eq 0) { continue }

                        $transNode = $doc.DocumentElement.SelectSingleNode($e.key)
                        if ($null -eq $transNode) { continue }
                        $transPH = @($placeholderRe.Matches($transNode.InnerXml) | ForEach-Object { $_.Value }) | Sort-Object -Unique

                        $diff = Compare-Object $engPH $transPH -PassThru
                        if ($diff) {
                            $errors.Add("PLACEHOLDER MISMATCH $packId/$lang/$rel/$($e.key): eng=[$($engPH -join ',')] trans=[$($transPH -join ',')]")
                        }
                    }
                }
            }

            # Names
            $namesObj = $packObj.names
            if ($null -ne $namesObj) {
                foreach ($rel in @(($namesObj | Get-Member -MemberType NoteProperty).Name)) {
                    $names   = @($namesObj.$rel)
                    $outPath = Join-Path $langDir $rel.Replace('/', '\')

                    if (-not (Test-Path -LiteralPath $outPath)) {
                        $errors.Add("MISSING: $packId/$lang/$rel")
                        continue
                    }
                    $checked++
                    $transNames = @(Get-Content -LiteralPath $outPath -Encoding UTF8 | Where-Object { $_.Trim() -ne '' })
                    if ($transNames.Count -ne $names.Count) {
                        $errors.Add("NAME COUNT MISMATCH $packId/$lang/$rel : eng=$($names.Count) trans=$($transNames.Count)")
                    }
                }
            }
        }
    }

    Write-Host "Validated $checked files across $($Languages.Count) languages"

    if ($warnings.Count -gt 0) {
        Write-Host "`nWarnings ($($warnings.Count)):"
        $warnings | Select-Object -First 20 | ForEach-Object { Write-Host "  WARNING: $_" }
        if ($warnings.Count -gt 20) { Write-Host "  ... and $($warnings.Count - 20) more" }
    }

    if ($errors.Count -gt 0) {
        Write-Host "`nErrors ($($errors.Count)):" -ForegroundColor Red
        $errors | Select-Object -First 30 | ForEach-Object { Write-Host "  ERROR: $_" -ForegroundColor Red }
        if ($errors.Count -gt 30) { Write-Host "  ... and $($errors.Count - 30) more" -ForegroundColor Red }
        exit 1
    } else {
        Write-Host "`nAll checks passed." -ForegroundColor Green
    }
}

# -- dispatch -----------------------------------------------------------------

switch ($Command) {
    'extract'            { Invoke-Extract }
    'generate-skeleton'  { Invoke-GenerateSkeleton }
    'apply'              { Invoke-Apply }
    'validate'           { Invoke-Validate }
}
