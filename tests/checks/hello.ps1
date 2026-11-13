# hello: build, run the built program, `aip run`, and refuse an unknown target.
$out = (& $aip build examples\hello | Out-String).Trim()
Report 'hello: builds' ($LASTEXITCODE -eq 0) $out
$exe = Join-Path $root 'examples\hello\build\bin\windows-x64\hello.exe'
if (Test-Path $exe) {
  $run = (& $exe | Out-String).Trim()
  Report 'hello: prints "Hello, World!"' ($run -eq 'Hello, World!') "got: $run"
} else { Report 'hello: prints "Hello, World!"' $false 'no program was built' }

$out = (& $aip run examples\hello | Out-String).Trim()
Report 'hello: `aip run` prints only "Hello, World!"' ($LASTEXITCODE -eq 0 -and $out -eq 'Hello, World!') "got: $out"

$out = (& $aip build examples\hello --target wasm | Out-String).Trim()
Report 'hello: wasm is refused with a clear message' ($LASTEXITCODE -ne 0 -and $out.Contains('unsupported-target')) $out
