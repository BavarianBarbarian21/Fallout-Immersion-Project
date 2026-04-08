param(
    [string]$FinalizedRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$TranslateRoot = (Join-Path (Split-Path (Resolve-Path (Join-Path $PSScriptRoot '..')).Path -Parent) '.Translate'),
    [switch]$LibraryMode,
    [switch]$SkipMachineTranslation
)

$ErrorActionPreference = 'Stop'

$locales = @(
    'Catalan',
    'ChineseSimplified',
    'ChineseTraditional',
    'Czech',
    'Danish',
    'Dutch',
    'Estonian',
    'Finnish',
    'French',
    'German',
    'Greek',
    'Hungarian',
    'Italian',
    'Japanese',
    'Korean',
    'Norwegian',
    'Polish',
    'Portuguese',
    'PortugueseBrazilian',
    'Romanian',
    'Russian',
    'Slovak',
    'Spanish',
    'SpanishLatin',
    'Swedish',
    'Turkish',
    'Ukrainian',
    'Vietnamese'
)

$localeCodes = @{
    Catalan = 'ca'
    ChineseSimplified = 'zh-CN'
    ChineseTraditional = 'zh-TW'
    Czech = 'cs'
    Danish = 'da'
    Dutch = 'nl'
    Estonian = 'et'
    Finnish = 'fi'
    French = 'fr'
    German = 'de'
    Greek = 'el'
    Hungarian = 'hu'
    Italian = 'it'
    Japanese = 'ja'
    Korean = 'ko'
    Norwegian = 'no'
    Polish = 'pl'
    Portuguese = 'pt'
    PortugueseBrazilian = 'pt-BR'
    Romanian = 'ro'
    Russian = 'ru'
    Slovak = 'sk'
    Spanish = 'es'
    SpanishLatin = 'es'
    Swedish = 'sv'
    Turkish = 'tr'
    Ukrainian = 'uk'
    Vietnamese = 'vi'
}

$textFields = @(
    'label',
    'description',
    'reportString',
    'jobString',
    'customLabel',
    'baseDescription',
    'gerundLabel',
    'verb',
    'title',
    'leaderTitle',
    'labelShort',
    'chargeNoun'
)

$translationCache = @{}
$batchSeparator = "`nZXQSPLITZX`n"
$placeholderPattern = '\{[^}]+\}|\(\*[^)]+\)|\(/[^)]+\)'

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
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
    [System.Uri]::UnescapeDataString($relativeUri.ToString()).Replace('/', '\')
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Source,

        [Parameter(Mandatory = $true)]
        [string]$Destination
    )

    if (-not (Test-Path -LiteralPath $Source)) {
        return
    }

    Ensure-Directory -Path $Destination

    Get-ChildItem -Path $Source -Recurse -File | ForEach-Object {
        $relativePath = Get-RelativePath -BasePath $Source -FullPath $_.FullName
        $destinationPath = Join-Path $Destination $relativePath
        Ensure-Directory -Path (Split-Path -Parent $destinationPath)
        Copy-Item -LiteralPath $_.FullName -Destination $destinationPath -Force
    }
}

function Get-DirectTextChildren {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$Node
    )

    @(
        $Node.ChildNodes | Where-Object {
            $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and
            $_.Name -in $textFields -and
            -not @($_.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element }).Count
        }
    )
}

function Get-DefInjectedEntries {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ModPath
    )

    $defsRoot = Join-Path $ModPath 'Defs'
    $entriesByDefType = @{}

    if (-not (Test-Path -LiteralPath $defsRoot)) {
        return $entriesByDefType
    }

    Get-ChildItem -Path $defsRoot -Recurse -File -Filter *.xml | ForEach-Object {
        try {
            $document = New-Object System.Xml.XmlDocument
            $document.Load($_.FullName)
        }
        catch {
            Write-Warning "Skipping unreadable XML: $($_.FullName)"
            return
        }

        if (-not $document.DocumentElement) {
            return
        }

        foreach ($defNode in @($document.DocumentElement.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })) {
            $defNameNode = @($defNode.ChildNodes | Where-Object {
                $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq 'defName'
            } | Select-Object -First 1)

            if (-not $defNameNode) {
                continue
            }

            $defName = $defNameNode.InnerText.Trim()
            if ([string]::IsNullOrWhiteSpace($defName)) {
                continue
            }

            $defType = $defNode.Name
            if (-not $entriesByDefType.ContainsKey($defType)) {
                $entriesByDefType[$defType] = [ordered]@{}
            }

            foreach ($childNode in Get-DirectTextChildren -Node $defNode) {
                $value = $childNode.InnerText.Trim()
                if ([string]::IsNullOrWhiteSpace($value)) {
                    continue
                }

                $entriesByDefType[$defType]["$defName.$($childNode.Name)"] = $value
            }
        }
    }

    return $entriesByDefType
}

