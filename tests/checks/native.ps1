# T-0956: native libraries. examples/native is run by the example runner; here: check and the index.
$dir = Join-Path $root 'examples\native'
$out = AipBoth check "`"$dir`""
Report 'native: check passes' ($out -notmatch 'error') $out
$idx = AipOut index "`"$dir`""
Report 'native: index shows the native effect' ($idx -match 'absolute.*native\.ucrtbase') $idx
