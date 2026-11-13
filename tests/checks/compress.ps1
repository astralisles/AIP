# compress: reads a line, run-length compresses it, prints byte values, decompresses, prints again.
$b = (& $aip build examples\compress | Out-String).Trim()
Report 'compress: builds' ($LASTEXITCODE -eq 0) $b
$exe = Join-Path $root 'examples\compress\build\bin\windows-x64\compress.exe'
$tmp = NewTempDir 'aip-compress-'
function RunWith($text) {
  $in = Join-Path $tmp 'in.txt'; $o = Join-Path $tmp 'out.txt'
  [IO.File]::WriteAllText($in, "$text`n")
  Start-Process $exe -NoNewWindow -Wait -RedirectStandardInput $in -RedirectStandardOutput $o
  Get-Content $o -Raw
}
$out = RunWith 'aaabccccd'
Report 'compress: round trip of "aaabccccd"' ($out.Contains('Compressed (8 bytes): 3 97 1 98 4 99 1 100') -and $out.Contains('Decompressed (9 bytes): 97 97 97 98 99 99 99 99 100') -and $out.Contains('Text: aaabccccd')) $out
$out = RunWith ('a' * 300)
Report 'compress: runs longer than 255 are split and restored' ($out.Contains('Compressed (4 bytes): 255 97 45 97') -and $out.Contains('Decompressed (300 bytes)')) $out
Remove-Item $tmp -Recurse -Force
