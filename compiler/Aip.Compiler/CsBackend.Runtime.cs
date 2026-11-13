namespace Aip;

sealed partial class CsBackend
{
    /// <summary>The generated `Rt` runtime class, one file per topic. Add a new part with its own const and one line here.</summary>
    static readonly (string File, string Source)[] RuntimeParts =
    {
        ("Rt.Core.cs", RtCore),
        ("Rt.Map.cs", RtMap),
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
            public static void Nop() { }
            public static void Write(string path, string content) => System.IO.File.WriteAllText(path, content);
            public static string Text(string v) => v;
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

}
