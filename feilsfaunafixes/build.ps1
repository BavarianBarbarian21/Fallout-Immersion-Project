$ErrorActionPreference = 'Stop'
$game = if ($env:VINTAGE_STORY) { $env:VINTAGE_STORY } else { Join-Path $env:APPDATA 'Vintagestory' }
$runtime = Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.NETCore.App') -Directory | Where-Object Name -Like '10.*' | Sort-Object Name -Descending | Select-Object -First 1
if (-not $runtime) { throw 'A .NET 10 runtime is required (it is bundled with Vintage Story 1.22.7 installations).' }
$csc = Join-Path $env:ProgramFiles 'dotnet\sdk\9.0.305\Roslyn\bincore\csc.dll'
if (-not (Test-Path $csc)) { $csc = (Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\sdk') -Recurse -Filter csc.dll | Where-Object FullName -Match 'Roslyn\\bincore' | Select-Object -Last 1).FullName }
$refs = Get-ChildItem -LiteralPath $runtime.FullName -Filter '*.dll' | Where-Object { try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; $true } catch { $false } } | ForEach-Object { '/r:' + $_.FullName }
$gameRefs = @(
    ('/r:' + (Join-Path $game 'VintagestoryAPI.dll'))
    ('/r:' + (Join-Path $game 'VintagestoryLib.dll'))
    ('/r:' + (Join-Path $game 'Mods\VSEssentials.dll'))
    ('/r:' + (Join-Path $game 'Mods\VSSurvivalMod.dll'))
    ('/r:' + (Join-Path $game 'Lib\Newtonsoft.Json.dll'))
)
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Select-Object -ExpandProperty FullName
$outputArg = '/out:' + (Join-Path $PSScriptRoot 'feilsfaunafixes.dll')
& dotnet $csc /noconfig /nostdlib+ /langversion:latest /nullable:enable /target:library $outputArg $refs $gameRefs $sources
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
