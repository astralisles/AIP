# std layout (T-0942): grouped one folder deep; the compiler rejects a misplaced file.
$out = (& $aip std | Out-String)
$groups = @($out -split "`r?`n" | Where-Object { $_ -match '^\w+/$' })
$srcCount = @(Get-ChildItem compiler\std -Recurse -File -Filter *.fn).Count
$fnCount = @($out -split "`r?`n" | Where-Object { $_ -match '^  \w+\(' }).Count
Report 'std: `aip std` lists every std file under its group' ($LASTEXITCODE -eq 0 -and $groups.Count -ge 9 -and $fnCount -eq $srcCount -and @(Get-ChildItem compiler\std -File -Filter *.fn).Count -eq 0) "groups: $($groups -join ' '), listed $fnCount of $srcCount`n$out"
$bin = Join-Path $root 'compiler\Aip.Compiler\bin\Release\net10.0'
$copy = NewTempDir 'aip-stdlayout-'
Remove-Item $copy -Force
Copy-Item $bin $copy -Recurse
Move-Item (Join-Path $copy 'std\text\textLength.fn') (Join-Path $copy 'std\textLength.fn')
Move-Item (Join-Path $copy 'std\list\listGet.fn') (Join-Path $copy 'std\map\listGet.fn')
$out = (& (Join-Path $copy 'aip.exe') check examples\hello | Out-String).Trim()
$code = $LASTEXITCODE
Remove-Item -Recurse -Force $copy
Report 'std: misplaced std files are rejected (std-layout)' ($code -ne 0 -and $out.Contains('std-layout') -and $out.Contains('`textLength` must be in a group folder') -and $out.Contains('`listGet` in std/map/ must start with `map`')) $out
