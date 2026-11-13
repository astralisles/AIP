# net: console input, TCP and UDP functions, using the built examples\net program.
$out = (& $aip build examples\net | Out-String).Trim()
Report 'net: builds' ($LASTEXITCODE -eq 0) $out
$exe = Join-Path $root 'examples\net\build\bin\windows-x64\net.exe'
$base = 20000 + (Get-Random -Maximum 20000)

function Start-Net($argList, $inputText) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo $exe
    $psi.Arguments = $argList
    $psi.RedirectStandardInput = $true; $psi.RedirectStandardOutput = $true
    $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
    $p = [System.Diagnostics.Process]::Start($psi)
    $p.StandardInput.BaseStream.Close()
    return $p
}

# Console input from a file redirect (a redirected PowerShell stdin may start with a byte order mark).
$inFile = Join-Path ([IO.Path]::GetTempPath()) "aip-net-in-$base.txt"
[IO.File]::WriteAllBytes($inFile, [Text.Encoding]::ASCII.GetBytes("hello console`r`n"))
$o = (& cmd /c "`"$exe`" echo < `"$inFile`"" | Out-String)
Remove-Item $inFile -Force
Report 'net: readLine reads from the console' ($o -match 'you typed: hello console') $o

# TCP: one long connection; the server speaks first, then the client sends twice (text, then raw bytes 0 255 10).
$port = $base
$srv = Start-Net "serve $port" $null
Start-Sleep -Milliseconds 800
$cli = Start-Net "client $port" $null
$co = $cli.StandardOutput.ReadToEnd(); $so = $srv.StandardOutput.ReadToEnd()
$cli.WaitForExit(); $srv.WaitForExit()
Report 'net: tcp server sends first on the open connection' ($co -match 'client: greeting welcome') $co
Report 'net: tcp client sends text and gets the echo' ($co -match 'answer 8 bytes, last byte 101') $co
Report 'net: tcp carries raw bytes (0 255 10) in both directions' ($co -match 'answer 8 bytes, last byte 10') $co
Report 'net: tcp server sees both messages, then the close' ($so -match 'got 3 bytes' -and $so -match 'got 3 bytes' -and $so -match 'client closed') $so

$port = $base + 1
$rcv = Start-Net "udp-receive $port" $null
Start-Sleep -Milliseconds 800
$snd = Start-Net "udp-send $port" $null
$snd.WaitForExit()
$ro = $rcv.StandardOutput.ReadToEnd(); $rcv.WaitForExit()
Report 'net: udp datagram arrives' ($ro -match 'udp got: datagram') $ro