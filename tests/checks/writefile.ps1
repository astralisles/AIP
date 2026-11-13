# writefile: the built program writes greeting.txt in the current folder.
$tmp = NewTempDir 'aip-writefile-'
$out = (& $aip build examples\writefile | Out-String).Trim()
Report 'writefile: builds' ($LASTEXITCODE -eq 0) $out
Push-Location $tmp
$run = (& (Join-Path $root 'examples\writefile\build\bin\windows-x64\writefile.exe') | Out-String).Trim()
$file = Join-Path $tmp 'greeting.txt'
$content = if (Test-Path $file) { (Get-Content $file -Raw).Trim() } else { '' }
Pop-Location
Remove-Item $tmp -Recurse -Force
Report 'writefile: writes greeting.txt with "Hello from AIP!"' ($content -eq 'Hello from AIP!' -and $run -eq 'Wrote greeting.txt') "file: $content, output: $run"
