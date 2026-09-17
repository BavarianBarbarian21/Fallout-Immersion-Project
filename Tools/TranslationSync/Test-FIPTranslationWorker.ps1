[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$worker = Join-Path $repoRoot 'FIP-Yes Man\Tools\Start-FIPTranslation.ps1'
$testSandbox = Join-Path ([System.IO.Path]::GetTempPath()) ('FIP-YesMan-Worker-Test-' + [Guid]::NewGuid().ToString('N'))
$testConfig = $testSandbox
$testRoot = Join-Path $testConfig 'FIP-YesMan\Exports'
$english = Join-Path $testRoot 'FIP-English Language Pack'
$testOutput = Join-Path $testConfig 'FIP-Languages'
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $english 'About'), (Join-Path $english 'Languages\English\DefInjected\ThingDef'), (Join-Path $english 'Languages\English\DefInjected\RecipeDef'), (Join-Path $english 'Languages\English\DefInjected\VanillaFurnitureExpandedFactory.MachiningProcessTemplateDef'), (Join-Path $english 'Languages\English\Keyed'), (Join-Path $english 'Languages\English\Strings\Names') | Out-Null
    Copy-Item -LiteralPath (Join-Path $repoRoot 'FIP-Yes Man\About\ModIcon.png') -Destination (Join-Path $english 'About\ModIcon.png')
    @' 
