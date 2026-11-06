Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$outIcon = Join-Path $PSScriptRoot 'app.ico'
$preview = Join-Path $root '.qa\icon-preview.png'

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$pngs = @()

function New-RoundPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $m = $s * 0.055
    $side = $s - 2 * $m
    $r = $side * 0.24
    $shape = New-RoundPath $m $m $side $side $r
    $c1 = [System.Drawing.ColorTranslator]::FromHtml('#7C9CFF')
    $c2 = [System.Drawing.ColorTranslator]::FromHtml('#B18CFF')
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.RectangleF($m, $m, $side, $side)), $c1, $c2, 55.0)
    $g.FillPath($brush, $shape)
    $brush.Dispose()

    $border = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 255, 255, 255), [Math]::Max(0.7, $s * 0.008))
    $g.DrawPath($border, $shape)
    $border.Dispose()

    $cx = $s / 2.0
    $cy = $s / 2.0
    $clockR = $s * 0.255
    $clockPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(245, 255, 255, 255), [Math]::Max(0.9, $s * 0.062))
    $clockPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $clockPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawEllipse($clockPen, $cx - $clockR, $cy - $clockR, $clockR * 2, $clockR * 2)
    $g.DrawLine($clockPen, $cx, $cy, $cx, $cy - $clockR * 0.72)
    $g.DrawLine($clockPen, $cx, $cy, $cx + $clockR * 0.62, $cy + $clockR * 0.30)
    $clockPen.Dispose()

    $dot = [Math]::Max(1.3, $s * 0.038)
    $dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(250, 255, 255, 255))
    $g.FillEllipse($dotBrush, $cx - $dot / 2, $cy - $dot / 2, $dot, $dot)
    $dotBrush.Dispose()

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,$ms.ToArray()
    if ($s -eq 256) { $bmp.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    $ms.Dispose()
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $dim = if ($s -eq 256) { 0 } else { $s }
    $bw.Write([byte]$dim)
    $bw.Write([byte]$dim)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngs[$i].Length)
    $bw.Write([UInt32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Flush()
[System.IO.File]::WriteAllBytes($outIcon, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()

Write-Host ("icon written: {0} ({1} bytes)" -f $outIcon, (Get-Item $outIcon).Length)
