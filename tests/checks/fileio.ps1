# fileio: folder, byte file, listing and exit code functions needed by the bootstrap (T-0971).
$tmp = NewTempDir 'aip-fileio-'
$out = (& $aip build examples\fileio | Out-String).Trim()
Report 'fileio: builds' ($LASTEXITCODE -eq 0) $out
Push-Location $tmp
$run = (& (Join-Path $root 'examples\fileio\build\bin\windows-x64\fileio.exe') | Out-String).Trim() -replace "`r", ''
$code = $LASTEXITCODE
Pop-Location
Remove-Item $tmp -Recurse -Force
$want = "exists folder: true`nexists missing: false`nlist: a.txt,b.bin,sub`nbytes: 2 first 65"
Report 'fileio: output' ($run -eq $want) $run
Report 'fileio: processExit sets the exit code' ($code -eq 3) "exit code $code"
