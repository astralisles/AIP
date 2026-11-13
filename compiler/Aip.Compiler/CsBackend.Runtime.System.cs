namespace Aip;

sealed partial class CsBackend
{
    const string RtSystem = """
        public static partial class Rt
        {
            public static void Exit(long code)
            {
                Report((int)code);
                System.Console.Out.Flush();
                System.Environment.Exit((int)code);
            }
            public static bool FileExists(string path) => System.IO.File.Exists(path) || System.IO.Directory.Exists(path);
            public static void Append(string path, string content) => System.IO.File.AppendAllText(path, content);
            public static Aip.Persistent.PList DirList(string path)
            {
                var names = new System.Collections.Generic.List<string>();
                foreach (var e in System.IO.Directory.EnumerateFileSystemEntries(path)) names.Add(System.IO.Path.GetFileName(e));
                names.Sort(System.StringComparer.Ordinal);
                var r = Aip.Persistent.PList.Empty;
                foreach (var n in names) r = r.Append(n);
                return r;
            }
            static readonly System.Random rng = new System.Random();
            public static long RandomInt(long low, long high)
            {
                if (high <= low) throw new System.ArgumentException("randomInt needs low < high, got " + low + ", " + high);
                lock (rng) return rng.NextInt64(low, high);
            }
        }
        """;
}
