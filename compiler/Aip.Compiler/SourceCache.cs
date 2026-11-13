namespace Aip;

/// <summary>
/// Keeps a copy of every source file's text in one file, `build/sources.cache`, so a big project is read
/// with one file open instead of one per function (opening a file costs far more than parsing it).
/// It can never be stale: on every run each file's size and last-write time are compared with the cache,
/// and any file that differs, is new, or is missing from the cache is read from disk again. Deleted files
/// are dropped. If the cache is missing or damaged it is simply rebuilt. Delete it any time.
/// </summary>
sealed class SourceCache
{
    const string Magic = "aip-sources-1";
    readonly string _path;
    readonly Dictionary<string, (long Size, long Ticks, string Text)> _old = new(StringComparer.Ordinal);
    readonly Dictionary<string, (long Size, long Ticks, string Text)> _now = new(StringComparer.Ordinal);
    public int Hits, Misses;

    public SourceCache(string path)
    {
        _path = path;
        try
        {
            if (!File.Exists(path)) return;
            using var r = new BinaryReader(File.OpenRead(path));
            if (r.ReadString() != Magic) return;
            var n = r.ReadInt32();
            for (int i = 0; i < n; i++) _old[r.ReadString()] = (r.ReadInt64(), r.ReadInt64(), r.ReadString());
        }
        catch (Exception e) when (e is IOException or EndOfStreamException or UnauthorizedAccessException) { _old.Clear(); }
    }

    /// <summary>Returns the file's text, from the cache when size and last-write time still match. Thread safe.</summary>
    public string Read(FileInfo f)
    {
        var key = f.FullName;
        var size = f.Length;
        var ticks = f.LastWriteTimeUtc.Ticks;
        string text;
        if (_old.TryGetValue(key, out var e) && e.Size == size && e.Ticks == ticks)
        {
            text = e.Text;
            Interlocked.Increment(ref Hits);
        }
        else
        {
            text = File.ReadAllText(key);
            Interlocked.Increment(ref Misses);
        }
        lock (_now) _now[key] = (size, ticks, text);
        return text;
    }

    /// <summary>Writes the cache if any file was added, changed or removed. Failure only costs speed next time.</summary>
    public void Save()
    {
        if (Misses == 0 && _old.Count == _now.Count) return;
        var tmp = _path + "." + Environment.ProcessId + ".tmp"; // other aip runs may save at the same time
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using (var w = new BinaryWriter(File.Create(tmp)))
            {
                w.Write(Magic);
                w.Write(_now.Count);
                foreach (var (k, v) in _now.OrderBy(x => x.Key, StringComparer.Ordinal)) { w.Write(k); w.Write(v.Size); w.Write(v.Ticks); w.Write(v.Text); }
            }
            File.Move(tmp, _path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { try { File.Delete(tmp); } catch (IOException) { } }
    }
}
