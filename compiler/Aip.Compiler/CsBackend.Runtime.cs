namespace Aip;

sealed partial class CsBackend
{
    /// <summary>The generated `Rt` runtime class, one file per topic. Add a new part with its own const and one line here.</summary>
    static readonly (string File, string Source)[] RuntimeParts =
    {
        ("Rt.Core.cs", RtCore),
        ("Rt.Map.cs", RtMap),
        ("Rt.Net.cs", RtNet),
        ("Rt.Profile.cs", RtProfile),
        ("Rt.Queue.cs", RtQueue),
        ("Rt.Sorted.cs", RtSorted),
    };

    const string RtCore = """
        public static partial class Rt
        {
            public static string[] Args = System.Array.Empty<string>();
            public static string Slice(string s, long start, long end)
            {
                int a = (int)System.Math.Clamp(start, 0, s.Length), b = (int)System.Math.Clamp(end, a, s.Length);
                return s.Substring(a, b - a);
            }
            public static System.Collections.Generic.List<object> Append(System.Collections.Generic.List<object> l, object item)
            {
                var r = new System.Collections.Generic.List<object>(l.Count + 1);
                r.AddRange(l);
                r.Add(item);
                return r;
            }
            public static System.Collections.Generic.List<object> ListSet(System.Collections.Generic.List<object> l, long i, object item)
            {
                var r = new System.Collections.Generic.List<object>(l);
                r[(int)i] = item;
                return r;
            }
            public static System.Collections.Generic.List<object> ListInsert(System.Collections.Generic.List<object> l, long i, object item)
            {
                var r = new System.Collections.Generic.List<object>(l);
                r.Insert((int)i, item);
                return r;
            }
            public static System.Collections.Generic.List<object> ListRemoveAt(System.Collections.Generic.List<object> l, long i)
            {
                var r = new System.Collections.Generic.List<object>(l);
                r.RemoveAt((int)i);
                return r;
            }
            public static System.Collections.Generic.List<object> ListConcat(System.Collections.Generic.List<object> a, System.Collections.Generic.List<object> b)
            {
                var r = new System.Collections.Generic.List<object>(a.Count + b.Count);
                r.AddRange(a);
                r.AddRange(b);
                return r;
            }
            public static System.Collections.Generic.List<object> ListSlice(System.Collections.Generic.List<object> l, long start, long end)
            {
                int a = (int)System.Math.Clamp(start, 0, l.Count), b = (int)System.Math.Clamp(end, a, l.Count);
                return l.GetRange(a, b - a);
            }
            static byte[] ToBytes(System.Collections.Generic.List<object> l)
            {
                var r = new byte[l.Count];
                for (int i = 0; i < r.Length; i++)
                {
                    long v = (long)l[i];
                    if (v < 0 || v > 255) throw new System.InvalidOperationException("byte out of range 0..255: " + v);
                    r[i] = (byte)v;
                }
                return r;
            }
            static System.Collections.Generic.List<object> FromBytes(byte[] b)
            {
                var r = new System.Collections.Generic.List<object>();
                foreach (var x in b) r.Add((long)x);
                return r;
            }
            public static System.Collections.Generic.List<object> HashSha256(System.Collections.Generic.List<object> d) => FromBytes(System.Security.Cryptography.SHA256.HashData(ToBytes(d)));
            public static System.Collections.Generic.List<object> HashHmacSha256(System.Collections.Generic.List<object> k, System.Collections.Generic.List<object> d) => FromBytes(System.Security.Cryptography.HMACSHA256.HashData(ToBytes(k), ToBytes(d)));
            public static System.Collections.Generic.List<object> BytesFromText(string t)
            {
                var r = new System.Collections.Generic.List<object>();
                foreach (var x in System.Text.Encoding.UTF8.GetBytes(t)) r.Add((long)x);
                return r;
            }
            public static string BytesToText(System.Collections.Generic.List<object> l) => System.Text.Encoding.UTF8.GetString(ToBytes(l));
            public static string BytesToHex(System.Collections.Generic.List<object> l) => System.Convert.ToHexString(ToBytes(l)).ToLowerInvariant();
            public static System.Collections.Generic.List<object> BytesFromHex(string h)
            {
                if (h.Length % 2 != 0) throw new System.InvalidOperationException("hex text must have an even number of digits");
                var r = new System.Collections.Generic.List<object>();
                foreach (var x in System.Convert.FromHexString(h)) r.Add((long)x);
                return r;
            }
            public static System.Collections.Generic.List<object> ListReverse(System.Collections.Generic.List<object> l)
            {
                var r = new System.Collections.Generic.List<object>(l);
                r.Reverse();
                return r;
            }
            public static System.Collections.Generic.List<object> ListRange(long start, long end)
            {
                var r = new System.Collections.Generic.List<object>();
                for (long i = start; i < end; i++) r.Add(i);
                return r;
            }
            public static void Nop() { }
            public static void Write(string path, string content) => System.IO.File.WriteAllText(path, content);
            public static string Text(string v) => v;
            public static string Text(double v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            public static string Text(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            public static string Text(int v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            public static string Text(uint v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            public static int Shift(long count, int width) => count >= 0 && count < width ? (int)count : throw new System.ArgumentOutOfRangeException("count", $"shift count {count} must be 0 to {width - 1}");
            public static long ToInt(double v) => double.IsNaN(v) || v >= 9223372036854775808.0 || v < -9223372036854775808.0 ? throw new System.OverflowException($"{v} does not fit in Int") : (long)v;
            public static string Text(ulong v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            public static string Text(long v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            public static string Text(bool v) => v ? "true" : "false";
        }
        """;

