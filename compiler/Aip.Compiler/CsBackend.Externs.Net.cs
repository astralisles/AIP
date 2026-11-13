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
        d["tcpSend"] = "Rt.TcpSend({0}, {1})";
        d["tcpReceive"] = "Rt.TcpReceive({0}, {1})";
        d["tcpClose"] = "Rt.TcpClose({0})";
        d["tcpStop"] = "Rt.TcpClose({0})";
        d["udpOpen"] = "Rt.UdpOpen({0})";
        d["udpSend"] = "Rt.UdpSend({0}, {1}, {2}, {3})";
        d["udpReceive"] = "Rt.UdpReceive({0}, {1})";
        d["udpClose"] = "Rt.TcpClose({0})";
        d["bytesEncode"] = "new System.Text.UTF8Encoding(false).GetBytes({0})";
        d["bytesDecode"] = "new System.Text.UTF8Encoding(false).GetString({0})";
        d["bytesLength"] = "(long)({0}).Length";
        d["bytesGet"] = "(long)({0})[(int)({1})]";
        d["bytesSlice"] = "Rt.BytesSlice({0}, {1}, {2})";
        d["bytesJoin"] = "Rt.BytesJoin({0}, {1})";
        d["bytesFromList"] = "Rt.BytesFromList({0})";
        d["bytesEmpty"] = "System.Array.Empty<byte>()";
    }
}
