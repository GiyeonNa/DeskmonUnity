param(
    [Parameter(Mandatory = $true)]
    [string]$MapPath,

    [string]$OutputDir = "Assets/Sprites/MonsterGenV2",
    [int]$SpriteSize = 48,
    [string]$MetaTemplate = "Assets/Sprites/mongle.png.meta"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$code = @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class MonsterBatchPost {
    static bool IsKey(byte r, byte g, byte b) {
        return r > 180 && b > 170 && g < 120 && (r - g) > 80 && (b - g) > 80 && Math.Abs(r - b) < 90;
    }
    static byte Quant(byte v) { return (byte)Math.Max(0, Math.Min(255, ((int)Math.Round(v / 17.0)) * 17)); }

    public static void Convert(string srcPath, string outPath, int maxSize, int spriteSize) {
        using (var original = new Bitmap(srcPath))
        using (var img = new Bitmap(original.Width, original.Height, PixelFormat.Format32bppArgb)) {
            using (var g0 = Graphics.FromImage(img)) {
                g0.Clear(Color.Transparent);
                g0.DrawImage(original, 0, 0, original.Width, original.Height);
            }

            var rect = new Rectangle(0, 0, img.Width, img.Height);
            var data = img.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            int stride = Math.Abs(data.Stride);
            byte[] bytes = new byte[stride * img.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            img.UnlockBits(data);

            int minX = img.Width, minY = img.Height, maxX = -1, maxY = -1;
            for (int y = 0; y < img.Height; y++) {
                int row = y * stride;
                for (int x = 0; x < img.Width; x++) {
                    int i = row + x * 4;
                    byte b = bytes[i], g = bytes[i + 1], r = bytes[i + 2], a = bytes[i + 3];
                    if (a > 0 && !IsKey(r, g, b)) {
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < 0) throw new Exception("No subject pixels found: " + srcPath);

            int pad = 8;
            minX = Math.Max(0, minX - pad);
            minY = Math.Max(0, minY - pad);
            maxX = Math.Min(img.Width - 1, maxX + pad);
            maxY = Math.Min(img.Height - 1, maxY + pad);
            int cw = maxX - minX + 1, ch = maxY - minY + 1;

            using (var crop = new Bitmap(cw, ch, PixelFormat.Format32bppArgb)) {
                var cdata = crop.LockBits(new Rectangle(0, 0, cw, ch), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                int cstride = Math.Abs(cdata.Stride);
                byte[] cb = new byte[cstride * ch];
                for (int y = 0; y < ch; y++) {
                    int srcRow = (minY + y) * stride;
                    int dstRow = y * cstride;
                    for (int x = 0; x < cw; x++) {
                        int si = srcRow + (minX + x) * 4;
                        int di = dstRow + x * 4;
                        byte b = bytes[si], gg = bytes[si + 1], r = bytes[si + 2], a = bytes[si + 3];
                        if (a == 0 || IsKey(r, gg, b)) {
                            cb[di + 3] = 0;
                        } else {
                            cb[di] = b;
                            cb[di + 1] = gg;
                            cb[di + 2] = r;
                            cb[di + 3] = 255;
                        }
                    }
                }
                Marshal.Copy(cb, 0, cdata.Scan0, cb.Length);
                crop.UnlockBits(cdata);

                using (var canvas = new Bitmap(spriteSize, spriteSize, PixelFormat.Format32bppArgb)) {
                    using (var g = Graphics.FromImage(canvas)) {
                        g.Clear(Color.Transparent);
                        g.CompositingMode = CompositingMode.SourceOver;
                        g.CompositingQuality = CompositingQuality.HighSpeed;
                        g.SmoothingMode = SmoothingMode.None;
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        double scale = Math.Min(maxSize / (double)cw, maxSize / (double)ch);
                        int dw = Math.Max(1, (int)Math.Round(cw * scale));
                        int dh = Math.Max(1, (int)Math.Round(ch * scale));
                        int dx = (spriteSize - dw) / 2;
                        int bottomPad = Math.Max(4, (int)Math.Round(spriteSize / 12.0));
                        int dy = spriteSize - dh - bottomPad;
                        g.DrawImage(crop, new Rectangle(dx, dy, dw, dh), new Rectangle(0, 0, cw, ch), GraphicsUnit.Pixel);
                    }

                    var odata = canvas.LockBits(new Rectangle(0, 0, spriteSize, spriteSize), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                    int ostride = Math.Abs(odata.Stride);
                    byte[] ob = new byte[ostride * spriteSize];
                    Marshal.Copy(odata.Scan0, ob, 0, ob.Length);
                    for (int y = 0; y < spriteSize; y++) {
                        int row = y * ostride;
                        for (int x = 0; x < spriteSize; x++) {
                            int i = row + x * 4;
                            if (ob[i + 3] < 80) {
                                ob[i] = 0; ob[i + 1] = 0; ob[i + 2] = 0; ob[i + 3] = 0;
                            } else {
                                ob[i] = Quant(ob[i]);
                                ob[i + 1] = Quant(ob[i + 1]);
                                ob[i + 2] = Quant(ob[i + 2]);
                                ob[i + 3] = 255;
                            }
                        }
                    }
                    Marshal.Copy(ob, 0, odata.Scan0, ob.Length);
                    canvas.UnlockBits(odata);
                    canvas.Save(outPath, ImageFormat.Png);
                }
            }
        }
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing

if (-not (Test-Path -LiteralPath $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$items = Import-Csv -LiteralPath $MapPath
foreach ($item in $items) {
    $dst = Join-Path $OutputDir $item.Name
    [MonsterBatchPost]::Convert($item.Src, $dst, [int]$item.Max, $SpriteSize)
}

if (Test-Path -LiteralPath $MetaTemplate) {
    $template = Get-Content -LiteralPath $MetaTemplate -Raw
    Get-ChildItem -LiteralPath $OutputDir -Filter "*.png" | ForEach-Object {
        $metaPath = $_.FullName + ".meta"
        if (-not (Test-Path -LiteralPath $metaPath)) {
            $guid = [guid]::NewGuid().ToString("N")
            $meta = $template -replace "guid: [0-9a-f]+", "guid: $guid"
            Set-Content -LiteralPath $metaPath -Value $meta -Encoding UTF8
        }
    }
}

"Postprocessed $($items.Count) monster sprites into $OutputDir at ${SpriteSize}x${SpriteSize}"
