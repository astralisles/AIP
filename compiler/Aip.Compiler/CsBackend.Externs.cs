namespace Aip;

sealed partial class CsBackend
{
    /// <summary>How each std extern function is written in C#. {0}, {1}... are the arguments.
    /// Each family lives in its own file (CsBackend.Externs.*.cs) so parallel branches rarely touch the same one.</summary>
    static readonly Dictionary<string, string> Externs = BuildExterns();

    static Dictionary<string, string> BuildExterns()
    {
        var d = new Dictionary<string, string>();
        AddCoreExterns(d);
        AddListExterns(d);
        AddMapExterns(d);
        AddQueueExterns(d);
        AddSortedExterns(d);
        AddTaskExterns(d);
        AddNetExterns(d);
        AddTimeExterns(d);
        AddSystemExterns(d);
        return d;
    }
}
