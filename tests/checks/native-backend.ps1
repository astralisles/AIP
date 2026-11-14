# native back end (T-1033): `--backend native` writes a Windows .exe from machine code (no C#); the exe must print what the cs back end prints.
foreach ($name in 'hello', 'control') {
  $out = (& $aip build "examples\$name" --backend native | Out-String).Trim()
  Report "native: $name builds" ($LASTEXITCODE -eq 0 -and $out.Contains('native back end')) $out
  $exe = Join-Path $root "examples\$name\build\bin\native-windows-x64\$name.exe"
  if (Test-Path $exe) {
    $run = (& $exe | Out-String).Trim() -replace "`r", ''
    $want = 'Hello, World!'
    if ($name -ne 'hello') { $want = (Get-Content (Join-Path $root "examples\$name\expected-output.txt") -Raw).Trim() -replace "`r", '' }
    Report "native: $name prints the expected output" ($run -eq $want) "got: $run"
    $bytes = [IO.File]::ReadAllBytes($exe)
    Report "native: $name is a PE file without .NET" ($bytes[0] -eq 0x4D -and $bytes[1] -eq 0x5A -and $bytes.Length -lt 16384) "size $($bytes.Length)"
  } else { Report "native: $name prints the expected output" $false 'no program was built' }
}

$out = (& $aip build examples\shapes --backend native | Out-String).Trim()
Report 'native: an unsupported construct is refused with native-unsupported' ($LASTEXITCODE -ne 0 -and $out.Contains('native-unsupported')) $out

$out = (& $aip build examples\hello --backend native --target linux-x64 | Out-String).Trim()
Report 'native: other targets are refused with unsupported-target' ($LASTEXITCODE -ne 0 -and $out.Contains('unsupported-target')) $out
