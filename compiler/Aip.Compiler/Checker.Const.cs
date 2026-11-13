namespace Aip;

/// <summary>Checker: constants (.const files).</summary>
sealed partial class Checker
{
    bool IsConstName(string name) => program.Fns.TryGetValue(name, out var f) && f.IsConst;

    /// <summary>A constant's value may not depend on itself, directly or through functions and other constants.</summary>
    void CheckConstCycles()
    {
        var reported = new HashSet<string>();
        foreach (var k in program.Fns.Values.Where(f => f.IsConst).OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            if (reported.Contains(k.Name)) continue; // one error per cycle, not one per constant in it
            // Breadth-first from the constant's direct uses; keep the path to report it.
            var from = new Dictionary<string, string>();
            var queue = new Queue<string>();
            foreach (var c in k.Calls.OrderBy(c => c, StringComparer.Ordinal)) if (from.TryAdd(c, k.Name)) queue.Enqueue(c);
            while (queue.Count > 0)
            {
                var name = queue.Dequeue();
                if (name == k.Name)
                {
                    var path = new List<string> { k.Name };
                    for (var at = from[k.Name]; at != k.Name; at = from[at]) path.Insert(0, at);
                    path.Insert(0, k.Name);
                    reported.UnionWith(path);
                    _fn = k;
                    Error(k.Line, "const-cycle", $"constant `{k.Name}` depends on itself: {string.Join(" -> ", path)}", "compute one of the values without the other");
                    break;
                }
                if (!program.Fns.TryGetValue(name, out var f)) continue;
                foreach (var c in f.Calls.OrderBy(c => c, StringComparer.Ordinal)) if (from.TryAdd(c, name)) queue.Enqueue(c);
            }
        }
    }
}
