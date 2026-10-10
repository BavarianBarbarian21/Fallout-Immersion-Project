$ErrorActionPreference = 'Stop'
$game = if ($env:VINTAGE_STORY) { $env:VINTAGE_STORY } else { Join-Path $env:APPDATA 'Vintagestory' }
$runtime = Get-ChildItem -LiteralPath (Join-Path $env:ProgramFiles 'dotnet\shared\Microsoft.NETCore.App') -Directory | Where-Object Name -Like '10.*' | Sort-Object Name -Descending | Select-Object -First 1
$csc = Join-Path $env:ProgramFiles 'dotnet\sdk\9.0.305\Roslyn\bincore\csc.dll'
$refs = Get-ChildItem -LiteralPath $runtime.FullName -Filter '*.dll' | Where-Object { try { [Reflection.AssemblyName]::GetAssemblyName($_.FullName) | Out-Null; $true } catch { $false } } | ForEach-Object { '/r:' + $_.FullName }
$gameRefs = @(
    ('/r:' + (Join-Path $game 'VintagestoryAPI.dll'))
    ('/r:' + (Join-Path $game 'VintagestoryLib.dll'))
    ('/r:' + (Join-Path $game 'Lib\Newtonsoft.Json.dll'))
)
$validator = Join-Path $PSScriptRoot 'ShapeAnimationValidator.dll'
& dotnet $csc /noconfig /nostdlib+ /langversion:latest /nullable:enable /target:exe ('/out:' + $validator) $refs $gameRefs (Join-Path $PSScriptRoot 'ShapeAnimationValidator.cs')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& dotnet $validator $args[0]
