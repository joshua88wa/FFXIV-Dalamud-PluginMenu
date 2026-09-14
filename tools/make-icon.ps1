# Draws images/icon.png for the Plugin Menu plugin.
# Kept in the repo so the icon can be adjusted and regenerated rather than
# re-drawn by hand. Run it from anywhere: paths are resolved from this file.

Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$outDir = Join-Path $root 'images'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$out = Join-Path $outDir 'icon.png'

$size = 512

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

$bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)

# Background: a rounded square so it reads as a tile at installer size.
$bg = New-RoundedPath 0 0 $size $size 112
$rect = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $rect,
    [System.Drawing.Color]::FromArgb(255, 48, 57, 88),
    [System.Drawing.Color]::FromArgb(255, 20, 24, 36),
    90)
$g.FillPath($bgBrush, $bg)

$edge = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60, 255, 255, 255), 5)
$g.DrawPath($edge, $bg)

# Three list rows: a coloured plugin tile and a name bar, which is what the menu
# actually looks like. Colours match the highlight palette in Nearby Player List.
$tileColours = @(
    [System.Drawing.Color]::FromArgb(255, 255, 205, 64),
    [System.Drawing.Color]::FromArgb(255, 90, 216, 255),
    [System.Drawing.Color]::FromArgb(255, 110, 220, 130)
)
$barWidths = @(210, 176, 90)
$rowTops = @(118, 224, 330)
$barBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(235, 232, 236, 245))

for ($i = 0; $i -lt 3; $i++) {
    $tile = New-RoundedPath 92 $rowTops[$i] 80 80 22
    $tileBrush = New-Object System.Drawing.SolidBrush($tileColours[$i])
    $g.FillPath($tileBrush, $tile)
    $tileBrush.Dispose()

    $bar = New-RoundedPath 196 ($rowTops[$i] + 27) $barWidths[$i] 26 13
    $g.FillPath($barBrush, $bar)
    $bar.Dispose()
    $tile.Dispose()
}

# A cog in the corner, sunk into a dark recess so it stays legible over the rows.
$cx = 386.0
$cy = 372.0
$recess = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 20, 24, 36))
$metal = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 232, 236, 245))

$g.FillEllipse($recess, $cx - 94, $cy - 94, 188, 188)

$state = $g.Save()
$g.TranslateTransform($cx, $cy)
for ($i = 0; $i -lt 8; $i++) {
    $g.RotateTransform(45)
    $tooth = New-RoundedPath (-15) (-78) 30 36 10
    $g.FillPath($metal, $tooth)
    $tooth.Dispose()
}
$g.Restore($state)

$g.FillEllipse($metal, $cx - 54, $cy - 54, 108, 108)
$g.FillEllipse($recess, $cx - 22, $cy - 22, 44, 44)

$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)

$g.Dispose()
$bmp.Dispose()
$bgBrush.Dispose()
$edge.Dispose()
$barBrush.Dispose()
$recess.Dispose()
$metal.Dispose()

"wrote $out"
