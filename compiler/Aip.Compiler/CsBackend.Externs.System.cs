namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Process, environment, clock, randomness and file-system externs (all effectful).</summary>
    static void AddSystemExterns(Dictionary<string, string> d)
    {
        d["processExit"] = "Rt.Exit({0})";
        d["fileExists"] = "Rt.FileExists({0})";
        d["fileAppend"] = "Rt.Append({0}, {1})";
        d["dirList"] = "Rt.DirList({0})";
        d["randomInt"] = "Rt.RandomInt({0}, {1})";
        d["envGet"] = "(System.Environment.GetEnvironmentVariable({0}) ?? \"\")";
        d["envHas"] = "(System.Environment.GetEnvironmentVariable({0}) != null)";
    }
}
