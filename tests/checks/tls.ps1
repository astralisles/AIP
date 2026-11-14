# tls: tlsAccept serves a real TLS handshake; tlsConnect rejects a certificate it cannot trust.
$out = (& $aip build examples\tls | Out-String).Trim()
Report 'tls: builds' ($LASTEXITCODE -eq 0) $out
$exe = Join-Path $root 'examples\tls\build\bin\windows-x64\tls.exe'
$port = 40000 + (Get-Random -Maximum 20000)
$dir = NewTempDir 'aip-tls'
$pfx = Join-Path $dir 'test.pfx'
$cert = New-SelfSignedCertificate -DnsName localhost -CertStoreLocation Cert:\CurrentUser\My -NotAfter (Get-Date).AddDays(1)
$pw = ConvertTo-SecureString 'pw' -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath $pfx -Password $pw | Out-Null
Remove-Item "Cert:\CurrentUser\My\$($cert.Thumbprint)" -Force

function Start-Tls($argList) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo $exe
    $psi.Arguments = $argList; $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
    return [System.Diagnostics.Process]::Start($psi)
}

# AIP server, .NET TLS client that accepts the self-signed certificate.
$srv = Start-Tls "serve $port `"$pfx`" pw"
Start-Sleep -Milliseconds 800
$answer = ''
try {
    $tcp = New-Object System.Net.Sockets.TcpClient('127.0.0.1', $port)
    $ss = New-Object System.Net.Security.SslStream($tcp.GetStream(), $false, ([System.Net.Security.RemoteCertificateValidationCallback]{ $true }))
    $ss.AuthenticateAsClient('localhost')
    $b = [Text.Encoding]::UTF8.GetBytes('ping'); $ss.Write($b, 0, $b.Length); $ss.Flush()
    $buf = New-Object byte[] 100; $n = $ss.Read($buf, 0, 100)
    $answer = [Text.Encoding]::UTF8.GetString($buf, 0, $n)
    $tcp.Close()
} catch { $answer = "error: $_" }
$so = $srv.StandardOutput.ReadToEnd(); $srv.WaitForExit()
Report 'tls: server completes a TLS handshake and echoes encrypted data' ($answer -eq 'secure:ping') $answer
Report 'tls: server received the decrypted bytes' ($so -match 'got 4 bytes') $so

# AIP client against the same self-signed certificate must fail validation.
$port2 = $port + 1
$srv2 = Start-Tls "serve $port2 `"$pfx`" pw"
Start-Sleep -Milliseconds 800
$cli = Start-Tls "client localhost $port2"
$cli.WaitForExit(); $co = $cli.StandardOutput.ReadToEnd(); $ce = $cli.StandardError.ReadToEnd()
try { $srv2.Kill() } catch {}
Report 'tls: client rejects an untrusted certificate' ($cli.ExitCode -ne 0 -and $co -notmatch 'secure') "$co $ce"
Remove-Item $dir -Recurse -Force