    const string RtMap = """
        public static partial class Rt
        {
            public static object MapGet(System.Collections.Generic.Dictionary<object, object> m, object k) =>
                m.TryGetValue(k, out var v) ? v : throw new System.Collections.Generic.KeyNotFoundException("mapGet: key not in the map");
            public static System.Collections.Generic.Dictionary<object, object> MapSet(System.Collections.Generic.Dictionary<object, object> m, object k, object v)
            {
                var r = new System.Collections.Generic.Dictionary<object, object>(m);
                r[k] = v;
                return r;
            }
            public static System.Collections.Generic.Dictionary<object, object> MapRemove(System.Collections.Generic.Dictionary<object, object> m, object k)
            {
                var r = new System.Collections.Generic.Dictionary<object, object>();
                foreach (var p in m) if (!p.Key.Equals(k)) r.Add(p.Key, p.Value);
                return r;
            }
        }
        """;

    const string RtSorted = """
        public static partial class Rt
        {
            // Sorted maps and sets are balanced trees: keys come back in ascending order (Int by value, Text by ordinal).
            sealed class OrderCmp : System.Collections.Generic.IComparer<object>
            {
                public int Compare(object? a, object? b) => a is string s ? string.CompareOrdinal(s, (string)b!) : ((long)a!).CompareTo((long)b!);
            }
            public static readonly System.Collections.Generic.IComparer<object> Order = new OrderCmp();
            public static object SortedGet(System.Collections.Generic.SortedDictionary<object, object> m, object k) =>
                m.TryGetValue(k, out var v) ? v : throw new System.Collections.Generic.KeyNotFoundException("sortedMapGet: key not in the map");
            public static System.Collections.Generic.SortedDictionary<object, object> SortedPut(System.Collections.Generic.SortedDictionary<object, object> m, object k, object v)
            {
                var r = new System.Collections.Generic.SortedDictionary<object, object>(m, Order);
                r[k] = v;
                return r;
            }
            public static System.Collections.Generic.SortedDictionary<object, object> SortedRemove(System.Collections.Generic.SortedDictionary<object, object> m, object k)
            {
                var r = new System.Collections.Generic.SortedDictionary<object, object>(m, Order);
                r.Remove(k);
                return r;
            }
        }
        """;