function Write-LanguageDataFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [System.Collections.Specialized.OrderedDictionary]$Entries
    )

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('<?xml version="1.0" encoding="utf-8"?>') | Out-Null
    $lines.Add('<LanguageData>') | Out-Null

    foreach ($key in $Entries.Keys) {
        $escapedValue = [System.Security.SecurityElement]::Escape([string]$Entries[$key])
        $lines.Add("    <$key>$escapedValue</$key>") | Out-Null
    }

    $lines.Add('</LanguageData>') | Out-Null

    Ensure-Directory -Path (Split-Path -Parent $FilePath)
    Set-Content -LiteralPath $FilePath -Value $lines -Encoding utf8
}

function Write-DefInjectedLanguage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LanguageRoot,

        [Parameter(Mandatory = $true)]
        [hashtable]$EntriesByDefType
    )

    foreach ($defType in ($EntriesByDefType.Keys | Sort-Object)) {
        $entries = $EntriesByDefType[$defType]
        if (-not $entries.Count) {
            continue
        }

        $defTypeRoot = Join-Path (Join-Path $LanguageRoot 'DefInjected') $defType
        $filePath = Join-Path $defTypeRoot 'AutoGenerated.xml'
        Write-LanguageDataFile -FilePath $filePath -Entries $entries
    }
}

function Protect-TranslationTokens {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text
    )

    $index = 0
    $tokens = @{}
    $protectedText = [regex]::Replace($Text, $placeholderPattern, {
        param($match)
        $token = "ZXQPH$index`ZX"
        $tokens[$token] = $match.Value
        $script:index += 1
        $token
    })

    [pscustomobject]@{
        Text = $protectedText
        Tokens = $tokens
    }
}

function Restore-TranslationTokens {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [hashtable]$Tokens
    )

    $restored = $Text
    foreach ($token in $Tokens.Keys) {
        $restored = $restored.Replace($token, $Tokens[$token])
    }

    $restored
}

function Get-TranslationString {
    param(
        [Parameter(Mandatory = $true)]
        $ApiResult
    )

    (($ApiResult[0] | ForEach-Object {
        if ($_ -is [System.Array] -and $_.Length -gt 0) {
            [string]$_[0]
        }
    }) -join '')
}

function Invoke-TranslationRequest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [Parameter(Mandatory = $true)]
        [string]$TargetCode
    )

    $normalizedText = $Text.Replace([char]0x2019, "'").Replace([char]0x2018, "'").Replace([char]0x201C, '"').Replace([char]0x201D, '"').Replace([char]0x2014, '-').Replace([char]0x2013, '-').Replace([char]0x00A0, ' ')
    $url = 'https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&dt=t&tl=' + $TargetCode + '&q=' + [System.Uri]::EscapeDataString($normalizedText)
    $delays = @(0, 2, 5, 10)

    for ($attempt = 0; $attempt -lt $delays.Count; $attempt += 1) {
        if ($delays[$attempt] -gt 0) {
            Start-Sleep -Seconds $delays[$attempt]
        }

        try {
            $result = Invoke-RestMethod -Uri $url -Method Get
            return (Get-TranslationString -ApiResult $result)
        }
        catch {
            if ($attempt -eq ($delays.Count - 1)) {
                throw
            }
        }
    }
}

function Translate-TextBatch {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Texts,

        [Parameter(Mandatory = $true)]
        [string]$TargetCode
    )

    $protectedTexts = New-Object System.Collections.Generic.List[string]
    $tokenSets = New-Object System.Collections.Generic.List[hashtable]

    foreach ($text in $Texts) {
        $protected = Protect-TranslationTokens -Text $text
        $protectedTexts.Add($protected.Text) | Out-Null
        $tokenSets.Add($protected.Tokens) | Out-Null
    }

    $joinedText = $protectedTexts -join $batchSeparator
    $translatedJoined = Invoke-TranslationRequest -Text $joinedText -TargetCode $TargetCode
    $translatedParts = @($translatedJoined -split [regex]::Escape($batchSeparator), 0, 'SimpleMatch')

    if ($translatedParts.Count -ne $Texts.Count) {
        $translatedParts = @()
        for ($index = 0; $index -lt $Texts.Count; $index += 1) {
            $singleTranslated = Invoke-TranslationRequest -Text $protectedTexts[$index] -TargetCode $TargetCode
            $translatedParts += Restore-TranslationTokens -Text $singleTranslated -Tokens $tokenSets[$index]
        }

        return $translatedParts
    }

    for ($index = 0; $index -lt $translatedParts.Count; $index += 1) {
        $translatedParts[$index] = Restore-TranslationTokens -Text $translatedParts[$index] -Tokens $tokenSets[$index]
    }

    $translatedParts
}

