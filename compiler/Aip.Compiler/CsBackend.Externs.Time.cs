namespace Aip;

sealed partial class CsBackend
{
    /// <summary>Clock and sleep, float math and random numbers (games, simulations).</summary>
    static void AddTimeExterns(Dictionary<string, string> d)
    {
        d["timeNow"] = "Rt.TimeNow()";
        d["timeMonotonic"] = "Rt.TimeMono()";
        d["timeSleep"] = "Rt.TimeSleep({0})";
        d["mathSqrt"] = "System.Math.Sqrt({0})";
        d["mathFloor"] = "System.Math.Floor({0})";
        d["mathCeil"] = "System.Math.Ceiling({0})";
        d["mathRound"] = "System.Math.Round({0}, System.MidpointRounding.AwayFromZero)";
        d["mathSin"] = "System.Math.Sin({0})";
        d["mathCos"] = "System.Math.Cos({0})";
        d["mathTan"] = "System.Math.Tan({0})";
        d["mathExp"] = "System.Math.Exp({0})";
        d["mathLog"] = "System.Math.Log({0})";
        d["mathAbs"] = "System.Math.Abs({0})";
        d["mathPow"] = "System.Math.Pow({0}, {1})";
        d["mathAtan2"] = "System.Math.Atan2({0}, {1})";
        d["mathMin"] = "System.Math.Min({0}, {1})";
        d["mathMax"] = "System.Math.Max({0}, {1})";
        d["mathClamp"] = "Rt.MathClamp({0}, {1}, {2})";
        d["mathLerp"] = "(({0}) + (({1}) - ({0})) * ({2}))";
        d["mathIsNaN"] = "double.IsNaN({0})";
        d["mathPi"] = "System.Math.PI";
        d["textToFloat"] = "double.Parse({0}, System.Globalization.CultureInfo.InvariantCulture)";
        d["randomNext"] = "Rt.RandNext({0})";
        d["randomRange"] = "Rt.RandRange({0}, {1})";
        d["randomFloat"] = "Rt.RandFloat({0})";
        d["randomSeed"] = "Rt.EntropySeed()";
    }
}
