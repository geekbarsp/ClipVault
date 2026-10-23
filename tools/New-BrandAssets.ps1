param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$assetRoot = Join-Path $projectRoot 'src\ClipVault.App\Assets'
$brandRoot = Join-Path $assetRoot 'Brand'
$masterLogoPath = Join-Path $brandRoot 'ClipVaultLogo.png'
$masterIconPath = Join-Path $brandRoot 'ClipVaultIcon.png'

function New-TransparentBitmap {
    param(
        [System.Drawing.Image]$Image,
        [System.Drawing.Rectangle]$SourceRectangle,
        [int]$Width,
        [int]$Height,
        [double]$Fill
    )

    $canvas = [System.Drawing.Bitmap]::new($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $canvas.SetResolution(96, 96)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $scale = [Math]::Min(($Width * $Fill) / $SourceRectangle.Width, ($Height * $Fill) / $SourceRectangle.Height)
        $drawWidth = [int][Math]::Round($SourceRectangle.Width * $scale)
        $drawHeight = [int][Math]::Round($SourceRectangle.Height * $scale)
        $destination = [System.Drawing.Rectangle]::new(
            [int](($Width - $drawWidth) / 2),
            [int](($Height - $drawHeight) / 2),
            $drawWidth,
            $drawHeight)
        $graphics.DrawImage($Image, $destination, $SourceRectangle, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }
    return $canvas
}

function Save-PngAsset {
    param(
        [System.Drawing.Image]$Image,
        [System.Drawing.Rectangle]$SourceRectangle,
        [int]$Width,
        [int]$Height,
        [double]$Fill,
        [string]$Path
    )

    $bitmap = New-TransparentBitmap $Image $SourceRectangle $Width $Height $Fill
    try { $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
}

function Get-PngBytes {
    param([System.Drawing.Image]$Image, [int]$Size)

    $sourceRectangle = [System.Drawing.Rectangle]::new(0, 0, $Image.Width, $Image.Height)
    $bitmap = New-TransparentBitmap $Image $sourceRectangle $Size $Size 0.92
    $stream = [System.IO.MemoryStream]::new()
    try {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return $stream.ToArray()
    }
    finally {
        $stream.Dispose()
        $bitmap.Dispose()
    }
}

Copy-Item -LiteralPath $SourcePath -Destination $masterLogoPath -Force
$source = [System.Drawing.Bitmap]::FromFile($SourcePath)
try {
    $shieldCrop = [System.Drawing.Rectangle]::new(300, 45, 735, 850)
    Save-PngAsset $source $shieldCrop 1024 1024 0.90 $masterIconPath

    $icon = [System.Drawing.Bitmap]::FromFile($masterIconPath)
    try {
        $iconRectangle = [System.Drawing.Rectangle]::new(0, 0, $icon.Width, $icon.Height)
        Save-PngAsset $icon $iconRectangle 300 300 0.88 (Join-Path $assetRoot 'Square150x150Logo.scale-200.png')
        Save-PngAsset $icon $iconRectangle 88 88 0.88 (Join-Path $assetRoot 'Square44x44Logo.scale-200.png')
        Save-PngAsset $icon $iconRectangle 24 24 0.92 (Join-Path $assetRoot 'Square44x44Logo.targetsize-24_altform-unplated.png')
        Save-PngAsset $icon $iconRectangle 48 48 0.92 (Join-Path $assetRoot 'Square44x44Logo.targetsize-48_altform-lightunplated.png')
        Save-PngAsset $icon $iconRectangle 48 48 0.88 (Join-Path $assetRoot 'LockScreenLogo.scale-200.png')
        Save-PngAsset $icon $iconRectangle 50 50 0.88 (Join-Path $assetRoot 'StoreLogo.png')

        $sizes = @(16, 24, 32, 48, 64, 128, 256)
        $images = foreach ($size in $sizes) { ,(Get-PngBytes $icon $size) }
        $iconPath = Join-Path $assetRoot 'AppIcon.ico'
        $stream = [System.IO.File]::Open($iconPath, [System.IO.FileMode]::Create)
        $writer = [System.IO.BinaryWriter]::new($stream)
        try {
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]$sizes.Count)
            $offset = 6 + (16 * $sizes.Count)
            for ($index = 0; $index -lt $sizes.Count; $index++) {
                $dimension = if ($sizes[$index] -eq 256) { [byte]0 } else { [byte]$sizes[$index] }
                $writer.Write($dimension)
                $writer.Write($dimension)
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([uint16]1)
                $writer.Write([uint16]32)
                $writer.Write([uint32]$images[$index].Length)
                $writer.Write([uint32]$offset)
                $offset += $images[$index].Length
            }
            foreach ($image in $images) {
                $stream.Write([byte[]]$image, 0, $image.Length)
            }
        }
        finally {
            $writer.Dispose()
            $stream.Dispose()
        }
    }
    finally {
        $icon.Dispose()
    }

    $fullRectangle = [System.Drawing.Rectangle]::new(0, 0, $source.Width, $source.Height)
    Save-PngAsset $source $fullRectangle 620 300 0.88 (Join-Path $assetRoot 'Wide310x150Logo.scale-200.png')
    Save-PngAsset $source $fullRectangle 1240 600 0.82 (Join-Path $assetRoot 'SplashScreen.scale-200.png')
}
finally {
    $source.Dispose()
}

Write-Output "Brand assets created from $SourcePath"
