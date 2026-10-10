$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$textureRoot = Join-Path $PSScriptRoot '..\assets\faunaoverhaul\textures'

function Save-MottledTexture {
    param(
        [string]$RelativePath,
        [System.Drawing.Color[]]$Palette,
        [int]$Size = 16,
        [hashtable]$AccentPixels = @{}
    )
    $target = Join-Path $textureRoot $RelativePath
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    for ($y = 0; $y -lt $Size; $y++) {
        for ($x = 0; $x -lt $Size; $x++) {
            $hash = ([int64]($x + 17) * 73856093) -bxor ([int64]($y + 31) * 19349663) -bxor ([int64]($x * $y + 11) * 83492791)
            $index = [Math]::Abs($hash % $Palette.Count)
            $key = "$x,$y"
            $color = if ($AccentPixels.ContainsKey($key)) { $AccentPixels[$key] } else { $Palette[$index] }
            $bitmap.SetPixel($x, $y, $color)
        }
    }
    $bitmap.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
}

$wool = @(
    [Drawing.Color]::FromArgb(255, 102, 91, 72),
    [Drawing.Color]::FromArgb(255, 142, 129, 103),
    [Drawing.Color]::FromArgb(255, 181, 168, 137),
    [Drawing.Color]::FromArgb(255, 213, 202, 172),
    [Drawing.Color]::FromArgb(255, 236, 227, 199)
)
Save-MottledTexture 'item\resource\rawwool.png' $wool 16

$raw = @(
    [Drawing.Color]::FromArgb(255, 118, 91, 57),
    [Drawing.Color]::FromArgb(255, 151, 119, 75),
    [Drawing.Color]::FromArgb(255, 184, 151, 102),
    [Drawing.Color]::FromArgb(255, 211, 182, 132)
)
$baked = @(
    [Drawing.Color]::FromArgb(255, 92, 48, 22),
    [Drawing.Color]::FromArgb(255, 132, 70, 27),
    [Drawing.Color]::FromArgb(255, 174, 98, 38),
    [Drawing.Color]::FromArgb(255, 211, 137, 60)
)
$charred = @(
    [Drawing.Color]::FromArgb(255, 25, 20, 18),
    [Drawing.Color]::FromArgb(255, 43, 29, 22),
    [Drawing.Color]::FromArgb(255, 65, 39, 26),
    [Drawing.Color]::FromArgb(255, 91, 51, 29)
)
$chips = @{
    '3,4'=[Drawing.Color]::FromArgb(255,55,29,17); '10,3'=[Drawing.Color]::FromArgb(255,63,32,17)
    '7,7'=[Drawing.Color]::FromArgb(255,52,27,16); '12,10'=[Drawing.Color]::FromArgb(255,62,31,18)
    '4,12'=[Drawing.Color]::FromArgb(255,58,28,17); '9,13'=[Drawing.Color]::FromArgb(255,57,29,18)
}
Save-MottledTexture 'item\food\rawcookie.png' $raw 16 @{'5,5'=[Drawing.Color]::FromArgb(255,92,70,45);'11,9'=[Drawing.Color]::FromArgb(255,95,72,46)}
Save-MottledTexture 'item\food\cookie.png' $baked 16 $chips
Save-MottledTexture 'item\food\charredcookie.png' $charred 16 @{'7,7'=[Drawing.Color]::FromArgb(255,15,13,12);'3,10'=[Drawing.Color]::FromArgb(255,17,14,12)}

$rust = @(
    [Drawing.Color]::FromArgb(255, 82, 39, 25),
    [Drawing.Color]::FromArgb(255, 112, 52, 28),
    [Drawing.Color]::FromArgb(255, 145, 67, 32),
    [Drawing.Color]::FromArgb(255, 171, 88, 43)
)
$dark = @(
    [Drawing.Color]::FromArgb(255, 42, 29, 24),
    [Drawing.Color]::FromArgb(255, 58, 35, 26),
    [Drawing.Color]::FromArgb(255, 76, 42, 28),
    [Drawing.Color]::FromArgb(255, 91, 49, 30)
)
$cream = @(
    [Drawing.Color]::FromArgb(255, 151, 123, 87),
    [Drawing.Color]::FromArgb(255, 184, 151, 105),
    [Drawing.Color]::FromArgb(255, 211, 181, 132),
    [Drawing.Color]::FromArgb(255, 229, 205, 160)
)
$black = @(
    [Drawing.Color]::FromArgb(255, 12, 10, 9),
    [Drawing.Color]::FromArgb(255, 24, 18, 15)
)
Save-MottledTexture 'entity\animal\mammal\squirrel-body.png' $rust 16
Save-MottledTexture 'entity\animal\mammal\squirrel-dark.png' $dark 16
Save-MottledTexture 'entity\animal\mammal\squirrel-cream.png' $cream 16
Save-MottledTexture 'entity\animal\mammal\squirrel-black.png' $black 16
Copy-Item -LiteralPath (Join-Path $textureRoot 'entity\animal\mammal\squirrel-body.png') -Destination (Join-Path $textureRoot 'entity\animal\mammal\squirrel.png') -Force

$gameRoot = if ($env:VINTAGE_STORY) { $env:VINTAGE_STORY } else { Join-Path $env:APPDATA 'Vintagestory' }
Copy-Item -LiteralPath (Join-Path $gameRoot 'assets\survival\textures\block\creature\bone.png') -Destination (Join-Path $textureRoot 'item\resource\hornpieces.png') -Force
Copy-Item -LiteralPath (Join-Path $gameRoot 'assets\survival\textures\item\resource\crushed\rocksalt.png') -Destination (Join-Path $textureRoot 'item\resource\hartshornsalt.png') -Force
