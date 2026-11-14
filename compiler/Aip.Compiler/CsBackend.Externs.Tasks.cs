namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Function-value externs (listMap, ...) and tasks (T-0928, T-0935).</summary>
    static void AddTaskExterns(Dictionary<string, string> d)
    {
        d["listMap"] = "Rt.ListMap({0}, {1})";
        d["listFilter"] = "Rt.ListFilter({0}, {1})";
        d["listFold"] = "Rt.ListFold({0}, {1}, {2})";
        d["listMapPar"] = "Rt.ListMapPar({0}, {1})";
        d["taskRun"] = "Rt.TaskRun({0}, {1})";
        d["taskAwait"] = "Rt.TaskAwait({0})";
    }
}
