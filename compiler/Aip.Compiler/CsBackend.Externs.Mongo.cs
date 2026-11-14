namespace Aip;

sealed partial class CsBackend
{
    /// <summary>MongoDB externs (T-0987). Documents travel as JSON text; `aip test` refuses them.</summary>
    static void AddMongoExterns(Dictionary<string, string> d)
    {
        d["mongoOpen"] = "Rt.MongoOpen({0}, {1})";
        d["mongoInsert"] = "Rt.MongoInsert({0}, {1}, {2})";
        d["mongoFind"] = "Rt.MongoFind({0}, {1}, {2})";
        d["mongoUpdate"] = "Rt.MongoUpdate({0}, {1}, {2}, {3})";
        d["mongoDelete"] = "Rt.MongoDelete({0}, {1}, {2})";
        d["mongoDrop"] = "Rt.MongoDrop({0}, {1})";
        d["mongoClose"] = "Rt.MongoClose({0})";
    }

    static readonly string[] MongoFunctions = { "mongoOpen", "mongoInsert", "mongoFind", "mongoUpdate", "mongoDelete", "mongoDrop", "mongoClose" };

    /// <summary>True when the project calls a mongo function: only then does the generated program reference MongoDB.Driver.</summary>
    static bool UsesMongo(ProgramModel program) => program.ProjectFns.Any(f => f.Calls.Overlaps(MongoFunctions));
}
