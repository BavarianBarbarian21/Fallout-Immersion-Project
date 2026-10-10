param([Parameter(Mandatory=$true)][string]$SourceRoot)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$targetRoot = Join-Path $PSScriptRoot '..\assets\faunaoverhaul\textures'
$mapping = @{
    'exec-a80ad4a3-6f08-4dc9-9677-9716d04cf645.png' = 'item\resource\rawwool.png'
    'exec-1ff3c90f-1586-4a57-81cd-cc466820980d.png' = 'item\resource\hornpieces.png'
    'exec-2006c7b0-9295-485f-ba67-ed05212b2423.png' = 'item\resource\hartshornsalt.png'
    'exec-27910fc0-819c-4d29-b078-df9c910017c0.png' = 'item\food\rawcookie.png'
    'exec-1cf42724-66e9-480a-a94e-1d7ada8a790b.png' = 'item\food\cookie.png'
    'exec-dc751929-e261-4b5e-a0cc-9572a43acc38.png' = 'item\food\charredcookie.png'
}

foreach ($entry in $mapping.GetEnumerator()) {
    $source = Join-Path $SourceRoot $entry.Key
    $target = Join-Path $targetRoot $entry.Value
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    $bitmap = [System.Drawing.Bitmap]::new($source)
    $minX = $bitmap.Width; $minY = $bitmap.Height; $maxX = -1; $maxY = -1
    for ($y = 0; $y -lt $bitmap.Height; $y += 2) {
        for ($x = 0; $x -lt $bitmap.Width; $x += 2) {
            if ($bitmap.GetPixel($x, $y).A -gt 8) {
                if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }
    $width = $maxX - $minX + 1; $height = $maxY - $minY + 1
    $side = [Math]::Max($width, $height)
    $crop = [System.Drawing.Rectangle]::new([Math]::Max(0, $minX - (($side-$width)/2) - 12), [Math]::Max(0, $minY - (($side-$height)/2) - 12), [Math]::Min($side+24, $bitmap.Width), [Math]::Min($side+24, $bitmap.Height))
    $out = [System.Drawing.Bitmap]::new(32, 32, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $gfx = [System.Drawing.Graphics]::FromImage($out)
    $gfx.Clear([System.Drawing.Color]::Transparent)
    $gfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $gfx.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $gfx.DrawImage($bitmap, [System.Drawing.Rectangle]::new(1,1,30,30), $crop, [System.Drawing.GraphicsUnit]::Pixel)
    $out.Save($target, [System.Drawing.Imaging.ImageFormat]::Png)
    $gfx.Dispose(); $out.Dispose(); $bitmap.Dispose()
}

$squirrelPath = Join-Path $targetRoot 'entity\animal\mammal\squirrel.png'
New-Item -ItemType Directory -Path (Split-Path $squirrelPath) -Force | Out-Null
$sq = [System.Drawing.Bitmap]::new(32, 32, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$palette = @(
    [System.Drawing.Color]::FromArgb(255, 63, 35, 23),
    [System.Drawing.Color]::FromArgb(255, 100, 55, 31),
    [System.Drawing.Color]::FromArgb(255, 143, 79, 39),
    [System.Drawing.Color]::FromArgb(255, 184, 111, 55),
    [System.Drawing.Color]::FromArgb(255, 218, 157, 91),
    [System.Drawing.Color]::FromArgb(255, 237, 202, 145)
)
for ($y=0; $y -lt 32; $y++) {
    for ($x=0; $x -lt 32; $x++) {
        $stripe = (($x + [int]($y/2)) % 11)
        $idx = if ($stripe -lt 2) { 0 } elseif ($stripe -lt 4) { 1 } elseif ($stripe -lt 7) { 2 } elseif ($stripe -lt 9) { 3 } elseif ($stripe -eq 9) { 4 } else { 5 }
        $sq.SetPixel($x, $y, $palette[$idx])
    }
}
$sq.Save($squirrelPath, [System.Drawing.Imaging.ImageFormat]::Png)
$sq.Dispose()