    const string RtQueue = """
        public static partial class Rt
        {
            // Queue is a List (front = index 0). PriorityQueue is a List of object[] { priority, item } kept in
            // ascending priority, and equal priorities keep insertion order.
            public static object Front(System.Collections.Generic.List<object> l, string fn) =>
                l.Count > 0 ? l[0] : throw new System.Collections.Generic.KeyNotFoundException(fn + ": the queue is empty");
            public static System.Collections.Generic.List<object> Pop(System.Collections.Generic.List<object> l, string fn)
            {
                if (l.Count == 0) throw new System.Collections.Generic.KeyNotFoundException(fn + ": the queue is empty");
                return l.GetRange(1, l.Count - 1);
            }
            public static System.Collections.Generic.List<object> PqPush(System.Collections.Generic.List<object> l, object item, long priority)
            {
                int i = l.Count;
                while (i > 0 && (long)((object[])l[i - 1])[0] > priority) i--;
                var r = new System.Collections.Generic.List<object>(l);
                r.Insert(i, new object[] { priority, item });
                return r;
            }
        }
        """;

    const string RtProfile = """
        public static partial class Rt
        {
            // ---- opt-in profiling and call log (one program thread; parallel tasks (T-0935) will need per-thread state) ----
            public static bool Prof, Calls;
            public static string? Error;
            static string? profPath, callsPath;
            static long startTs, depth, maxDepth, callLimit, callCount;
            static System.Text.StringBuilder callLog = new();
            sealed class Stat { public long Calls, Self; }
            static readonly System.Collections.Generic.Dictionary<string, Stat> stats = new();
            static readonly System.Collections.Generic.List<long> childTime = new();
            static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;
            public static void Init()
            {
                profPath = System.Environment.GetEnvironmentVariable("AIP_PROFILE");
                callsPath = System.Environment.GetEnvironmentVariable("AIP_CALLS");
                Prof = !string.IsNullOrEmpty(profPath);
                Calls = !string.IsNullOrEmpty(callsPath);
                callLimit = long.TryParse(System.Environment.GetEnvironmentVariable("AIP_CALLS_MAX"), out var n) ? n : 10000;
                startTs = System.Diagnostics.Stopwatch.GetTimestamp();
            }
            public static long Enter()
            {
                depth++;
                if (depth > maxDepth) maxDepth = depth;
                childTime.Add(0);
                return System.Diagnostics.Stopwatch.GetTimestamp();
            }
            public static void Leave(string name, long t0)
            {
                long total = System.Diagnostics.Stopwatch.GetTimestamp() - t0;
                long kids = childTime[^1];
                childTime.RemoveAt(childTime.Count - 1);
                if (childTime.Count > 0) childTime[^1] += total;
                if (!stats.TryGetValue(name, out var s)) stats[name] = s = new Stat();
                s.Calls++;
                s.Self += total - kids;
                depth--;
            }
            public static void Call(string name, object[] a)
            {
                callCount++;
                if (callCount > callLimit) return;
                callLog.Append("{\"n\":").Append(callCount).Append(",\"depth\":").Append(depth).Append(",\"fn\":\"").Append(name).Append("\",\"args\":[");
                for (int i = 0; i < a.Length; i++)
                {
                    var v = a[i] is string str ? str : System.Convert.ToString(a[i], Inv) ?? "null";
                    if (v.Length > 120) v = v.Substring(0, 120) + "...";
                    callLog.Append(i > 0 ? "," : "").Append(Json(v));
                }
                callLog.Append("]}\n");
            }
            static string Json(string v)
            {
                var sb = new System.Text.StringBuilder("\"");
                foreach (var c in v)
                    sb.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", _ when c < ' ' => "\\u" + ((int)c).ToString("X4"), _ => c.ToString() });
                return sb.Append('"').ToString();
            }
            static void Out(string? path, string text)
            {
                if (path == "-") System.Console.Error.Write(text);
                else if (path != null) System.IO.File.WriteAllText(path, text);
            }
            public static void Report(int code)
            {
                if (Calls)
                {
                    if (callCount > callLimit) callLog.Append("{\"truncated\":true,\"calls\":").Append(callCount).Append(",\"logged\":").Append(callLimit).Append("}\n");
                    Out(callsPath, callLog.ToString());
                }
                if (!Prof) return;
                string ms(long ts) => (ts * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("0.###", Inv);
                var proc = System.Diagnostics.Process.GetCurrentProcess();
                var sb = new System.Text.StringBuilder("{\n");
                sb.Append($"  \"schema\": 1,\n  \"exitCode\": {code},\n  \"error\": {(Error == null ? "null" : Json(Error))},\n");
                sb.Append($"  \"wallMs\": {ms(System.Diagnostics.Stopwatch.GetTimestamp() - startTs)},\n");
                sb.Append($"  \"cpuMs\": {proc.TotalProcessorTime.TotalMilliseconds.ToString("0.###", Inv)},\n");
                sb.Append($"  \"peakWorkingSetBytes\": {proc.PeakWorkingSet64},\n  \"allocatedBytes\": {System.GC.GetTotalAllocatedBytes()},\n");
                sb.Append($"  \"gcCollections\": [{System.GC.CollectionCount(0)}, {System.GC.CollectionCount(1)}, {System.GC.CollectionCount(2)}],\n");
                sb.Append($"  \"maxCallDepth\": {maxDepth},\n  \"functions\": [\n");
                sb.Append(string.Join(",\n", System.Linq.Enumerable.Select(System.Linq.Enumerable.OrderByDescending(stats, p => p.Value.Self),
                    p => $"    {{\"fn\": \"{p.Key}\", \"calls\": {p.Value.Calls}, \"selfMs\": {ms(p.Value.Self)}}}")));
                sb.Append("\n  ]\n}\n");
                Out(profPath, sb.ToString());
            }
        }
        """;

