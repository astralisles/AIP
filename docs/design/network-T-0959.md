# Console, TCP, UDP and bytes (T-0959)

- `Bytes` is a built-in immutable byte array (C# `byte[]`). Pure helpers in `std/bytes/`: `bytesFromText`/`bytesToText` (UTF-8),
  `bytesLength`, `bytesGet` (0..255), `bytesSlice`, `bytesJoin`, `bytesFromList`, `bytesEmpty`. Network code only sees bytes, never text.
- Handle types `TcpListener`, `TcpConn`, `UdpSocket` are built in. Only effectful std functions create them
  (`tcp.listen`, `tcp.connect`, `udp.open`), so holding one is proof that effect was declared somewhere in the call chain.
  Handles are not comparable or printable; they are erased to `object` in C#.
- TCP (long running, either side may speak at any time, any number of times): `tcpListen`, `tcpAccept`, `tcpConnect`,
  `tcpSend`, `tcpReceive(conn, max)` (blocks; 1..max bytes; empty = peer closed), `tcpClose`, `tcpStop`.
  TCP is a stream: chunk boundaries are not message boundaries, so programs frame their own messages.
- UDP: `udpOpen(port)` (0 = any), `udpSend`, `udpReceive(socket, timeoutMs)` (empty on timeout), `udpClose`.
  The sender's address is not returned yet.
- Console: `readLine()` (`terminal.read`), empty text at end of input.
- Handles are mutable resources outside the program, which is why every operation carries an effect. aip test refuses them.
- Example `examples/net`, test `tests/checks/net.ps1`. Not done: send-to-sender for UDP, receive timeouts for TCP, TLS, `Bytes` in `==`.