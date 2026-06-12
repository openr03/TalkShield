# make_icons.ps1 - on.ico / off.ico (다중 해상도) 생성
# 디자인: 카카오 옐로 라운드 사각 + 화이트 말풍선 + 레드 차단 배지
Add-Type -AssemblyName System.Drawing

function Add-RoundRect($path, [single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
    $d = 2 * $r
    $path.AddArc($x,            $y,            $d, $d, 180, 90)
    $path.AddArc($x + $w - $d,  $y,            $d, $d, 270, 90)
    $path.AddArc($x + $w - $d,  $y + $h - $d,  $d, $d,   0, 90)
    $path.AddArc($x,            $y + $h - $d,  $d, $d,  90, 90)
    $path.CloseFigure()
}

function Get-PngBytes([int]$size, [bool]$on) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    [double]$f = $size / 256.0

    # 1) 라운드 사각 배경 (세로 그라데이션)
    $bx = [single](16 * $f); $by = [single](16 * $f)
    $bw = [single](224 * $f); $bh = [single](224 * $f)
    $bgPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    Add-RoundRect $bgPath $bx $by $bw $bh ([single](52 * $f))
    if ($on) {
        $c1 = [System.Drawing.Color]::FromArgb(255, 255, 235, 59)
        $c2 = [System.Drawing.Color]::FromArgb(255, 255, 193, 7)
    } else {
        $c1 = [System.Drawing.Color]::FromArgb(255, 176, 176, 176)
        $c2 = [System.Drawing.Color]::FromArgb(255, 130, 130, 130)
    }
    $rect = [System.Drawing.RectangleF]::new($bx, $by, $bw, $bh)
    $br = [System.Drawing.Drawing2D.LinearGradientBrush]::new($rect, $c1, $c2, [single]90)
    $g.FillPath($br, $bgPath)
    $br.Dispose(); $bgPath.Dispose()

    # 2) 화이트 말풍선 + 꼬리
    $bubble = [System.Drawing.Drawing2D.GraphicsPath]::new()
    Add-RoundRect $bubble ([single](62*$f)) ([single](70*$f)) ([single](132*$f)) ([single](96*$f)) ([single](30*$f))
    $tail = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new([single](96*$f),  [single](158*$f)),
        [System.Drawing.PointF]::new([single](96*$f),  [single](198*$f)),
        [System.Drawing.PointF]::new([single](134*$f), [single](158*$f))
    )
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $g.FillPath($white, $bubble)
    $g.FillPolygon($white, $tail)
    $white.Dispose(); $bubble.Dispose()

    # 3) 레드 차단 배지 (켜짐일 때만)
    if ($on) {
        $rx = [single](150*$f); $ry = [single](150*$f); $rd = [single](80*$f)
        $red = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 229, 57, 53))
        $g.FillEllipse($red, $rx, $ry, $rd, $rd)
        $red.Dispose()
        $penW = [single][Math]::Max(2.5, 11*$f)
        $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, $penW)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        $ins = [single](20*$f)
        $mid = [single]($ry + $rd/2)
        $g.DrawLine($pen, ($rx+$ins), $mid, ($rx+$rd-$ins), $mid)  # 금지(가로 막대)
        $pen.Dispose()
    }

    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return ,$ms.ToArray()
}

function Write-Ico([bool]$on, [string]$outPath) {
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $imgs = @()
    foreach ($s in $sizes) {
        $imgs += [pscustomobject]@{ Size = $s; Bytes = (Get-PngBytes $s $on) }
    }
    $ms = [System.IO.MemoryStream]::new()
    $bw = [System.IO.BinaryWriter]::new($ms)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$imgs.Count)
    $offset = 6 + 16 * $imgs.Count
    foreach ($img in $imgs) {
        $wb = if ($img.Size -ge 256) { 0 } else { $img.Size }
        $bw.Write([byte]$wb); $bw.Write([byte]$wb)
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$img.Bytes.Length); $bw.Write([uint32]$offset)
        $offset += $img.Bytes.Length
    }
    foreach ($img in $imgs) { $bw.Write($img.Bytes) }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
    $bw.Dispose(); $ms.Dispose()
    Write-Host ("생성: {0} ({1:N1} KB)" -f $outPath, ((Get-Item $outPath).Length / 1KB))
}

Write-Ico $true  (Join-Path $PSScriptRoot 'on.ico')
Write-Ico $false (Join-Path $PSScriptRoot 'off.ico')
