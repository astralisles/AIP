# Renders text files as numbered PNG frames (one file per step), for an evidence GIF.
# Draws off-screen; no window is opened.
# Usage: powershell -File scripts\render-files-to-frames.ps1 -Files a.fn,b.struct -OutDir <evidence>\frames\<name> [-Title "..."] [-Hold 6]
param(
    [Parameter(Mandatory = $true)][string[]]$Files,
    [Parameter(Mandatory = $true)][string]$OutDir,
    [string]$Title = 'Files',
    [int]$Hold = 6
)

Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $OutDir | Out-Null

$W = 1280; $H = 720
$bg = [System.Drawing.Color]::FromArgb(22, 24, 30)
$panel = [System.Drawing.Color]::FromArgb(34, 37, 46)
$white = [System.Drawing.Color]::FromArgb(240, 240, 240)
$grey = [System.Drawing.Color]::FromArgb(150, 158, 170)
$blue = [System.Drawing.Color]::FromArgb(140, 200, 255)
$sel = [System.Drawing.Color]::FromArgb(60, 90, 140)
$fTitle = New-Object System.Drawing.Font('Segoe UI', 20, [System.Drawing.FontStyle]::Bold)
$fHead = New-Object System.Drawing.Font('Segoe UI', 13, [System.Drawing.FontStyle]::Bold)
$fMono = New-Object System.Drawing.Font('Consolas', 12)
$fList = New-Object System.Drawing.Font('Consolas', 11)
function Brush($c) { New-Object System.Drawing.SolidBrush($c) }

$total = $Files.Count * $Hold
$n = 0
for ($s = 0; $s -lt $Files.Count; $s++) {
    $path = $Files[$s]
    $lines = @(Get-Content -LiteralPath $path)
    for ($k = 0; $k -lt $Hold; $k++) {
        $n++
        $bmp = New-Object System.Drawing.Bitmap($W, $H)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
        $g.Clear($bg)
        $g.DrawString($Title, $fTitle, (Brush $white), 24, 16)

        $g.FillRectangle((Brush $panel), 24, 72, 280, 600)
        $g.DrawString("Files ($($Files.Count))", $fHead, (Brush $white), 34, 80)
        for ($i = 0; $i -lt $Files.Count; $i++) {
            $c = $grey
            if ($i -eq $s) { $g.FillRectangle((Brush $sel), 30, 112 + $i * 24, 268, 22); $c = $white }
            $g.DrawString((Split-Path $Files[$i] -Leaf), $fList, (Brush $c), 36, 114 + $i * 24)
        }

        $g.FillRectangle((Brush $panel), 320, 72, 936, 600)
        $g.DrawString("$(Split-Path $path -Leaf)   ($($lines.Count) lines)", $fHead, (Brush $white), 330, 80)
        for ($i = 0; $i -lt $lines.Count -and $i -lt 24; $i++) {
            $c = $blue
            if ($i -eq 0) { $c = $white }
            $g.DrawString($lines[$i], $fMono, (Brush $c), 334, 118 + $i * 22)
        }

        $g.FillRectangle((Brush $panel), 24, 690, $W - 48, 8)
        $g.FillRectangle((Brush $blue), 24, 690, [int](($W - 48) * $n / $total), 8)
        $g.Dispose()
        $bmp.Save((Join-Path $OutDir ('{0:D3}.png' -f $n)), [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
    }
}
"wrote $n frames to $OutDir"
