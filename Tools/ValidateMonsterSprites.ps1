param(
    [Parameter(Mandatory = $true)]
    [int]$StartNo,

    [Parameter(Mandatory = $true)]
    [int]$EndNo,

    [string]$SpriteDir = "Assets/Sprites/MonsterGenV2",
    [int]$SpriteSize = 48,
    [string]$PreviewPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$items = @()
foreach ($no in $StartNo..$EndNo) {
    $file = Get-ChildItem -LiteralPath $SpriteDir -Filter ("{0:D3}_*.png" -f $no) | Select-Object -First 1
    if (-not $file) {
        $items += [pscustomobject]@{
            No = $no
            Name = "MISSING"
            Width = 0
            Height = 0
            NonTransparent = 0
            TransparentCorners = $false
            HasMeta = $false
        }
        continue
    }

    $bmp = [System.Drawing.Bitmap]::new($file.FullName)
    try {
        $w = [int]$bmp.Width
        $h = [int]$bmp.Height
        $nonTransparent = 0

        for ($y = 0; $y -lt $h; $y++) {
            for ($x = 0; $x -lt $w; $x++) {
                if ($bmp.GetPixel($x, $y).A -gt 0) {
                    $nonTransparent++
                }
            }
        }

        $cornerAlpha = @(
            $bmp.GetPixel(0, 0).A,
            $bmp.GetPixel(($w - 1), 0).A,
            $bmp.GetPixel(0, ($h - 1)).A,
            $bmp.GetPixel(($w - 1), ($h - 1)).A
        )
        $transparentCorners = (($cornerAlpha | Where-Object { $_ -eq 0 }).Count -eq 4)

        $items += [pscustomobject]@{
            No = $no
            Name = $file.Name
            Width = $w
            Height = $h
            NonTransparent = $nonTransparent
            TransparentCorners = $transparentCorners
            HasMeta = (Test-Path -LiteralPath ($file.FullName + ".meta"))
        }
    }
    finally {
        $bmp.Dispose()
    }
}

$items | Format-Table -AutoSize

$bad = @(
    $items | Where-Object {
        $_.Name -eq "MISSING" -or
        $_.Width -ne $SpriteSize -or
        $_.Height -ne $SpriteSize -or
        $_.NonTransparent -le 0 -or
        -not $_.TransparentCorners -or
        -not $_.HasMeta
    }
)

if ($bad.Count -gt 0) {
    throw "Validation failed for $($bad.Count) sprites."
}

if ($PreviewPath -ne "") {
    $scale = 6
    $cell = ($SpriteSize + 16) * $scale
    $count = $EndNo - $StartNo + 1
    $cols = [Math]::Min(5, $count)
    $rows = [int][Math]::Ceiling($count / [double]$cols)
    $canvas = [System.Drawing.Bitmap]::new($cell * $cols, $cell * $rows, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)

    try {
        $graphics.Clear([System.Drawing.Color]::FromArgb(36, 37, 41))
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

        $i = 0
        foreach ($no in $StartNo..$EndNo) {
            $file = Get-ChildItem -LiteralPath $SpriteDir -Filter ("{0:D3}_*.png" -f $no) | Select-Object -First 1
            $img = [System.Drawing.Bitmap]::new($file.FullName)
            try {
                $x = [int](($i % $cols) * $cell + 8 * $scale)
                $y = [int]([Math]::Floor($i / $cols) * $cell + 4 * $scale)
                $dst = [System.Drawing.Rectangle]::new($x, $y, $SpriteSize * $scale, $SpriteSize * $scale)
                $graphics.DrawImage($img, $dst, 0, 0, $SpriteSize, $SpriteSize, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally {
                $img.Dispose()
            }
            $i++
        }
    }
    finally {
        $graphics.Dispose()
    }

    $canvas.Save($PreviewPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose()
    "Preview: $PreviewPath"
}
