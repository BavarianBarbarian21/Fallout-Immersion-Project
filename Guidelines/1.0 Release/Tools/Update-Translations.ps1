$ErrorActionPreference = 'Stop'

$stage = 'C:\Users\Matthias\Desktop\Fallout Immersion Project\Guidelines\1.0 Release'
$translationRoots = Get-ChildItem -LiteralPath $stage -Directory -Filter 'FIP-Translation*'

function Assert-InStage([string]$Path) {
    $root = [IO.Path]::GetFullPath($stage).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to mutate outside staging: $full"
    }
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    Assert-InStage $Path
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

$movedRulePacks = @{
    'HHTools_PawnNameMaker_SuperMutant_Male' = 'WestTek_PawnNameMaker_SuperMutant_Male'
    'HHTools_PawnNameMaker_Mutant_Female' = 'WestTek_PawnNameMaker_Mutant_Female'
    'HHTools_PawnNameMaker_Ghoul_Male' = 'WestTek_PawnNameMaker_Ghoul_Male'
    'HHTools_PawnNameMaker_Ghoul_Female' = 'WestTek_PawnNameMaker_Ghoul_Female'
    'HHTools_PawnNameMaker_Numen_Male' = 'WestTek_PawnNameMaker_Numen_Male'
    'HHTools_PawnNameMaker_Numen_Female' = 'WestTek_PawnNameMaker_Numen_Female'
}

foreach ($translationRoot in $translationRoots) {
    foreach ($languageRoot in Get-ChildItem -LiteralPath (Join-Path $translationRoot.FullName 'Languages') -Directory) {
        # Only FCP storytellers remain selectable in FIP 1.0. Keep translations for
        # those six defs, but never reintroduce labels or descriptions for storytellers
        # hidden by the gameplay modules.
        $storytellerEntries = Join-Path $languageRoot.FullName 'DefInjected\StorytellerDef\Entries.xml'
        if (Test-Path -LiteralPath $storytellerEntries) {
            [xml]$storytellerXml = [IO.File]::ReadAllText($storytellerEntries)
            foreach ($entry in @($storytellerXml.SelectNodes('/LanguageData/*[not(starts-with(local-name(), "FCP_Storyteller_"))]'))) {
                [void]$entry.ParentNode.RemoveChild($entry)
            }
            $settings = [Xml.XmlWriterSettings]::new()
            $settings.Indent = $true
            $settings.IndentChars = '  '
            $settings.NewLineChars = "`r`n"
            $settings.NewLineHandling = [Xml.NewLineHandling]::Replace
            $settings.Encoding = [Text.UTF8Encoding]::new($false)
            $settings.OmitXmlDeclaration = $false
            $writer = [Xml.XmlWriter]::Create($storytellerEntries, $settings)
            try { $storytellerXml.Save($writer) } finally { $writer.Dispose() }
        }

        $entries = Join-Path $languageRoot.FullName 'DefInjected\RulePackDef\Entries.xml'
        if (Test-Path -LiteralPath $entries) {
            $text = [IO.File]::ReadAllText($entries)
            foreach ($oldName in $movedRulePacks.Keys) {
                $text = $text.Replace($oldName, $movedRulePacks[$oldName])
            }
            foreach ($sex in @('Female', 'Male')) {
                $oldPrefix = "HHTools_PawnNameMaker_American_$sex"
                $newPrefix = "WestTek_PawnNameMaker_American_$sex"
                if (-not $text.Contains($newPrefix)) {
                    $pattern = "(?m)^(\s*)<" + [regex]::Escape($oldPrefix) + "([^\r\n]+)</" + [regex]::Escape($oldPrefix) + "([^\r\n]+)$"
                    $text = [regex]::Replace($text, $pattern, {
                        param($match)
                        $copy = $match.Value.Replace($oldPrefix, $newPrefix)
                        return $match.Value + "`r`n" + $copy
                    })
                }
            }
            Write-Utf8NoBom $entries $text
        }

        $hhNames = Join-Path $languageRoot.FullName 'Strings\FIP-H&HTools\Names'
        if (-not (Test-Path -LiteralPath $hhNames -PathType Container)) { continue }
        $westNames = Join-Path $languageRoot.FullName 'Strings\FIP-WestTek\Names'
        Assert-InStage $westNames
        [IO.Directory]::CreateDirectory($westNames) | Out-Null
        foreach ($stem in @(
            'American_Female', 'American_Last', 'American_Male',
            'Chinese_Female', 'Chinese_Last', 'Chinese_Male',
            'Ghoul', 'Mutant', 'Numen'
        )) {
            $source = Join-Path $hhNames "HHTools_Names_$stem.txt"
            $destination = Join-Path $westNames "WestTek_Names_$stem.txt"
            if (Test-Path -LiteralPath $source) {
                Assert-InStage $destination
                Copy-Item -LiteralPath $source -Destination $destination -Force
            }
        }
        foreach ($stem in @('Ghoul', 'Mutant', 'Numen')) {
            $obsolete = Join-Path $hhNames "HHTools_Names_$stem.txt"
            Assert-InStage $obsolete
            if (Test-Path -LiteralPath $obsolete) { Remove-Item -LiteralPath $obsolete -Force }
        }
    }
}

Write-Output 'Translation ownership updated for WestTek name makers and the Big MT package identity remains FIP.Sunset.'
