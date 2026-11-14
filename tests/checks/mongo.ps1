# mongo: examples/mongo against a MongoDB server. Needs MONGO_URL (an open server, no login); skipped when it is not set.
if (-not $env:MONGO_URL) {
  Write-Host 'SKIP mongo: set MONGO_URL (for example mongodb://127.0.0.1:27017) to run the MongoDB check'
} else {
  $out = (& $aip run examples\mongo | Out-String).Trim() -replace "`r", ''
  $expected = "id lengths: 24 24`nfound: 1`nupdated: 1`nall: 2`ndeleted: 1"
  Report 'mongo: example inserts, finds, updates and deletes' ($LASTEXITCODE -eq 0 -and $out -eq $expected) "got: $out"
}
