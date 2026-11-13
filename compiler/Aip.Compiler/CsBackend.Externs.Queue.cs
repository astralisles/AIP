namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Queue and PriorityQueue externs.</summary>
    static void AddQueueExterns(Dictionary<string, string> d)
    {
        d["queueEmpty"] = "Aip.Persistent.PFifo.Empty";
        d["queueSize"] = "(long)({0}).Count";
        d["queuePush"] = "({0}).Push({1})";
        d["queueFront"] = "({0}).Front(\"queueFront\")";
        d["queuePop"] = "({0}).Pop(\"queuePop\")";
        d["priorityQueueEmpty"] = "Aip.Persistent.PQueue.Empty";
        d["priorityQueueSize"] = "(long)({0}).Count";
        d["priorityQueuePush"] = "({0}).Push({1}, {2})";
        d["priorityQueueFront"] = "({0}).Front(\"priorityQueueFront\")";
        d["priorityQueuePop"] = "({0}).Pop(\"priorityQueuePop\")";
    }
}
