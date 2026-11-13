namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Queue and PriorityQueue externs.</summary>
    static void AddQueueExterns(Dictionary<string, string> d)
    {
        d["queueEmpty"] = "new System.Collections.Generic.List<object>()";
        d["queueSize"] = "(long)({0}).Count";
        d["queuePush"] = "Rt.Append({0}, {1})";
        d["queueFront"] = "Rt.Front({0}, \"queueFront\")";
        d["queuePop"] = "Rt.Pop({0}, \"queuePop\")";
        d["priorityQueueEmpty"] = "new System.Collections.Generic.List<object>()";
        d["priorityQueueSize"] = "(long)({0}).Count";
        d["priorityQueuePush"] = "Rt.PqPush({0}, {1}, {2})";
        d["priorityQueueFront"] = "((object[])Rt.Front({0}, \"priorityQueueFront\"))[1]";
        d["priorityQueuePop"] = "Rt.Pop({0}, \"priorityQueuePop\")";
    }
}