    const string RtNet = """
        public static partial class Rt
        {
        // Network handles (TcpListener, TcpConn, UdpSocket) are erased to object; the effects say who may open them.
        public static object TcpListen(long port) { var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, (int)port); l.Start(); return l; }
        public static object TcpAccept(object l) { var c = ((System.Net.Sockets.TcpListener)l).AcceptTcpClient(); c.NoDelay = true; return c; }
        public static object TcpConnect(string host, long port) { var c = new System.Net.Sockets.TcpClient(host, (int)port); c.NoDelay = true; return c; }
        public static void TcpSend(object c, byte[] data) { var st = ((System.Net.Sockets.TcpClient)c).GetStream(); st.Write(data, 0, data.Length); st.Flush(); }
        public static byte[] TcpReceive(object c, long max)
        {
            var buf = new byte[System.Math.Max(1, (int)max)];
            int n = ((System.Net.Sockets.TcpClient)c).GetStream().Read(buf, 0, buf.Length);
            return buf[..n];
        }
        public static void TcpClose(object o)
        {
            if (o is System.Net.Sockets.TcpListener l) l.Stop();
            else ((System.IDisposable)o).Dispose();
        }
        public static object UdpOpen(long port) => new System.Net.Sockets.UdpClient((int)port);
        public static void UdpSend(object s, string host, long port, byte[] data) => ((System.Net.Sockets.UdpClient)s).Send(data, data.Length, host, (int)port);
        public static byte[] UdpReceive(object s, long timeoutMs)
        {
            var u = (System.Net.Sockets.UdpClient)s;
            u.Client.ReceiveTimeout = (int)timeoutMs;
            try { var ep = new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0); return u.Receive(ref ep); }
            catch (System.Net.Sockets.SocketException) { return System.Array.Empty<byte>(); }
        }
        public static byte[] BytesSlice(byte[] d, long start, long end)
        {
            int a = (int)System.Math.Clamp(start, 0, d.Length), b = (int)System.Math.Clamp(end, a, d.Length);
            return d[a..b];
        }
        public static byte[] BytesJoin(byte[] a, byte[] b) { var r = new byte[a.Length + b.Length]; a.CopyTo(r, 0); b.CopyTo(r, a.Length); return r; }
        public static byte[] BytesFromList(System.Collections.Generic.List<object> l)
        {
            var r = new byte[l.Count];
            for (int i = 0; i < r.Length; i++) r[i] = checked((byte)(long)l[i]);
            return r;
        }
        }
        """;
}