function Translate-Texts {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Texts,

        [Parameter(Mandatory = $true)]
        [string]$Locale
    )

    if (-not $Texts.Count) {
        return @()
    }

    if (-not $translationCache.ContainsKey($Locale)) {
        $translationCache[$Locale] = @{}
    }

    $targetCode = $localeCodes[$Locale]
    $results = New-Object string[] $Texts.Count
    $currentBatchIndexes = New-Object System.Collections.Generic.List[int]
    $currentBatchTexts = New-Object System.Collections.Generic.List[string]
    $currentLength = 0

    for ($index = 0; $index -lt $Texts.Count; $index += 1) {
        $text = [string]$Texts[$index]
        if ([string]::IsNullOrWhiteSpace($text)) {
            $results[$index] = $text
            continue
        }

        if ($translationCache[$Locale].ContainsKey($text)) {
            $results[$index] = $translationCache[$Locale][$text]
            continue
        }

        $entryLength = $text.Length + $batchSeparator.Length
        if ($currentBatchTexts.Count -gt 0 -and ($currentBatchTexts.Count -ge 20 -or ($currentLength + $entryLength) -gt 2000)) {
            $translatedBatch = Translate-TextBatch -Texts $currentBatchTexts.ToArray() -TargetCode $targetCode
            for ($batchIndex = 0; $batchIndex -lt $currentBatchIndexes.Count; $batchIndex += 1) {
                $sourceIndex = $currentBatchIndexes[$batchIndex]
                $sourceText = $currentBatchTexts[$batchIndex]
                $translatedText = $translatedBatch[$batchIndex]
                $translationCache[$Locale][$sourceText] = $translatedText
                $results[$sourceIndex] = $translatedText
            }

            $currentBatchIndexes.Clear()
            $currentBatchTexts.Clear()
            $currentLength = 0
        }

        $currentBatchIndexes.Add($index) | Out-Null
        $currentBatchTexts.Add($text) | Out-Null
        $currentLength += $entryLength
    }

    if ($currentBatchTexts.Count -gt 0) {
        $translatedBatch = Translate-TextBatch -Texts $currentBatchTexts.ToArray() -TargetCode $targetCode
        for ($batchIndex = 0; $batchIndex -lt $currentBatchIndexes.Count; $batchIndex += 1) {
            $sourceIndex = $currentBatchIndexes[$batchIndex]
            $sourceText = $currentBatchTexts[$batchIndex]
            $translatedText = $translatedBatch[$batchIndex]
            $translationCache[$Locale][$sourceText] = $translatedText
            $results[$sourceIndex] = $translatedText
        }
    }

    $results
}

function Translate-LanguageDataFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceFile,

        [Parameter(Mandatory = $true)]
        [string]$TargetFile,

        [Parameter(Mandatory = $true)]
        [string]$Locale
    )

    function Save-XmlWithRetry {
        param(
            [Parameter(Mandatory = $true)]
            [System.Xml.XmlDocument]$XmlDocument,

            [Parameter(Mandatory = $true)]
            [string]$Path
        )

        $delays = @(0, 1, 2, 5)
        for ($attempt = 0; $attempt -lt $delays.Count; $attempt += 1) {
            if ($delays[$attempt] -gt 0) {
                Start-Sleep -Seconds $delays[$attempt]
            }

            try {
                $XmlDocument.Save($Path)
                return
            }
            catch {
                if ($attempt -eq ($delays.Count - 1)) {
                    throw
                }
            }
        }
    }

    $document = New-Object System.Xml.XmlDocument
    $document.PreserveWhitespace = $true
    $document.Load($SourceFile)

    if (-not $document.DocumentElement) {
        Copy-Item -LiteralPath $SourceFile -Destination $TargetFile -Force
        return
    }

    $textNodes = @($document.DocumentElement.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
    $sourceValues = @($textNodes | ForEach-Object { $_.InnerText })
    $translatedValues = Translate-Texts -Texts $sourceValues -Locale $Locale

    for ($index = 0; $index -lt $textNodes.Count; $index += 1) {
        $textNodes[$index].InnerText = $translatedValues[$index]
    }

    Ensure-Directory -Path (Split-Path -Parent $TargetFile)
    Save-XmlWithRetry -XmlDocument $document -Path $TargetFile
}

