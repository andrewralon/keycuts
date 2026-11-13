<#
.SYNOPSIS
  Regenerates the app icons in assets/icons: a PNG per size, a 1024 px PNG and a multi-size .ico for each icon.

.DESCRIPTION
  The keycap icons are drawn by tools/icons/app-icon.html (with tools/icons/keycap.js), screenshotted with the Edge or
  Chrome already on the machine on a transparent background, and cut into sizes. The Classic icon is the old Team Ralon
  monogram on a white square: its sizes come from keycuts' original icon (assets/icons/classic/source.ico), so it has no
  1024 px version. classic-transparent is the same monogram without the square.

  Each .ico holds 16, 20, 24, 32, 40, 48, 64, 96 and 256 px: Microsoft's minimum (16, 24, 32, 48, 256) plus the sizes the
  title bar, the taskbar and Explorer ask for at 125, 150 and 200 percent scaling, so Windows only ever scales down from
  a near size. Below 256 the frames are 32-bit bitmaps, which every icon reader handles; 256 is PNG, as Windows expects.

  Nothing is downloaded. Run it after changing the drawing, look at the PNGs, and commit what changed.

.EXAMPLE
  pwsh -File tools/New-AppIcons.ps1
#>
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\assets\icons')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$Sizes = 16, 20, 24, 32, 40, 48, 64, 96, 256
$Big = 1024
$RowY = 1032
$Gap = 8
# The page lays the icons out the same way: 1024 px at (0,0), then each size on one row (see app-icon.html)
$KeycapIcons = 'porcelain', 'midnight', 'cobalt', 'twilight', 'key-white', 'key-charcoal', 'key-blue'

$OutDir = [System.IO.Path]::GetFullPath($OutDir)

$candidates = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Google\Chrome\Application\chrome.exe",
    "${env:ProgramFiles(x86)}\Google\Chrome\Application\chrome.exe"
)
$browser = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
if (-not $browser) { throw 'Neither Edge nor Chrome was found.' }

$page = ([System.Uri](Join-Path $PSScriptRoot 'icons\app-icon.html')).AbsoluteUri

function Get-DibBytes([System.Drawing.Bitmap]$bitmap) {
    # A 32-bit BITMAPINFOHEADER image, bottom-up, followed by the 1-bit AND mask (set where the pixel is fully transparent,
    # for readers that ignore alpha). The height is doubled because an icon DIB counts the mask too.
    $w = $bitmap.Width
    $h = $bitmap.Height
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    $writer.Write([int]40); $writer.Write([int]$w); $writer.Write([int]($h * 2))
    $writer.Write([int16]1); $writer.Write([int16]32)
    $writer.Write([int]0); $writer.Write([int]($w * $h * 4)); $writer.Write([int]0); $writer.Write([int]0); $writer.Write([int]0); $writer.Write([int]0)

    $rect = [System.Drawing.Rectangle]::new(0, 0, $w, $h)
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $pixels = [byte[]]::new($data.Stride * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    }
    finally {
        $bitmap.UnlockBits($data)
    }

    for ($y = $h - 1; $y -ge 0; $y--) {
        $writer.Write($pixels, $y * $data.Stride, $w * 4)
    }

    $maskStride = [int]([math]::Floor(($w + 31) / 32) * 4)
    for ($y = $h - 1; $y -ge 0; $y--) {
        $row = [byte[]]::new($maskStride)
        for ($x = 0; $x -lt $w; $x++) {
            if ($pixels[$y * $data.Stride + $x * 4 + 3] -eq 0) {
                $row[[int][math]::Floor($x / 8)] = $row[[int][math]::Floor($x / 8)] -bor (0x80 -shr ($x % 8))
            }
        }
        $writer.Write($row)
    }

    $writer.Flush()
    return $stream.ToArray()
}

function Get-PngBytes([System.Drawing.Bitmap]$bitmap) {
    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    return $stream.ToArray()
}

