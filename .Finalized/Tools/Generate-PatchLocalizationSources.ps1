param(
    [string]$FinalizedRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string[]]$ModsFilter
)

$ErrorActionPreference = 'Stop'

$translatableLeafNames = @(
    'label',
    'description',
    'labelNoun',
    'labelShort',
    'reportString',
    'jobString',
    'customLabel',
    'baseDescription',
    'gerundLabel',
    'verb',
    'title',
    'leaderTitle',
    'chargeNoun',
    'name'
)

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Sanitize-PathName {
    param([string]$Text)

    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    $sanitized = $Text
    foreach ($char in $invalid) {
        $sanitized = $sanitized.Replace($char, '_')
    }
    $sanitized.Replace(' ', '_')
}

function Get-NodeTextValue {
    param([System.Xml.XmlNode]$ValueNode)

    $element = @($ValueNode.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element } | Select-Object -First 1)
    if ($element) {
        return [string]$element.InnerText
    }

    [string]$ValueNode.InnerText
}

function Write-LanguageDataFile {
    param(
        [string]$FilePath,
        [hashtable]$Entries
    )

    if (-not $Entries.Count) {
        return
    }

    Ensure-Directory -Path (Split-Path -Parent $FilePath)

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('<?xml version="1.0" encoding="utf-8"?>') | Out-Null
    $lines.Add('<LanguageData>') | Out-Null
    foreach ($key in ($Entries.Keys | Sort-Object)) {
        $escapedValue = [System.Security.SecurityElement]::Escape([string]$Entries[$key])
        $lines.Add("    <$key>$escapedValue</$key>") | Out-Null
    }
    $lines.Add('</LanguageData>') | Out-Null

    Set-Content -LiteralPath $FilePath -Value $lines -Encoding utf8
}

function Convert-XPathSegmentToKeyPart {
    param([string]$Segment)

    if ($Segment -match '^li\[key="([^"]+)"\]$') {
        return $Matches[1]
    }

    if ($Segment -match '^li\[(?:text\(\)|@Class)="([^"]+)"\]$') {
        return ($Matches[1] -replace '[^A-Za-z0-9_]', '_')
    }

    if ($Segment -match '^\*\[defName="([^"]+)"\]$') {
        return $Matches[1]
    }

    if ($Segment -match '^([A-Za-z0-9_.]+)\[(?:defName|@Name)="([^"]+)"\]$') {
        return $Matches[2]
    }

    if ($Segment -match '^li$') {
        return 'li'
    }

    $Segment -replace '[^A-Za-z0-9_.]', '_'
}

function Get-DefTypeFromXPath {
    param(
        [string]$XPath,
        [string]$DefName,
        [string]$LeafName
    )

    if ($XPath -match '^/Defs/([A-Za-z0-9_.]+)\[(?:defName|@Name)="') {
        return $Matches[1]
    }

    if ($DefName -like '*_UpgradeTree') {
        return 'UpgradeTreeDef'
    }

    if ($LeafName -eq 'name') {
        return 'PipeNetDef'
    }

    'ThingDef'
}

function Add-Entry {
    param(
        [hashtable]$Map,
        [string]$Key,
        [string]$Value
    )

    if ([string]::IsNullOrWhiteSpace($Key) -or [string]::IsNullOrWhiteSpace($Value)) {
        return
    }

    $Map[$Key] = $Value.Trim()
}

$mods = Get-ChildItem -Path $FinalizedRoot -Directory | Where-Object {
    $_.Name -ne 'Tools' -and ((-not $ModsFilter) -or ($_.Name -in $ModsFilter))
}

