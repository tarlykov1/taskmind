param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,
    [string]$ThemePath = ""
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ThemePath)) {
    $ThemePath = Join-Path (Split-Path -Parent $PSScriptRoot) 'src/GSPTaskMiningAgent/Assets/icon-theme.json'
}

$theme = Get-Content -LiteralPath $ThemePath -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

function ConvertTo-Rgba([string]$hex) {
    $value = $hex.TrimStart('#')
    return @(
        [Convert]::ToByte($value.Substring(0, 2), 16),
        [Convert]::ToByte($value.Substring(2, 2), 16),
        [Convert]::ToByte($value.Substring(4, 2), 16),
        [byte]255)
}

function Get-LineDistance([double]$px, [double]$py, [double]$x1, [double]$y1, [double]$x2, [double]$y2) {
    $dx = $x2 - $x1
    $dy = $y2 - $y1
    if ($dx -eq 0 -and $dy -eq 0) { return [Math]::Sqrt(($px - $x1) * ($px - $x1) + ($py - $y1) * ($py - $y1)) }
    $t = (($px - $x1) * $dx + ($py - $y1) * $dy) / ($dx * $dx + $dy * $dy)
    $t = [Math]::Max(0, [Math]::Min(1, $t))
    $cx = $x1 + $t * $dx
    $cy = $y1 + $t * $dy
    return [Math]::Sqrt(($px - $cx) * ($px - $cx) + ($py - $cy) * ($py - $cy))
}

function Set-Pixel([byte[]]$pixels, [int]$size, [int]$x, [int]$y, [byte[]]$rgba) {
    $index = ($y * $size + $x) * 4
    $pixels[$index] = $rgba[0]
    $pixels[$index + 1] = $rgba[1]
    $pixels[$index + 2] = $rgba[2]
    $pixels[$index + 3] = $rgba[3]
}

function New-IconPixels([int]$size, [byte[]]$background, [byte[]]$foreground, [byte[]]$status) {
    $pixels = New-Object byte[] ($size * $size * 4)
    $radius = $size / 7.0
    $lineWidth = [Math]::Max(1, $size / 12.0)
    $nodes = @(@($size * 0.28, $size * 0.35), @($size * 0.50, $size * 0.58), @($size * 0.72, $size * 0.35))
    $nodeRadius = [Math]::Max(1.2, $size / 9.0)
    $statusRadius = [Math]::Max(2.2, $size / 5.0)
    $statusCenter = @($size - $statusRadius * 0.8, $size - $statusRadius * 0.8)

    for ($y = 0; $y -lt $size; $y++) {
        for ($x = 0; $x -lt $size; $x++) {
            $px = $x + 0.5
            $py = $y + 0.5
            $left = 1.0; $top = 1.0; $right = $size - 2.0; $bottom = $size - 2.0
            $cx = [Math]::Min([Math]::Max($px, $left + $radius), $right - $radius)
            $cy = [Math]::Min([Math]::Max($py, $top + $radius), $bottom - $radius)
            if ([Math]::Sqrt(($px - $cx) * ($px - $cx) + ($py - $cy) * ($py - $cy)) -le $radius) {
                Set-Pixel $pixels $size $x $y $background
            }

            if ((Get-LineDistance $px $py $nodes[0][0] $nodes[0][1] $nodes[1][0] $nodes[1][1]) -le $lineWidth -or
                (Get-LineDistance $px $py $nodes[1][0] $nodes[1][1] $nodes[2][0] $nodes[2][1]) -le $lineWidth) {
                Set-Pixel $pixels $size $x $y $foreground
            }

            foreach ($node in $nodes) {
                if ([Math]::Sqrt(($px - $node[0]) * ($px - $node[0]) + ($py - $node[1]) * ($py - $node[1])) -le $nodeRadius) {
                    Set-Pixel $pixels $size $x $y $foreground
                }
            }

            $statusDistance = [Math]::Sqrt(($px - $statusCenter[0]) * ($px - $statusCenter[0]) + ($py - $statusCenter[1]) * ($py - $statusCenter[1]))
            if ($statusDistance -le ($statusRadius / 2.0 + [Math]::Max(1, $size / 24.0))) { Set-Pixel $pixels $size $x $y $foreground }
            if ($statusDistance -le ($statusRadius / 2.0)) { Set-Pixel $pixels $size $x $y $status }
        }
    }
    return $pixels
}

function Write-UInt16([System.IO.BinaryWriter]$writer, [int]$value) { $writer.Write([uint16]$value) }
function Write-UInt32([System.IO.BinaryWriter]$writer, [long]$value) { $writer.Write([uint32]$value) }

function New-Dib([int]$size, [byte[]]$pixels) {
    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    Write-UInt32 $writer 40
    Write-UInt32 $writer $size
    Write-UInt32 $writer ($size * 2)
    Write-UInt16 $writer 1
    Write-UInt16 $writer 32
    Write-UInt32 $writer 0
    Write-UInt32 $writer ($size * $size * 4)
    Write-UInt32 $writer 0
    Write-UInt32 $writer 0
    Write-UInt32 $writer 0
    Write-UInt32 $writer 0

    for ($y = $size - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $size; $x++) {
            $index = ($y * $size + $x) * 4
            $writer.Write([byte]$pixels[$index + 2])
            $writer.Write([byte]$pixels[$index + 1])
            $writer.Write([byte]$pixels[$index])
            $writer.Write([byte]$pixels[$index + 3])
        }
    }

    $maskRow = [int]([Math]::Floor(($size + 31) / 32) * 4)
    $writer.Write((New-Object byte[] ($maskRow * $size)))
    $writer.Flush()
    return $stream.ToArray()
}

function Write-Ico([string]$path, [string]$statusHex) {
    $background = ConvertTo-Rgba $theme.background
    $foreground = ConvertTo-Rgba $theme.foreground
    $status = ConvertTo-Rgba $statusHex
    $images = @()
    foreach ($sizeValue in $theme.sizes) {
        $size = [int]$sizeValue
        $images += ,(New-Dib $size (New-IconPixels $size $background $foreground $status))
    }

    $stream = [System.IO.File]::Open($path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
    try {
        $writer = [System.IO.BinaryWriter]::new($stream)
        Write-UInt16 $writer 0
        Write-UInt16 $writer 1
        Write-UInt16 $writer $images.Count
        $offset = 6 + 16 * $images.Count
        for ($i = 0; $i -lt $images.Count; $i++) {
            $size = [int]$theme.sizes[$i]
            $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
            $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            Write-UInt16 $writer 1
            Write-UInt16 $writer 32
            Write-UInt32 $writer $images[$i].Length
            Write-UInt32 $writer $offset
            $offset += $images[$i].Length
        }
        foreach ($image in $images) { $writer.Write($image) }
    }
    finally {
        $stream.Dispose()
    }
}

foreach ($status in $theme.statuses.PSObject.Properties) {
    Write-Ico (Join-Path $OutputDirectory $status.Name) $status.Value
}
