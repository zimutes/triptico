# Gera src/Triptico/Assets/triptico.ico (três painéis = tríptico).
# Tamanhos pequenos em BMP 32 bits (melhor para a bandeja), 256 em PNG.
param([string]$Destino = (Join-Path $PSScriptRoot '..\src\Triptico\Assets\triptico.ico'))

Add-Type -AssemblyName System.Drawing

function New-RoundedPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    if ($r -le 0.5) { $p.AddRectangle((New-Object System.Drawing.RectangleF($x, $y, $w, $h))); return $p }
    $d = 2 * $r
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function Draw-Icon([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Fundo: quadrado arredondado em gradiente índigo -> azul-céu.
    $bg = New-RoundedPath 0 0 $s $s ([Math]::Max(2, $s * 0.22))
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)), (New-Object System.Drawing.PointF($s, $s)),
        [System.Drawing.Color]::FromArgb(255, 79, 70, 229), [System.Drawing.Color]::FromArgb(255, 14, 165, 233))
    $g.FillPath($grad, $bg)

    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $soft = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(185, 255, 255, 255))

    if ($s -le 24) {
        # Versão simplificada, alinhada ao píxel.
        $u = $s / 16.0
        $g.SmoothingMode = 'None'
        $g.FillRectangle($soft, [float](2 * $u), [float](5 * $u), [float](3 * $u), [float](6 * $u))
        $g.FillRectangle($white, [float](6 * $u), [float](4 * $u), [float](4 * $u), [float](8 * $u))
        $g.FillRectangle($soft, [float](11 * $u), [float](5 * $u), [float](3 * $u), [float](6 * $u))
    } else {
        $r = $s * 0.035
        $g.FillPath($soft, (New-RoundedPath ($s * 0.12) ($s * 0.31) ($s * 0.22) ($s * 0.30) $r))
        $g.FillPath($white, (New-RoundedPath ($s * 0.37) ($s * 0.24) ($s * 0.26) ($s * 0.40) $r))
        $g.FillPath($soft, (New-RoundedPath ($s * 0.66) ($s * 0.31) ($s * 0.22) ($s * 0.30) $r))
        # Pé do ecrã do meio.
        $g.FillRectangle($white, [float]($s * 0.475), [float]($s * 0.64), [float]($s * 0.05), [float]($s * 0.08))
        $g.FillPath($white, (New-RoundedPath ($s * 0.40) ($s * 0.72) ($s * 0.20) ($s * 0.04) ($s * 0.02)))
    }
    $g.Dispose()
    return $bmp
}

function Get-DibBytes([System.Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    # BITMAPINFOHEADER (altura a dobrar: cor + máscara)
    $bw.Write([int]40); $bw.Write([int]$s); $bw.Write([int]($s * 2)); $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
        }
    }
    $maskRow = [int]([Math]::Ceiling($s / 32.0) * 4)
    $bw.Write((New-Object byte[] ($maskRow * $s)))
    $bw.Flush()
    return $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$images = foreach ($s in $sizes) {
    $bmp = Draw-Icon $s
    if ($s -ge 256) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        ,@($s, $ms.ToArray())
    } else {
        ,@($s, (Get-DibBytes $bmp))
    }
    $bmp.Dispose()
}

New-Item -ItemType Directory -Force (Split-Path $Destino) | Out-Null
$fs = [System.IO.File]::Create($Destino)
$w = New-Object System.IO.BinaryWriter($fs)
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $s = $img[0]; $data = $img[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$data.Length); $w.Write([int]$offset)
    $offset += $data.Length
}
foreach ($img in $images) { $w.Write([byte[]]$img[1]) }
$w.Close()
Write-Host "Icone gerado: $Destino"

