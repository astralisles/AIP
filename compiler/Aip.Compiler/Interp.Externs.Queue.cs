using System.Globalization;

namespace Aip;

sealed partial class Interp
{
    /// <summary>Queue and PriorityQueue.</summary>
    static object? QueueExtern(string name, List<object?> a)
    {
        List<object?> L(int i) => (List<object?>)a[i]!;
        switch (name)
        {
            case "queueEmpty" or "priorityQueueEmpty": return new List<object?>();
            case "queueSize" or "priorityQueueSize": return (long)L(0).Count;
            case "queuePush": return new List<object?>(L(0)) { a[1] };
            case "queueFront": return L(0).Count > 0 ? L(0)[0] : throw new KeyNotFoundException("queueFront: the queue is empty");
            case "priorityQueueFront": return L(0).Count > 0 ? ((object?[])L(0)[0]!)[1] : throw new KeyNotFoundException("priorityQueueFront: the queue is empty");
            case "queuePop" or "priorityQueuePop": return L(0).Count > 0 ? L(0).GetRange(1, L(0).Count - 1) : throw new KeyNotFoundException(name + ": the queue is empty");
            case "priorityQueuePush":
                {
                    var l = L(0); int i = l.Count; long p = (long)a[2]!;
                    while (i > 0 && (long)((object?[])l[i - 1]!)[0]! > p) i--;
                    var r = new List<object?>(l); r.Insert(i, new object?[] { p, a[1] }); return r;
                }
        }
        return NotHandled;
    }
}
