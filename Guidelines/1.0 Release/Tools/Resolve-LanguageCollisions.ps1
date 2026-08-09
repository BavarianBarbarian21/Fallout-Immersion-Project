$ErrorActionPreference = 'Stop'

$stage = 'C:\Users\Matthias\Desktop\Fallout Immersion Project\Guidelines\1.0 Release'

function Assert-InStage([string]$Path) {
    $root = [IO.Path]::GetFullPath($stage).TrimEnd('\') + '\'
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to mutate outside staging: $full"
    }
}

function Save-Xml([xml]$Document, [string]$Path) {
    Assert-InStage $Path
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Encoding = [Text.UTF8Encoding]::new($false)
    $settings.Indent = $true
    $settings.NewLineChars = "`r`n"
    $writer = [Xml.XmlWriter]::Create($Path, $settings)
    try { $Document.Save($writer) } finally { $writer.Dispose() }
}

function Get-LanguageKeys([string]$Root) {
    $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Filter '*.xml' | Where-Object FullName -Match '\\Languages\\English\\') {
        [xml]$document = [IO.File]::ReadAllText($file.FullName)
        if (-not $document.LanguageData) { continue }
        foreach ($node in $document.LanguageData.ChildNodes | Where-Object NodeType -eq Element) {
            $keys.Add($node.Name) | Out-Null
        }
    }
    return ,$keys
}

function Remove-LanguageKeys([string]$Root, [Collections.Generic.HashSet[string]]$Keys) {
    foreach ($file in Get-ChildItem -LiteralPath $Root -Recurse -File -Filter '*.xml' | Where-Object FullName -Match '\\Languages\\English\\') {
        [xml]$document = [IO.File]::ReadAllText($file.FullName)
        if (-not $document.LanguageData) { continue }
        $changed = $false
        foreach ($node in @($document.LanguageData.ChildNodes | Where-Object NodeType -eq Element)) {
            if ($Keys.Contains($node.Name)) {
                $node.ParentNode.RemoveChild($node) | Out-Null
                $changed = $true
            }
        }
        if (-not $changed) { continue }
        if (@($document.LanguageData.ChildNodes | Where-Object NodeType -eq Element).Count -eq 0) {
            Assert-InStage $file.FullName
            Remove-Item -LiteralPath $file.FullName -Force
        } else {
            Save-Xml $document $file.FullName
        }
    }
}

# Psycast/anima language is solely Hubris-owned, including the optional Royalty flavor.
$hubrisRoot = Join-Path $stage 'FIP-Hubris\LoadFolders\Hubris'
$whitespringRoot = Join-Path $stage 'FIP-Whitespring\LoadFolders\Whitespring'
$hubrisKeys = Get-LanguageKeys $hubrisRoot
$whitespringKeys = Get-LanguageKeys $whitespringRoot
$psycastCollisions = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($key in $hubrisKeys) {
    if ($whitespringKeys.Contains($key)) { $psycastCollisions.Add($key) | Out-Null }
}
Remove-LanguageKeys $whitespringRoot $psycastCollisions

# Dedicated feature owners win the remaining confirmed collisions.
$hhRoot = Join-Path $stage 'FIP-H&HTools\LoadFolders'
$hhLosingKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($key in @(
    'AsteroidLetterText', 'CaravanShuttleFuel',
    'Empire.description', 'Empire.label', 'Empire.pawnSingular', 'Empire.pawnsPlural',
    'RefugeePodCrash', 'RefugeePodCrashLabel'
)) { $hhLosingKeys.Add($key) | Out-Null }
Remove-LanguageKeys $hhRoot $hhLosingKeys

$luckyRoot = Join-Path $stage 'FIP-Lucky 38\LoadFolders'
$luckyLosingKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$luckyLosingKeys.Add('Chemshine.label') | Out-Null
$luckyLosingKeys.Add('Chemshine.description') | Out-Null
Remove-LanguageKeys $luckyRoot $luckyLosingKeys

Write-Output "Removed $($psycastCollisions.Count) Whitespring psycast/anima duplicates plus 10 dedicated-owner collisions."