function Translate-TextFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceFile,

        [Parameter(Mandatory = $true)]
        [string]$TargetFile,

        [Parameter(Mandatory = $true)]
        [string]$Locale
    )

    function Set-ContentWithRetry {
        param(
            [Parameter(Mandatory = $true)]
            [string]$Path,

            [Parameter(Mandatory = $true)]
            [string[]]$Value
        )

        $delays = @(0, 1, 2, 5)
        for ($attempt = 0; $attempt -lt $delays.Count; $attempt += 1) {
            if ($delays[$attempt] -gt 0) {
                Start-Sleep -Seconds $delays[$attempt]
            }

            try {
                Set-Content -LiteralPath $Path -Value $Value -Encoding utf8
                return
            }
            catch {
                if ($attempt -eq ($delays.Count - 1)) {
                    throw
                }
            }
        }
    }

    $lines = Get-Content -LiteralPath $SourceFile
    $translatedLines = New-Object System.Collections.Generic.List[string]
    $pendingIndexes = New-Object System.Collections.Generic.List[int]
    $pendingValues = New-Object System.Collections.Generic.List[string]

    for ($index = 0; $index -lt $lines.Count; $index += 1) {
        $line = [string]$lines[$index]
        if ([string]::IsNullOrWhiteSpace($line)) {
            $translatedLines.Add($line) | Out-Null
            continue
        }

        $translatedLines.Add([string]::Empty) | Out-Null
        $pendingIndexes.Add($index) | Out-Null
        $pendingValues.Add($line) | Out-Null
    }

    if ($pendingValues.Count -gt 0) {
        $translatedValues = Translate-Texts -Texts $pendingValues.ToArray() -Locale $Locale
        for ($index = 0; $index -lt $pendingIndexes.Count; $index += 1) {
            $translatedLines[$pendingIndexes[$index]] = $translatedValues[$index]
        }
    }

    Ensure-Directory -Path (Split-Path -Parent $TargetFile)
    Set-ContentWithRetry -Path $TargetFile -Value $translatedLines
}

function Get-ExistingTranslationSet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceRoot
    )

    $set = @{}
    if (-not (Test-Path -LiteralPath $SourceRoot)) {
        return $set
    }

    Get-ChildItem -Path $SourceRoot -Recurse -File | ForEach-Object {
        $set[(Get-RelativePath -BasePath $SourceRoot -FullPath $_.FullName)] = $true
    }

    return $set
}

if (-not $LibraryMode) {
    $finalizedMods = Get-ChildItem -Path $FinalizedRoot -Directory | Where-Object { $_.Name -ne 'Tools' }
    $summary = New-Object System.Collections.Generic.List[string]

    foreach ($mod in $finalizedMods) {
        $modLanguagesRoot = Join-Path $mod.FullName 'Languages'
        $englishLanguageRoot = Join-Path $modLanguagesRoot 'English'
        $translateLanguagesRoot = Join-Path (Join-Path $TranslateRoot $mod.Name) 'Languages'
        $defInjectedEntries = Get-DefInjectedEntries -ModPath $mod.FullName

        Ensure-Directory -Path $modLanguagesRoot
        Ensure-Directory -Path $englishLanguageRoot

        if ($defInjectedEntries.Count) {
            Write-DefInjectedLanguage -LanguageRoot $englishLanguageRoot -EntriesByDefType $defInjectedEntries
        }

        $englishFiles = @()
        if (Test-Path -LiteralPath $englishLanguageRoot) {
            $englishFiles = @(Get-ChildItem -Path $englishLanguageRoot -Recurse -File | Where-Object { $_.Extension -in '.xml', '.txt' })
        }

        $translatedFileCount = 0

        foreach ($locale in $locales) {
            $localeRoot = Join-Path $modLanguagesRoot $locale
            Ensure-Directory -Path $localeRoot

            Copy-DirectoryContents -Source $englishLanguageRoot -Destination $localeRoot

            $translatedLocaleRoot = Join-Path $translateLanguagesRoot $locale
            Copy-DirectoryContents -Source $translatedLocaleRoot -Destination $localeRoot

            if ($SkipMachineTranslation -or -not $localeCodes.ContainsKey($locale)) {
                continue
            }

            $existingTranslationSet = Get-ExistingTranslationSet -SourceRoot $translatedLocaleRoot

            foreach ($englishFile in $englishFiles) {
                $relativePath = Get-RelativePath -BasePath $englishLanguageRoot -FullPath $englishFile.FullName
                if ($existingTranslationSet.ContainsKey($relativePath)) {
                    continue
                }

                $targetFile = Join-Path $localeRoot $relativePath
                if ($englishFile.Extension -eq '.xml') {
                    Translate-LanguageDataFile -SourceFile $englishFile.FullName -TargetFile $targetFile -Locale $locale
                    $translatedFileCount += 1
                    continue
                }

                if ($englishFile.Extension -eq '.txt') {
                    Translate-TextFile -SourceFile $englishFile.FullName -TargetFile $targetFile -Locale $locale
                    $translatedFileCount += 1
                }
            }
        }

        $summary.Add("$($mod.Name): English=$($englishFiles.Count), DefInjectedTypes=$($defInjectedEntries.Count), TranslatedFiles=$translatedFileCount") | Out-Null
    }

    $summary | ForEach-Object { Write-Host $_ }
}