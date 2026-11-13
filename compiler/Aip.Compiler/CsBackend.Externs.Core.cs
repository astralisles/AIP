namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Text, console, file, argument and no-op externs.</summary>
    static void AddCoreExterns(Dictionary<string, string> d)
    {
        d["logLine"] = "System.Console.Error.Write({0} + \"\\n\")";
        d["printLine"] = "System.Console.Out.Write({0} + \"\\n\")";
        d["textLength"] = "(long)({0}).Length";
        d["textSlice"] = "Rt.Slice({0}, {1}, {2})";
        d["textCode"] = "(long)({0})[(int)({1})]";
        d["textFromCode"] = "((char)({0})).ToString()";
        d["textToInt"] = "long.Parse({0}, System.Globalization.CultureInfo.InvariantCulture)";
        d["fileRead"] = "System.IO.File.ReadAllText({0})";
        d["fileWrite"] = "Rt.Write({0}, {1})";
        d["noop"] = "Rt.Nop()";
        d["argCount"] = "(long)Rt.Args.Length";
        d["arg"] = "Rt.Args[(int)({0})]";
    }
}
