Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = 'C:\Users\Matthias\Desktop\Fallout Immersion Project'
$part1Root = Join-Path $repoRoot 'FIP-Translation Part 1\Languages'

function Get-LangNode {
    param([System.Xml.XmlDocument]$Doc,[string]$NodeName)
    foreach ($child in $Doc.LanguageData.ChildNodes) {
        if ($child.NodeType -eq [System.Xml.XmlNodeType]::Element -and $child.Name -eq $NodeName) {
            return $child
        }
    }
    return $null
}

function Set-OrCreateLangNode {
    param([System.Xml.XmlDocument]$Doc,[string]$NodeName,[string]$Value)
    $node = Get-LangNode -Doc $Doc -NodeName $NodeName
    if ($null -eq $node) {
        $node = $Doc.CreateElement($NodeName)
        [void]$Doc.LanguageData.AppendChild($node)
    }
    $node.InnerText = $Value
}

[xml]$srcLucky = Get-Content (Join-Path $repoRoot 'FIP-Lucky 38\LoadFolders\Plants_VBrewECandT\Languages\English\DefInjected\ThingDef\Lucky38_CoffeesAndTeas.xml')
[xml]$srcFish = Get-Content (Join-Path $repoRoot 'FIP-Arktos\LoadFolders\Odyssey\Languages\English\DefInjected\ThingDef\Arktos_Fish.xml')
[xml]$srcVcef = Get-Content (Join-Path $repoRoot 'FIP-Arktos\LoadFolders\Odyssey_VCEF\Languages\English\DefInjected\ThingDef\Arktos_VCEF_Fish.xml')

$luckyMap = @{}
foreach ($k in @('VBE_EspressoMachineBuilding.label','VBE_EspressoMachineBuilding.description')) {
    $n = Get-LangNode -Doc $srcLucky -NodeName $k
    if ($null -ne $n) { $luckyMap[$k] = $n.InnerText }
}

$fishDescMap = @{}
foreach ($n in $srcFish.LanguageData.ChildNodes) {
    if ($n.NodeType -eq [System.Xml.XmlNodeType]::Element -and $n.Name.EndsWith('.description')) {
        $fishDescMap[$n.Name] = $n.InnerText
    }
}

$vcefDescMap = @{}
foreach ($n in $srcVcef.LanguageData.ChildNodes) {
    if ($n.NodeType -eq [System.Xml.XmlNodeType]::Element -and $n.Name.EndsWith('.description')) {
        $vcefDescMap[$n.Name] = $n.InnerText
    }
}

$vcefLabelMap = @{}
foreach ($k in @('VCEF_RawHaddock.label','VCEF_RawHerring.label','VCEF_RawSprat.label')) {
    $n = Get-LangNode -Doc $srcVcef -NodeName $k
    if ($null -ne $n) { $vcefLabelMap[$k] = $n.InnerText }
}

$updatedFiles = 0
foreach ($langDir in Get-ChildItem -LiteralPath $part1Root -Directory) {
    $luckyPath = Join-Path $langDir.FullName 'DefInjected\ThingDef\FIP-Lucky 38__Lucky38_CoffeesAndTeas.xml'
    $fishPath = Join-Path $langDir.FullName 'DefInjected\ThingDef\FIP-Arktos__Arktos_Fish.xml'
    $vcefPath = Join-Path $langDir.FullName 'DefInjected\ThingDef\FIP-Arktos__Arktos_VCEF_Fish.xml'

    if (Test-Path -LiteralPath $luckyPath) {
        [xml]$doc = Get-Content $luckyPath
        foreach ($entry in $luckyMap.GetEnumerator()) {
            Set-OrCreateLangNode -Doc $doc -NodeName $entry.Key -Value $entry.Value
        }
        $doc.Save($luckyPath)
        $updatedFiles++
    }

    if (Test-Path -LiteralPath $fishPath) {
        [xml]$doc = Get-Content $fishPath
        foreach ($entry in $fishDescMap.GetEnumerator()) {
            Set-OrCreateLangNode -Doc $doc -NodeName $entry.Key -Value $entry.Value
        }
        $doc.Save($fishPath)
        $updatedFiles++
    }

    if (Test-Path -LiteralPath $vcefPath) {
        [xml]$doc = Get-Content $vcefPath
        foreach ($entry in $vcefDescMap.GetEnumerator()) {
            Set-OrCreateLangNode -Doc $doc -NodeName $entry.Key -Value $entry.Value
        }
        foreach ($entry in $vcefLabelMap.GetEnumerator()) {
            Set-OrCreateLangNode -Doc $doc -NodeName $entry.Key -Value $entry.Value
        }
        $doc.Save($vcefPath)
        $updatedFiles++
    }
}

Write-Host "UpdatedFiles=$updatedFiles"
