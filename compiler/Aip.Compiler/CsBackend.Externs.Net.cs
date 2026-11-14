namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Console input, TCP, UDP and Bytes externs.</summary>
    static void AddNetExterns(Dictionary<string, string> d)
    {
        d["readLine"] = "(System.Console.In.ReadLine() ?? \"\")";
        d["tcpListen"] = "Rt.TcpListen({0})";
        d["tcpAccept"] = "Rt.TcpAccept({0})";
        d["tcpConnect"] = "Rt.TcpConnect({0}, {1})";
        d["tcpSend"] = "Rt.TcpSend({0}, Rt.ToByteArray({1}))";
        d["tcpReceive"] = "Rt.FromByteArray(Rt.TcpReceive({0}, {1}))";
        d["tcpClose"] = "Rt.TcpClose({0})";
        d["tcpStop"] = "Rt.TcpClose({0})";
        d["tlsConnect"] = "Rt.TlsConnect({0}, {1})";
        d["tlsAccept"] = "Rt.TlsAccept({0}, {1}, {2})";
        d["udpOpen"] = "Rt.UdpOpen({0})";
        d["udpSend"] = "Rt.UdpSend({0}, {1}, {2}, Rt.ToByteArray({3}))";
        d["udpReceive"] = "Rt.FromByteArray(Rt.UdpReceive({0}, {1}))";
        d["udpClose"] = "Rt.TcpClose({0})";
        d["bytesEncode"] = "Rt.FromByteArray(new System.Text.UTF8Encoding(false).GetBytes({0}))";
        d["bytesDecode"] = "new System.Text.UTF8Encoding(false).GetString(Rt.ToByteArray({0}))";
        d["bytesFromList"] = "Rt.BytesFromList({0})";
    }
}
