# Builds MacLookLike.exe with the .NET Framework C# compiler (no SDK needed).
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

# --- generate app.ico (three traffic lights, PNG-compressed entries) ---
Add-Type -AssemblyName System.Drawing
$ico = Join-Path $root 'app.ico'
$sizes = 16, 32, 48, 256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $bg = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = $s * 0.22; $d = $r * 2
    $bg.AddArc(0, 0, $d, $d, 180, 90); $bg.AddArc($s - $d - 1, 0, $d, $d, 270, 90)
    $bg.AddArc($s - $d - 1, $s - $d - 1, $d, $d, 0, 90); $bg.AddArc(0, $s - $d - 1, $d, $d, 90, 90); $bg.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 58, 58, 62))), $bg)
    $cols = @([System.Drawing.Color]::FromArgb(255, 95, 87), [System.Drawing.Color]::FromArgb(254, 188, 46), [System.Drawing.Color]::FromArgb(40, 200, 64))
    $dot = $s * 0.2
    for ($i = 0; $i -lt 3; $i++) {
        $x = $s * 0.1 + $i * ($s * 0.3)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush $cols[$i]), $x, ($s - $dot) / 2, $dot, $dot)
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , $ms.ToArray()
    $bmp.Dispose()
}
$fs = [System.IO.File]::Create($ico)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $wb = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$wb); $bw.Write([byte]$wb); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close(); $fs.Close()

# --- compile ---
$out = Join-Path $root 'MacLookLike.exe'
& $csc /nologo /target:winexe /optimize+ "/out:$out" "/win32manifest:$root\app.manifest" "/win32icon:$ico" `
    /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "$root\src\*.cs"
if ($LASTEXITCODE -ne 0) { throw "compile failed ($LASTEXITCODE)" }
Write-Host "Built $out"
