# packages: every compiler\packages\<name>\package.txt names exactly the functions its .fn files declare, and has a runtime file.
foreach ($dir in Get-ChildItem (Join-Path $root 'compiler\packages') -Directory) {
  $manifest = Get-Content (Join-Path $dir.FullName 'package.txt')
  $csManifest = Get-Content (Join-Path $dir.FullName 'cs\cs.txt')
  $ext = @($csManifest | Where-Object { $_ -match '^extern (\w+) ' } | ForEach-Object { ($_ -split ' ')[1] } | Sort-Object)
  $fns = @(Get-ChildItem $dir.FullName -Filter *.fn | ForEach-Object { $_.BaseName } | Sort-Object)
  $ok = (($ext -join ',') -eq ($fns -join ',')) -and (Test-Path (Join-Path $dir.FullName 'cs\Rt.cs')) -and ($manifest -contains "name $($dir.Name)")
  Report "packages: $($dir.Name) manifest matches its functions" $ok "externs: $($ext -join ',') files: $($fns -join ',')"
}
