$basePath = 'c:\Users\km-fei-mat\source\repos\FIP-Mods'
$parts = 1..4 | ForEach-Object { "FIP-Translation Part $_" }
$allMissing  = [System.Collections.Generic.List[string]]::new()
$allMismatch = [System.Collections.Generic.List[PSCustomObject]]::new()

foreach ($part in $parts) {
    $langRoot = [IO.Path]::Combine($basePath, $part, 'Languages')
    $engPath  = [IO.Path]::Combine($langRoot, 'English')
    if (-not (Test-Path $engPath)) { Write-Host "SKIP $part"; continue }

    # Pre-cache English entry counts (count  <tag> lines, ignore blank/header lines)
    $engMap = @{}
    Get-ChildItem $engPath -Recurse -Filter '*.xml' | ForEach-Object {
        $rel = $_.FullName.Substring($engPath.Length + 1)
        $engMap[$rel] = ([System.IO.File]::ReadAllLines($_.FullName) -match '<[A-Za-z_][^>]*>[^<]*</').Count
    }

    $languages = Get-ChildItem $langRoot -Directory | Where-Object Name -ne 'English'
    Write-Host "$part`: $($engMap.Count) eng files, $($languages.Count) other langs"

    foreach ($lang in $languages) {
        # Index existing language files
        $langFiles = @{}
        Get-ChildItem $lang.FullName -Recurse -Filter '*.xml' -ErrorAction SilentlyContinue | ForEach-Object {
            $rel = $_.FullName.Substring($lang.FullName.Length + 1)
            $langFiles[$rel] = $_.FullName
        }

        foreach ($rel in $engMap.Keys) {
            if (-not $langFiles.ContainsKey($rel)) {
                $allMissing.Add("[$part] $($lang.Name) / $rel  (eng=$($engMap[$rel])entries)")
            } else {
                $ll = ([System.IO.File]::ReadAllLines($langFiles[$rel]) -match '<[A-Za-z_][^>]*>[^<]*</').Count
                if ($ll -ne $engMap[$rel]) {
                    $allMismatch.Add([PSCustomObject]@{
                        Part=$part; Lang=$lang.Name; File=$rel
                        EngLines=$engMap[$rel]; LangLines=$ll
                    })
                }
            }
        }
    }
}

Write-Host ""
Write-Host "=== SUMMARY ==="
Write-Host "  MISSING : $($allMissing.Count)"
Write-Host "  MISMATCH: $($allMismatch.Count)"

if ($allMissing.Count -gt 0) {
    Write-Host ""
    Write-Host "=== MISSING FILES ==="
    $allMissing | Sort-Object | ForEach-Object { Write-Host "  $_" }
}

if ($allMismatch.Count -gt 0) {
    Write-Host ""
    Write-Host "=== LINE MISMATCHES ==="
    $allMismatch | Sort-Object Part,Lang,File | ForEach-Object {
        Write-Host "  [$($_.Part)] $($_.Lang) / $($_.File)  (eng=$($_.EngLines) lang=$($_.LangLines))"
    }
}

Write-Host ""
Write-Host "=== BY PART + LANGUAGE (issues only) ==="
$combined = @()
foreach ($m in $allMissing)  {
    if ($m -match '^\[([^\]]+)\] ([^ ]+)') { $combined += [PSCustomObject]@{Key="$($Matches[1])|$($Matches[2])"; Type='M'} }
}
foreach ($m in $allMismatch) { $combined += [PSCustomObject]@{Key="$($m.Part)|$($m.Lang)"; Type='X'} }
$combined | Group-Object Key | Sort-Object Name | ForEach-Object {
    $ms = ($_.Group | Where-Object Type -eq 'M').Count
    $xs = ($_.Group | Where-Object Type -eq 'X').Count
    Write-Host "  $($_.Name): MISSING=$ms MISMATCH=$xs"
}