<ModMetaData><packageId>FIP.English</packageId><name>FIP - English Language Pack</name><author>Feil</author><description>Test</description><supportedVersions><li>1.6</li></supportedVersions></ModMetaData>
'@.Trim() | Set-Content -LiteralPath (Join-Path $english 'About\About.xml') -Encoding UTF8
    '<LanguageData><TestBread.label>Bread</TestBread.label><TestBread.description>Food for {PAWN_nameDef} from {TARGET_label}. &lt;color=#fff&gt;Ready&lt;/color&gt;</TestBread.description></LanguageData>' | Set-Content -LiteralPath (Join-Path $english 'Languages\English\DefInjected\ThingDef\Entries.xml') -Encoding UTF8
    $batchEntries = 0..119 | ForEach-Object { '<TestRecipe' + $_ + '.label>Test recipe number ' + $_ + '</TestRecipe' + $_ + '.label>' }
    ('<LanguageData>' + ($batchEntries -join '') + '</LanguageData>') | Set-Content -LiteralPath (Join-Path $english 'Languages\English\DefInjected\RecipeDef\Batch.xml') -Encoding UTF8
    '<LanguageData />' | Set-Content -LiteralPath (Join-Path $english 'Languages\English\DefInjected\VanillaFurnitureExpandedFactory.MachiningProcessTemplateDef\FIP.xml') -Encoding UTF8
    '<LanguageData><Test.Message>Hello [NAME]</Test.Message></LanguageData>' | Set-Content -LiteralPath (Join-Path $english 'Languages\English\Keyed\Keys.xml') -Encoding UTF8
    "Alice`nBob" | Set-Content -LiteralPath (Join-Path $english 'Languages\English\Strings\Names\Names.txt') -Encoding UTF8

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -ExportRoot $testRoot -Provider none -EstimateOnly
    if ($LASTEXITCODE -ne 0) {
        $failedStatusPath = Join-Path $testRoot 'Status\translation.status.json'
        $details = if (Test-Path -LiteralPath $failedStatusPath) { Get-Content -Raw -LiteralPath $failedStatusPath } else { 'No status file was written.' }
        throw "Estimate worker failed.`n$details"
    }
    $status = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'Status\translation.status.json') | ConvertFrom-Json
    if ($status.state -ne 'completed' -or $status.total -ne 125) { throw "Unexpected status: $($status | ConvertTo-Json -Compress)" }

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -ExportRoot $testRoot -Provider test
    if ($LASTEXITCODE -ne 0) {
        $details = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'Status\translation.status.json')
        $qaDetails = Get-ChildItem -LiteralPath (Join-Path $testOutput '_QA') -Filter '*.json' -ErrorAction SilentlyContinue | ForEach-Object { Get-Content -Raw -LiteralPath $_.FullName }
        throw "Offline generation worker failed.`n$details`n$qaDetails"
    }
    $generatedRoot = Join-Path $testOutput 'FIP-Simplified Chinese Language Pack'
    $generatedXml = Join-Path $generatedRoot 'Languages\ChineseSimplified\DefInjected\ThingDef\Entries.xml'
    $generatedNames = Join-Path $generatedRoot 'Languages\ChineseSimplified\Strings\Names\Names.txt'
    if (-not (Test-Path -LiteralPath $generatedXml) -or -not (Test-Path -LiteralPath $generatedNames)) { throw 'Generated language files are missing.' }
    $generatedXmlText = Get-Content -Raw -LiteralPath $generatedXml
    foreach ($protectedValue in @('{PAWN_nameDef}', '{TARGET_label}', '&lt;color=#fff&gt;', '&lt;/color&gt;')) {
        if (-not $generatedXmlText.Contains($protectedValue)) { throw "A protected value was damaged: $protectedValue" }
    }
    $status = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'Status\translation.status.json') | ConvertFrom-Json
    if ($status.state -ne 'completed' -or $status.current -ne 125 -or $status.total -ne 125) { throw "Unexpected generation status: $($status | ConvertTo-Json -Compress)" }
    if ($status.apiRequests -ge $status.total) { throw "Batching did not reduce provider calls: $($status | ConvertTo-Json -Compress)" }

    '<LanguageData><TestBread.label>Fresh bread</TestBread.label><TestBread.description>Food for {PAWN_nameDef} from {TARGET_label}. &lt;color=#fff&gt;Ready&lt;/color&gt;</TestBread.description><TestRobot.label>Utility robot</TestRobot.label></LanguageData>' | Set-Content -LiteralPath (Join-Path $english 'Languages\English\DefInjected\ThingDef\Entries.xml') -Encoding UTF8
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -ExportRoot $testRoot -Provider test
    if ($LASTEXITCODE -ne 0) {
        $details = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'Status\translation.status.json')
        throw "Incremental update worker failed.`n$details"
    }
    $plan = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'Translation\last-update-plan.json') | ConvertFrom-Json
    if ($plan.addedCount -ne 1 -or $plan.changedCount -ne 1 -or $plan.removedCount -ne 0 -or $plan.unchangedCount -ne 124) { throw "Unexpected update plan: $($plan | ConvertTo-Json -Compress)" }
    [xml]$updated = Get-Content -Raw -LiteralPath $generatedXml
    if ($updated.LanguageData.'TestBread.label' -ne 'Fresh bread' -or $updated.LanguageData.'TestRobot.label' -ne 'Utility robot') { throw 'Added or changed DefInjected entries were not applied.' }

    Remove-Item -LiteralPath (Join-Path $english 'Languages\English\Keyed\Keys.xml') -Force
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -ExportRoot $testRoot -Provider test
    if ($LASTEXITCODE -ne 0) { throw 'Removal update worker failed.' }
    $plan = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'Translation\last-update-plan.json') | ConvertFrom-Json
    if ($plan.removedCount -ne 1 -or $plan.unchangedCount -ne 125) { throw "Unexpected removal plan: $($plan | ConvertTo-Json -Compress)" }
    if (Test-Path -LiteralPath (Join-Path $generatedRoot 'Languages\Japanese\Keyed\Keys.xml')) { throw 'Removed source file survived in the generated pack.' }

    $cachePath = Join-Path $testRoot 'Translation\translation-cache.json'
    if (Test-Path -LiteralPath $cachePath) { Remove-Item -LiteralPath $cachePath -Force }
    Set-Content -LiteralPath (Join-Path $testRoot 'Status\translation.cancel.request') -Value 'test'
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $worker -ExportRoot $testRoot -Provider test
    if ($LASTEXITCODE -ne 2) { throw "Cancellation returned unexpected exit code $LASTEXITCODE." }
    $status = Get-Content -Raw -LiteralPath (Join-Path $testRoot 'Status\translation.status.json') | ConvertFrom-Json
    if ($status.state -ne 'cancelled') { throw "Unexpected cancellation status: $($status | ConvertTo-Json -Compress)" }
    if (-not (Test-Path -LiteralPath $cachePath)) { throw 'First-run cancellation did not create a resumable cache file.' }
    Write-Host 'Translation worker smoke test passed (estimate, incremental add/change/remove, placeholders, QA, cancellation).' -ForegroundColor Green
}
finally {
    if (Test-Path -LiteralPath $testSandbox) { Remove-Item -LiteralPath $testSandbox -Recurse -Force }
}
