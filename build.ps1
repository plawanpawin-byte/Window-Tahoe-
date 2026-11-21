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
    $cols = @([System.Drawing.Color]::FromArgb(255, 10, 132, 255), [System.Drawing.Color]::FromArgb(255, 48, 209, 88), [System.Drawing.Color]::FromArgb(255, 255, 159, 10), [System.Drawing.Color]::FromArgb(255, 191, 90, 242))
    $gap = $s * 0.08; $tile = ($s - $gap * 3) / 2
    for ($i = 0; $i -lt 4; $i++) {
        $x = $gap + ($i % 2) * ($tile + $gap); $y = $gap + [math]::Floor($i / 2) * ($tile + $gap)
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $r = $tile * 0.28; $d = $r * 2
        $p.AddArc($x, $y, $d, $d, 180, 90); $p.AddArc($x + $tile - $d, $y, $d, $d, 270, 90)
        $p.AddArc($x + $tile - $d, $y + $tile - $d, $d, $d, 0, 90); $p.AddArc($x, $y + $tile - $d, $d, $d, 90, 90); $p.CloseFigure()
        $g.FillPath((New-Object System.Drawing.SolidBrush $cols[$i]), $p)
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
$wpf = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'
$refs = @('PresentationFramework','PresentationCore','WindowsBase') | ForEach-Object { "/reference:$wpf\$_.dll" }
$refs += '/reference:System.Xaml.dll','/reference:System.Web.Extensions.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll'
& $csc /nologo /target:winexe /optimize+ "/out:$out" "/win32manifest:$root\app.manifest" "/win32icon:$ico" `
    $refs "$root\src\*.cs"
if ($LASTEXITCODE -ne 0) { throw "compile failed ($LASTEXITCODE)" }
Write-Host "Built $out"
