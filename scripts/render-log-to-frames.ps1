# Renders a captured text log as PNG frames that reveal it line by line (terminal look).
# The frames are drawn from the log file; this is not a screen recording.
# Usage: scripts\render-log-to-frames.ps1 -Log log.txt -OutDir frames\name [-Title text] [-Rows 30] [-EndHold 12]
param([Parameter(Mandatory)][string]$Log, [Parameter(Mandatory)][string]$OutDir,
      [string]$Title = '', [int]$Rows = 30, [int]$EndHold = 12)
Add-Type -AssemblyName System.Drawing
$lines = [IO.File]::ReadAllLines($Log)
New-Item -ItemType Directory -Force $OutDir | Out-Null
Get-ChildItem $OutDir -Filter *.png | Remove-Item -Confirm:$false
$w = 1280; $h = 720
$font = New-Object System.Drawing.Font('Consolas', 13)
$small = New-Object System.Drawing.Font('Segoe UI', 11)
$bg = [System.Drawing.Color]::FromArgb(18, 18, 24)
$cmd = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(120, 200, 255))
$txt = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(220, 220, 220))
$err = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 130, 120))
$ok = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(130, 230, 140))
$dim = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 150, 160))
$n = 0
function Frame($count) {
  $bmp = New-Object System.Drawing.Bitmap $w, $h
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.TextRenderingHint = 'ClearTypeGridFit'
  $g.Clear($bg)
  $g.DrawString("$Title  (rendered from the captured log, not a screen recording)", $small, $dim, 16, 10)
  $first = [Math]::Max(0, $count - $Rows)
  for ($i = $first; $i -lt $count; $i++) {
    $l = $lines[$i]
    if ($l.Length -gt 112) { $l = $l.Substring(0, 109) + '...' }
    $b = $txt
    if ($l.StartsWith('> ')) { $b = $cmd }
    elseif ($l -match 'error|FAIL|exit code') { $b = $err }
    elseif ($l -match '^(ok:|PASS|ALL PASSED|Hello)') { $b = $ok }
    $g.DrawString($l, $font, $b, 16, 44 + ($i - $first) * 22)
  }
  $g.Dispose()
  $script:n++
  $bmp.Save((Join-Path $OutDir ('{0:D4}.png' -f $script:n)), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}
for ($c = 1; $c -le $lines.Count; $c++) {
  if ($lines[$c - 1] -eq '' -and $c -lt $lines.Count) { continue }
  Frame $c
  if ($lines[$c - 1].StartsWith('> ')) { Frame $c }
}
for ($k = 0; $k -lt $EndHold; $k++) { Frame $lines.Count }
"$n frames -> $OutDir"
