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
    if ($inputText) { $p.StandardInput.WriteLine($inputText) }
    $p.StandardInput.Close()
    return $p
}

$p = Start-Net 'echo' 'hello console'
$o = $p.StandardOutput.ReadToEnd(); $p.WaitForExit()
Report 'net: readLine reads from the console' ($o -match 'typed=\[hello console\]') $o

$port = $base
$srv = Start-Net "serve $port" $null
Start-Sleep -Milliseconds 800
$cli = Start-Net "request $port" $null
$co = $cli.StandardOutput.ReadToEnd(); $so = $srv.StandardOutput.ReadToEnd()
$cli.WaitForExit(); $srv.WaitForExit()
Report 'net: tcp request gets the reply' ($co -match 'answer=\[pong\]') $co
Report 'net: tcp server gets the line' ($so -match 'served=\[ping\]') $so

$port = $base + 1
$rcv = Start-Net "udp-receive $port" $null
Start-Sleep -Milliseconds 800
$snd = Start-Net "udp-send $port" $null
$snd.WaitForExit()
$ro = $rcv.StandardOutput.ReadToEnd(); $rcv.WaitForExit()
Report 'net: udp datagram arrives' ($ro -match 'datagram=\[datagram\]') $ro
