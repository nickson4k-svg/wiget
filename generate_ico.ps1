Add-Type -AssemblyName System.Drawing

function Create-AppIcon {
    param([string]$outputPath)

    $sizes = @(256, 128, 64, 48, 32, 16)
    $images = @()

    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)

        $scale = $size / 256.0
        $cx = $size / 2.0
        $cy = $size / 2.0

        # Background rounded badge (Dark sleek slate with glass border)
        $pad = 12 * $scale
        $w = $size - 2 * $pad
        $rad = 48 * $scale
        $rect = [System.Drawing.RectangleF]::new($pad, $pad, $w, $w)
        
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $d = $rad * 2
        $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
        $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
        $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
        $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
        $path.CloseFigure()

        $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            [System.Drawing.PointF]::new($rect.X, $rect.Y),
            [System.Drawing.PointF]::new($rect.Right, $rect.Bottom),
            [System.Drawing.Color]::FromArgb(255, 24, 24, 30),
            [System.Drawing.Color]::FromArgb(255, 12, 12, 16)
        )
        $g.FillPath($bgBrush, $path)

        $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(70, 255, 255, 255), [float][Math]::Max(1.0, 1.5 * $scale))
        $g.DrawPath($borderPen, $path)

        # Concentric Macro Rings
        # Outer Ring: Fats (Amber #F59E0B)
        $penFat = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 245, 158, 11), [float](16 * $scale))
        $penFat.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $penFat.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $rFat = [float](82 * $scale)
        $g.DrawArc($penFat, [float]($cx - $rFat), [float]($cy - $rFat), [float](2 * $rFat), [float](2 * $rFat), [float]-90, [float]280)

        # Middle Ring: Protein (Blue #3B82F6)
        $penProt = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 59, 130, 246), [float](16 * $scale))
        $penProt.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $penProt.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $rProt = [float](57 * $scale)
        $g.DrawArc($penProt, [float]($cx - $rProt), [float]($cy - $rProt), [float](2 * $rProt), [float](2 * $rProt), [float]-90, [float]225)

        # Inner Ring: Carbs (Coral Red #EF4444)
        $penCarb = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 239, 68, 68), [float](16 * $scale))
        $penCarb.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $penCarb.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $rCarb = [float](32 * $scale)
        $g.DrawArc($penCarb, [float]($cx - $rCarb), [float]($cy - $rCarb), [float](2 * $rCarb), [float](2 * $rCarb), [float]-90, [float]310)

        # Center Accent Core: Emerald Leaf / Dot (#10B981)
        $coreBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 16, 185, 129))
        $rCore = [float](10 * $scale)
        $g.FillEllipse($coreBrush, [float]($cx - $rCore), [float]($cy - $rCore), [float](2 * $rCore), [float](2 * $rCore))

        $g.Dispose()

        # Save to PNG bytes
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes = $ms.ToArray()
        $ms.Dispose()
        $bmp.Dispose()

        $images += ,@($size, $pngBytes)
    }

    # Write ICO File
    $fs = [System.IO.File]::Create($outputPath)
    $bw = New-Object System.IO.BinaryWriter($fs)

    # ICONDIR
    $bw.Write([uint16]0) # Reserved
    $bw.Write([uint16]1) # Type 1 = Icon
    $bw.Write([uint16]$images.Count) # Count

    $offset = 6 + (16 * $images.Count)

    # ICONDIRENTRY list
    foreach ($item in $images) {
        $size = $item[0]
        $png = $item[1]

        $bWidth = if ($size -ge 256) { 0 } else { [byte]$size }
        $bHeight = if ($size -ge 256) { 0 } else { [byte]$size }

        $bw.Write([byte]$bWidth)
        $bw.Write([byte]$bHeight)
        $bw.Write([byte]0) # ColorCount
        $bw.Write([byte]0) # Reserved
        $bw.Write([uint16]1) # Planes
        $bw.Write([uint16]32) # BitCount
        $bw.Write([uint32]$png.Length) # BytesInRes
        $bw.Write([uint32]$offset) # ImageOffset

        $offset += $png.Length
    }

    # Image data
    foreach ($item in $images) {
        $png = $item[1]
        $bw.Write($png)
    }

    $bw.Flush()
    $bw.Close()
    $fs.Close()
    Write-Host "Generated $outputPath successfully ($([System.IO.FileInfo]::new($outputPath).Length) bytes)."
}

Create-AppIcon -outputPath "c:\Users\nicks\Desktop\wig\app.ico"
