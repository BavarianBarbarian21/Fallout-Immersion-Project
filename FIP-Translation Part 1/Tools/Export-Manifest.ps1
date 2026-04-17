<#
.SYNOPSIS
    Extract all English strings from the 3 FIP Translation Packs into a set of
    per-pack manifest CSV files for translation reference.

.DESCRIPTION
    Scans Languages\English\ across Parts 1-3 and writes:
      manifests\Part1_strings.tsv
      manifests\Part2_strings.tsv
      manifests\Part3_strings.tsv

    Each TSV has columns: StringId<TAB>Text
    StringId format: PackId|RelPath|Key  (for XML) or PackId|RelPath|Index (for TXT)
#>
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ScriptDir = $PSScriptRoot
$RepoRoot  = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$OutDir    = Join-Path $ScriptDir 'manifests'

if (-not (Test-Path -LiteralPath $OutDir)) {
    $null = New-Item -Path $OutDir -ItemType Directory -Force
}

$PackDefs = [ordered]@{
    Part1 = Join-Path $RepoRoot 'FIP-Translation Part 1'
    Part2 = Join-Path $RepoRoot 'FIP-Translation Part 2'
    Part3 = Join-Path $RepoRoot 'FIP-Translation Part 3'
}

$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

foreach ($packId in $PackDefs.Keys) {
    $packPath = $PackDefs[$packId]
    $engDir = Join-Path $packPath 'Languages\English'
    $outFile = Join-Path $OutDir "${packId}_strings.tsv"

    if (-not (Test-Path -LiteralPath $engDir)) { continue }

    $sb = [System.Text.StringBuilder]::new()
    $null = $sb.AppendLine("StringId`tText")
    $count = 0

    # Keyed
    $keyedDir = Join-Path $engDir 'Keyed'
    if (Test-Path -LiteralPath $keyedDir) {
        foreach ($f in Get-ChildItem -LiteralPath $keyedDir -Filter '*.xml' | Sort-Object Name) {
            $rel = $f.FullName.Substring($engDir.Length + 1).Replace('\', '/')
            $doc = [System.Xml.XmlDocument]::new()
            $doc.Load($f.FullName)
            foreach ($node in $doc.DocumentElement.ChildNodes) {
                if ($node.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
                $sid = "$packId|$rel|$($node.LocalName)"
                $text = $node.InnerXml.Replace("`t", ' ').Replace("`r", '').Replace("`n", '\n')
                $null = $sb.AppendLine("$sid`t$text")
                $count++
            }
        }
    }

    # DefInjected
    $diDir = Join-Path $engDir 'DefInjected'
    if (Test-Path -LiteralPath $diDir) {
        foreach ($sub in Get-ChildItem -LiteralPath $diDir -Directory | Sort-Object Name) {
            foreach ($f in Get-ChildItem -LiteralPath $sub.FullName -Filter '*.xml' | Sort-Object Name) {
                $rel = $f.FullName.Substring($engDir.Length + 1).Replace('\', '/')
                $doc = [System.Xml.XmlDocument]::new()
                $doc.Load($f.FullName)
                foreach ($node in $doc.DocumentElement.ChildNodes) {
                    if ($node.NodeType -ne [System.Xml.XmlNodeType]::Element) { continue }
                    $sid = "$packId|$rel|$($node.LocalName)"
                    $text = $node.InnerXml.Replace("`t", ' ').Replace("`r", '').Replace("`n", '\n')
                    $null = $sb.AppendLine("$sid`t$text")
                    $count++
                }
            }
        }
    }

    # Names
    $namesDir = Join-Path $engDir 'Strings\Names'
    if (Test-Path -LiteralPath $namesDir) {
        foreach ($f in Get-ChildItem -LiteralPath $namesDir -Filter '*.txt' | Sort-Object Name) {
            $rel = $f.FullName.Substring($engDir.Length + 1).Replace('\', '/')
            $lines = [System.IO.File]::ReadAllLines($f.FullName, [System.Text.Encoding]::UTF8)
            $idx = 0
            foreach ($line in $lines) {
                if ($line.Trim() -eq '') { continue }
                $sid = "$packId|$rel|$idx"
                $null = $sb.AppendLine("$sid`t$line")
                $count++
                $idx++
            }
        }
    }

    [System.IO.File]::WriteAllText($outFile, $sb.ToString(), $Utf8NoBom)
    Write-Host "$packId : $count strings -> $outFile"
}

Write-Host "Done."