function Write-Ico([string]$path, [System.Drawing.Bitmap[]]$bitmaps) {
    $images = foreach ($bitmap in $bitmaps) {
        if ($bitmap.Width -ge 256) { , (Get-PngBytes $bitmap) } else { , (Get-DibBytes $bitmap) }
    }

    $stream = [System.IO.FileStream]::new($path, [System.IO.FileMode]::Create)
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([int16]0); $writer.Write([int16]1); $writer.Write([int16]$bitmaps.Count)
        $offset = 6 + 16 * $bitmaps.Count
        for ($i = 0; $i -lt $bitmaps.Count; $i++) {
            $side = $bitmaps[$i].Width
            # 0 means 256 in the one-byte width and height
            $writer.Write([byte]($side % 256)); $writer.Write([byte]($side % 256))
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([int16]1); $writer.Write([int16]32)
            $writer.Write([int]$images[$i].Length); $writer.Write([int]$offset)
            $offset += $images[$i].Length
        }
        foreach ($image in $images) { $writer.Write([byte[]]$image) }
    }
    finally {
        $writer.Dispose()
    }
}

function Resize-Bitmap([System.Drawing.Bitmap]$source, [int]$side) {
    $bitmap = [System.Drawing.Bitmap]::new($side, $side, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.DrawImage($source, 0, 0, $side, $side)
    }
    finally {
        $graphics.Dispose()
    }
    return $bitmap
}

