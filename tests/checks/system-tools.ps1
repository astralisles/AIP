# system tools: fileExists, fileAppend, dirList, environment variables, processExit code.
$dir = NewTempDir 'aip-system'
try {
  $proj = Join-Path $dir 'p'
  New-Item -ItemType Directory $proj | Out-Null
  $work = Join-Path $dir 'work'
  New-Item -ItemType Directory $work | Out-Null
  $fn = @'
fn main() -> Unit
  "Exercise files, env and exit."
  effects terminal.write, file.read, file.write, process.env, process.exit
  fails
body
  let dir = envGet("AIP_TEST_DIR")
  let path = "{dir}/b.txt"
  let before = fileExists(path)
  printLine("before: {before}")
  fileAppend(path, "one")
  fileAppend(path, "two")
  fileWrite("{dir}/a.txt", "x")
  let content = fileRead(path)
  printLine("content: {content}")
  let after = fileExists(path)
  printLine("after: {after}")
  let sep = ","
  let names = textJoin(dirList(dir), sep)
  printLine("list: {names}")
  processExit(3)
'@
  Set-Content (Join-Path $proj 'main.fn') $fn -Encoding ascii
  $env:AIP_TEST_DIR = $work
  try { $out = AipOut run $proj; $code = $LASTEXITCODE } finally { Remove-Item Env:\AIP_TEST_DIR }
  Report 'system: files, env and processExit' ($code -eq 3 -and $out.Contains('before: false') -and $out.Contains('content: onetwo') -and $out.Contains('after: true') -and $out.Contains('list: a.txt,b.txt')) "code=$code out=$out"
} finally { Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue }
