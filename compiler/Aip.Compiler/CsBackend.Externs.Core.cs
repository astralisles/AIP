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
        d["textFind"] = "(long)({0}).IndexOf({1}, System.StringComparison.Ordinal)";
        d["textContains"] = "({0}).Contains({1}, System.StringComparison.Ordinal)";
        d["textStartsWith"] = "({0}).StartsWith({1}, System.StringComparison.Ordinal)";
        d["textEndsWith"] = "({0}).EndsWith({1}, System.StringComparison.Ordinal)";
        d["textTrim"] = "({0}).Trim()";
        d["textReplace"] = "({0}).Replace({1}, {2}, System.StringComparison.Ordinal)";
        d["textToUpper"] = "({0}).ToUpperInvariant()";
        d["textToLower"] = "({0}).ToLowerInvariant()";
        d["textCompare"] = "(long)System.Math.Sign(string.CompareOrdinal({0}, {1}))";
        d["textFromInt"] = "({0}).ToString(System.Globalization.CultureInfo.InvariantCulture)";
        d["textRepeat"] = "string.Concat(System.Linq.Enumerable.Repeat({0}, (int)({1})))";
        d["textSplit"] = "Aip.Persistent.PList.From(({0}).Split({1}, System.StringSplitOptions.None))";
        d["textJoin"] = "string.Join({1}, System.Linq.Enumerable.Cast<string>({0}))";
        d["intMin"] = "System.Math.Min({0}, {1})";
        d["intMax"] = "System.Math.Max({0}, {1})";
        d["intAbs"] = "System.Math.Abs({0})";
        d["intSign"] = "(long)System.Math.Sign({0})";
        d["intClamp"] = "System.Math.Clamp({0}, {1}, {2})";
        d["intPow"] = "(long)System.Numerics.BigInteger.Pow({0}, (int)({1}))";
        d["intGcd"] = "(long)System.Numerics.BigInteger.GreatestCommonDivisor({0}, {1})";
        d["intAnd"] = "(({0}) & ({1}))";
        d["intOr"] = "(({0}) | ({1}))";
        d["intXor"] = "(({0}) ^ ({1}))";
        d["intNot"] = "(~({0}))";
        d["intShiftLeft"] = "(({0}) << (int)({1}))";
        d["intShiftRight"] = "(({0}) >> (int)({1}))";
        d["numToI8"] = "checked((sbyte)({0}))";
        d["numFromI8"] = "checked((long)({0}))";
        d["numToI16"] = "checked((short)({0}))";
        d["numFromI16"] = "checked((long)({0}))";
        d["numToI32"] = "checked((int)({0}))";
        d["numFromI32"] = "checked((long)({0}))";
        d["numToU8"] = "checked((byte)({0}))";
        d["numFromU8"] = "checked((long)({0}))";
        d["numToU16"] = "checked((ushort)({0}))";
        d["numFromU16"] = "checked((long)({0}))";
        d["numToU32"] = "checked((uint)({0}))";
        d["numFromU32"] = "checked((long)({0}))";
        d["numToU64"] = "checked((ulong)({0}))";
        d["numFromU64"] = "checked((long)({0}))";
        d["numToF32"] = "((float)({0}))";
        d["numToF64"] = "((double)({0}))";
        d["numFromF32"] = "Rt.ToInt((double)({0}))";
        d["numFromF64"] = "Rt.ToInt({0})";
        d["numF32ToF64"] = "((double)({0}))";
        d["numF64ToF32"] = "((float)({0}))";
        d["fileRead"] = "System.IO.File.ReadAllBytes({0})";
        d["fileWrite"] = "Rt.Write({0}, {1})";
        d["noop"] = "Rt.Nop()";
        d["argCount"] = "(long)Rt.Args.Length";
        d["arg"] = "Rt.Args[(int)({0})]";
    }
}