foreach ($mod in $mods) {
    $patchRoot = Join-Path $mod.FullName 'Patches'
    if (-not (Test-Path -LiteralPath $patchRoot)) {
        continue
    }

    $englishRoot = Join-Path (Join-Path $mod.FullName 'Languages') 'English'
    Ensure-Directory -Path $englishRoot

    Get-ChildItem -Path $patchRoot -Recurse -File -Filter *.xml | ForEach-Object {
        $keyedEntries = @{}
        $defInjectedByType = @{}
        $patchName = Sanitize-PathName $_.BaseName

        $document = New-Object System.Xml.XmlDocument
        $document.Load($_.FullName)

        $allOperations = @($document.SelectNodes('//*[xpath and value]'))
        foreach ($operation in $allOperations) {
            $xpathNode = $operation.SelectSingleNode('./xpath')
            $valueNode = $operation.SelectSingleNode('./value')

            if ($xpathNode -and $valueNode) {
                $xpath = [string]$xpathNode.InnerText
                $valueText = Get-NodeTextValue -ValueNode $valueNode

                if ($xpath -match '^/LanguageData/([A-Za-z0-9_]+)$') {
                    Add-Entry -Map $keyedEntries -Key $Matches[1] -Value $valueText
                    continue
                }

                if ($xpath -notmatch '^/Defs/') {
                    continue
                }

                $segments = @($xpath.Trim('/').Split('/'))
                if ($segments.Count -lt 3) {
                    continue
                }

                $leafName = Convert-XPathSegmentToKeyPart -Segment $segments[-1]
                if ($leafName -notin $translatableLeafNames) {
                    continue
                }

                $defSegment = $segments[1]
                $defKey = Convert-XPathSegmentToKeyPart -Segment $defSegment
                if ([string]::IsNullOrWhiteSpace($defKey)) {
                    continue
                }

                $pathParts = New-Object System.Collections.Generic.List[string]
                for ($index = 2; $index -lt $segments.Count; $index += 1) {
                    $pathParts.Add((Convert-XPathSegmentToKeyPart -Segment $segments[$index])) | Out-Null
                }

                $entryKey = $defKey + '.' + ($pathParts -join '.')
                $defType = Get-DefTypeFromXPath -XPath $xpath -DefName $defKey -LeafName $leafName
                if (-not $defInjectedByType.ContainsKey($defType)) {
                    $defInjectedByType[$defType] = @{}
                }

                Add-Entry -Map $defInjectedByType[$defType] -Key $entryKey -Value $valueText
            }

            $defsValueNode = $operation.SelectSingleNode('./value/*[self::Defs or self::LanguageData]')
            if (-not $defsValueNode) {
                continue
            }

            if ($defsValueNode.Name -eq 'LanguageData') {
                foreach ($child in @($defsValueNode.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })) {
                    Add-Entry -Map $keyedEntries -Key $child.Name -Value $child.InnerText
                }
                continue
            }

            foreach ($defNode in @($defsValueNode.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })) {
                $defNameNode = @($defNode.SelectNodes('./defName') | Select-Object -First 1)
                if (-not $defNameNode) {
                    continue
                }

                $defName = [string]$defNameNode.InnerText
                if ([string]::IsNullOrWhiteSpace($defName)) {
                    continue
                }

                $defType = $defNode.Name
                if (-not $defInjectedByType.ContainsKey($defType)) {
                    $defInjectedByType[$defType] = @{}
                }

                foreach ($child in @($defNode.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })) {
                    if ($child.Name -notin $translatableLeafNames) {
                        continue
                    }
                    Add-Entry -Map $defInjectedByType[$defType] -Key "$defName.$($child.Name)" -Value $child.InnerText
                }
            }
        }

        if ($keyedEntries.Count) {
            $keyedFile = Join-Path (Join-Path $englishRoot 'Keyed') ("Patch_$patchName.xml")
            Write-LanguageDataFile -FilePath $keyedFile -Entries $keyedEntries
        }

        foreach ($defType in $defInjectedByType.Keys) {
            $entries = $defInjectedByType[$defType]
            if (-not $entries.Count) {
                continue
            }

            $defTypeFolder = Join-Path (Join-Path $englishRoot 'DefInjected') (Sanitize-PathName $defType)
            $defFile = Join-Path $defTypeFolder ("Patch_$patchName.xml")
            Write-LanguageDataFile -FilePath $defFile -Entries $entries
        }
    }
}