Add-Type -AssemblyName System.Drawing

function Generate-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $scale = $size / 256.0

    # Заокруглена преміальна темна підкладка (Squircle)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0, 0)),
        (New-Object System.Drawing.PointF($size, $size)),
        [System.Drawing.Color]::FromArgb(255, 26, 26, 32),
        [System.Drawing.Color]::FromArgb(255, 14, 14, 18)
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rad = 48.0 * $scale
    $rect = New-Object System.Drawing.RectangleF(8 * $scale, 8 * $scale, 240 * $scale, 240 * $scale)
    $path.AddArc($rect.X, $rect.Y, $rad * 2, $rad * 2, 180, 90)
    $path.AddArc($rect.Right - $rad * 2, $rect.Y, $rad * 2, $rad * 2, 270, 90)
    $path.AddArc($rect.Right - $rad * 2, $rect.Bottom - $rad * 2, $rad * 2, $rad * 2, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $rad * 2, $rad * 2, $rad * 2, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # Тонка рамка навколо підкладки
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 255, 255, 255), [Math]::Max(1.0, 1.5 * $scale))
    $g.DrawPath($borderPen, $path)

    # Центр: (128, 128)
    $cx = 128.0 * $scale
    $cy = 128.0 * $scale

    # 1. Зовнішнє кільце (Жири - бурштин #F59E0B)
    $penFat = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 245, 158, 11), 16.0 * $scale)
    $penFat.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penFat.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $rFat = 82.0 * $scale
    $g.DrawArc($penFat, ($cx - $rFat), ($cy - $rFat), ($rFat * 2), ($rFat * 2), -90, 270)

    # 2. Середнє кільце (Білки - синій #3B82F6)
    $penProt = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 59, 130, 246), 16.0 * $scale)
    $penProt.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penProt.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $rProt = 58.0 * $scale
    $g.DrawArc($penProt, ($cx - $rProt), ($cy - $rProt), ($rProt * 2), ($rProt * 2), -90, 220)

    # 3. Внутрішнє кільце (Вуглеводи - червоний #EF4444)
    $penCarbs = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 239, 68, 68), 16.0 * $scale)
    $penCarbs.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penCarbs.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $rCarbs = 34.0 * $scale
    $g.DrawArc($penCarbs, ($cx - $rCarbs), ($cy - $rCarbs), ($rCarbs * 2), ($rCarbs * 2), -90, 310)

    # 4. Центр: Смарагдова крапка / вогник (#10B981)
    $sparkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 16, 185, 129))
    $rCenter = 12.0 * $scale
    $g.FillEllipse($sparkBrush, ($cx - $rCenter), ($cy - $rCenter), ($rCenter * 2), ($rCenter * 2))

    $g.Flush()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $g.Dispose()

    return $ms.ToArray()
}

$sizes = @(256, 64, 48, 32, 16)
$pngs = @()

foreach ($s in $sizes) {
    $pngs += ,(Generate-IconPng $s)
}

# Складання валідного Windows ICO файлу
$icoMs = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($icoMs)

# ICONDIR
$writer.Write([uint16]0)          # idReserved
$writer.Write([uint16]1)          # idType (1 = Icon)
$writer.Write([uint16]$sizes.Count) # idCount

$offset = 6 + (16 * $sizes.Count)

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $data = $pngs[$i]

    $wByte = if ($sz -ge 256) { 0 } else { [byte]$sz }
    $hByte = if ($sz -ge 256) { 0 } else { [byte]$sz }

    $writer.Write([byte]$wByte)        # bWidth
    $writer.Write([byte]$hByte)        # bHeight
    $writer.Write([byte]0)             # bColorCount
    $writer.Write([byte]0)             # bReserved
    $writer.Write([uint16]1)           # wPlanes
    $writer.Write([uint16]32)          # wBitCount
    $writer.Write([uint32]$data.Length)# dwBytesInRes
    $writer.Write([uint32]$offset)     # dwImageOffset

    $offset += $data.Length
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $writer.Write($pngs[$i])
}

$writer.Flush()
$destFile = "c:\Users\nicks\Desktop\wig\app.ico"
[System.IO.File]::WriteAllBytes($destFile, $icoMs.ToArray())
$writer.Dispose()
$icoMs.Dispose()

Write-Output "app.ico generated successfully at $destFile"