foreach ($name in $KeycapIcons) {
    $dir = Join-Path $OutDir $name
    New-Item -ItemType Directory -Force -Path $dir | Out-Null

    $shot = Join-Path ([System.IO.Path]::GetTempPath()) "keycuts-icon-$name.png"
    Remove-Item -LiteralPath $shot -ErrorAction SilentlyContinue
    & $browser --headless --disable-gpu --hide-scrollbars --force-device-scale-factor=1 --default-background-color=00000000 `
        "--window-size=$Big,$($RowY + 256)" "--screenshot=$shot" "${page}?icon=$name" 2>&1 | Out-Null
    if (-not (Test-Path -LiteralPath $shot)) { throw "The browser wrote no image for $name." }

    $sheet = [System.Drawing.Bitmap]::new($shot)
    try {
        $format = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
        $large = $sheet.Clone([System.Drawing.Rectangle]::new(0, 0, $Big, $Big), $format)
        $large.Save((Join-Path $dir "$name-$Big.png"), [System.Drawing.Imaging.ImageFormat]::Png)
        $large.Dispose()

        $frames = @()
        $x = 0
        foreach ($side in $Sizes) {
            $frame = $sheet.Clone([System.Drawing.Rectangle]::new($x, $RowY, $side, $side), $format)
            $frame.Save((Join-Path $dir "$name-$side.png"), [System.Drawing.Imaging.ImageFormat]::Png)
            $frames += $frame
            $x += $side + $Gap
        }

        Write-Ico (Join-Path $dir "$name.ico") $frames
        $frames | ForEach-Object { $_.Dispose() }
    }
    finally {
        $sheet.Dispose()
        Remove-Item -LiteralPath $shot -ErrorAction SilentlyContinue
    }

    Write-Host "Wrote $dir"
}

# Classic: keycuts' original icon. Its own frames where it has the size; the others from its 256 px frame. The original is
# an input kept in the repository, so it is read from there whatever -OutDir says.
$classicDir = Join-Path $OutDir 'classic'
New-Item -ItemType Directory -Force -Path $classicDir | Out-Null
$source = Join-Path $PSScriptRoot '..\assets\icons\classic\source.ico'
$bytes = [System.IO.File]::ReadAllBytes($source)
$count = [BitConverter]::ToUInt16($bytes, 4)
$entries = for ($i = 0; $i -lt $count; $i++) {
    $at = 6 + 16 * $i
    [pscustomobject]@{
        Side   = if ($bytes[$at] -eq 0) { 256 } else { [int]$bytes[$at] }
        Bits   = [BitConverter]::ToUInt16($bytes, $at + 6)
        Length = [BitConverter]::ToInt32($bytes, $at + 8)
        Offset = [BitConverter]::ToInt32($bytes, $at + 12)
    }
}
$largest = $entries | Where-Object { $_.Side -eq 256 -and $_.Bits -eq 32 } | Select-Object -First 1
$pngStream = [System.IO.MemoryStream]::new($bytes, $largest.Offset, $largest.Length)
# Copied, so the stream can go; a Bitmap made from a stream needs it for as long as the Bitmap lives
$fromStream = [System.Drawing.Bitmap]::new($pngStream)
$classic256 = [System.Drawing.Bitmap]::new($fromStream)
$fromStream.Dispose()
$pngStream.Dispose()

$frames = foreach ($side in $Sizes) {
    $own = $entries | Where-Object { $_.Side -eq $side -and $_.Bits -eq 32 -and $side -lt 256 } | Select-Object -First 1
    if ($own) {
        $icon = [System.Drawing.Icon]::new($source, $side, $side)
        $bitmap = $icon.ToBitmap()
        $icon.Dispose()
        if ($bitmap.Width -ne $side) { throw "Classic: asked for $side px, got $($bitmap.Width)." }
        $bitmap
    }
    elseif ($side -eq 256) {
        [System.Drawing.Bitmap]::new($classic256)
    }
    else {
        Resize-Bitmap $classic256 $side
    }
}
foreach ($frame in $frames) {
    $frame.Save((Join-Path $classicDir "classic-$($frame.Width).png"), [System.Drawing.Imaging.ImageFormat]::Png)
}
Write-Ico (Join-Path $classicDir 'classic.ico') $frames
Write-Host "Wrote $classicDir"

# The original is a black monogram on an opaque white square. The transparent version takes each pixel's darkness as its
# opacity and paints it the monogram's black, which separates a one-colour drawing exactly, antialiased edges included.
function ConvertTo-Transparent([System.Drawing.Bitmap]$source) {
    $w = $source.Width
    $h = $source.Height
    $format = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    $bitmap = $source.Clone([System.Drawing.Rectangle]::new(0, 0, $w, $h), $format)
    $rect = [System.Drawing.Rectangle]::new(0, 0, $w, $h)
    $data = $bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, $format)
    try {
        $pixels = [byte[]]::new($data.Stride * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
        for ($i = 0; $i -lt $pixels.Length; $i += 4) {
            # BGRA; the lightest channel decides. The 256 px frame's white is a few steps off pure white, so anything from
            # 240 up is fully transparent and the range down to the monogram's black (about 24) is stretched to full opacity.
            $light = [math]::Max([math]::Max($pixels[$i], $pixels[$i + 1]), $pixels[$i + 2])
            $alpha = [math]::Round((240 - $light) * 255 / (240 - 24))
            $pixels[$i] = 23; $pixels[$i + 1] = 23; $pixels[$i + 2] = 24
            $pixels[$i + 3] = [byte][math]::Min(255, [math]::Max(0, $alpha))
        }
        [System.Runtime.InteropServices.Marshal]::Copy($pixels, 0, $data.Scan0, $pixels.Length)
    }
    finally {
        $bitmap.UnlockBits($data)
    }
    return $bitmap
}

$transparentDir = Join-Path $OutDir 'classic-transparent'
New-Item -ItemType Directory -Force -Path $transparentDir | Out-Null
$transparent = foreach ($frame in $frames) { ConvertTo-Transparent $frame }
foreach ($frame in $transparent) {
    $frame.Save((Join-Path $transparentDir "classic-transparent-$($frame.Width).png"), [System.Drawing.Imaging.ImageFormat]::Png)
}
Write-Ico (Join-Path $transparentDir 'classic-transparent.ico') $transparent
$transparent | ForEach-Object { $_.Dispose() }
$frames | ForEach-Object { $_.Dispose() }
$classic256.Dispose()
Write-Host "Wrote $transparentDir"
