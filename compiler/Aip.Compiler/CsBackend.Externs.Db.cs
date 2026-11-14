namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Relational database externs (T-0987). Handles are erased to object; `aip test` refuses them.</summary>
    static void AddDbExterns(Dictionary<string, string> d)
    {
        d["dbOpen"] = "Rt.DbOpen({0})";
        d["dbExec"] = "Rt.DbExec({0}, {1}, {2})";
        d["dbQuery"] = "Rt.DbQuery({0}, {1}, {2})";
        d["dbClose"] = "Rt.DbClose({0})";
    }

    static readonly string[] DbFunctions = { "dbOpen", "dbExec", "dbQuery", "dbClose" };

    /// <summary>True when the project calls a db function: only then does the generated program reference the SQLite package.</summary>
    static bool UsesDb(ProgramModel program) => program.ProjectFns.Any(f => f.Calls.Overlaps(DbFunctions));
}
