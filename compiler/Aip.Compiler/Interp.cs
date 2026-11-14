using System.Globalization;
using System.Numerics;

namespace Aip;

/// <summary>A struct or variant value: which case, and its field values in declaration order.</summary>
sealed record Val(Case Case, object?[] Fields);


/// <summary>
/// Tree-walking evaluator for pure functions. It runs `example` lines and checks `requires`, `ensures` and
/// parameter/result ranges at every call. It follows the C# back end's rules (checked sized integers, truncating division, IEEE floats).
/// </summary>
sealed partial class Interp(ProgramModel program)
{
    /// <summary>The function needs something the interpreter does not have (an effect, an unsupported extern).</summary>
    public sealed class Unsupported(string message) : Exception(message);

    /// <summary>A contract, range or arithmetic rule did not hold.</summary>
    public sealed class Failed(string code, string message) : Exception(message)
    {
        public string Code = code;
    }

    object? CallIndirect(Call ic, Dictionary<string, object?> env) =>
        CallFn(((FnVal)env[ic.Name]!).Target, ic.Args.Select(a => Eval(a, env)).ToList());

    int _depth;
    const int MaxDepth = 100_000;

    object? CallFn(Fn fn, List<object?> args)
    {
        if (fn.Effects.Count > 0) throw new Unsupported($"`{fn.Name}` has effects ({fn.EffectsText})");
        _active = this;
        for (int i = 0; i < fn.Params.Count; i++) args[i] = Coerce(args[i], fn.Params[i].Type.Name);
        for (int i = 0; i < fn.Params.Count; i++) CheckRange(fn.Params[i].Type, args[i], $"argument `{fn.Params[i].Name}` of `{fn.Name}`");
        if (fn.IsExtern) return Extern(fn.Name, args);
        if (++_depth > MaxDepth) throw new Failed("recursion-too-deep", $"`{fn.Name}` recursed more than {MaxDepth} calls deep");
        try
        {
            var env = new Dictionary<string, object?>();
            for (int i = 0; i < fn.Params.Count; i++) env[fn.Params[i].Name] = args[i];
            foreach (var r in fn.Requires)
                if (Eval(r, env) is false) throw new Failed("requires-violated", $"`requires` of `{fn.Name}` ({Path.GetFileName(fn.File)}:{r.Line}) does not hold for ({Show(args)})");
            object? result = null;
            for (int i = 0; i < fn.Body.Count; i++)
            {
                var v = Eval(fn.Body[i].Value, env);
                if (fn.Body[i].Let != null) env[fn.Body[i].Let!] = v;
                else if (i == fn.Body.Count - 1) result = v;
            }
            result = Coerce(result, fn.Ret.Name);
            CheckRange(fn.Ret, result, $"the result of `{fn.Name}`");
            var post = new Dictionary<string, object?>(env);
            if (fn.Ret.Name != "Unit") post["result"] = result;
            foreach (var en in fn.Ensures)
                if (Eval(en, post) is false) throw new Failed("ensures-violated", $"`ensures` of `{fn.Name}` ({Path.GetFileName(fn.File)}:{en.Line}) does not hold for ({Show(args)}) -> {Show(result)}");
            return result;
        }
        finally { _depth--; }
    }

    /// <summary>
    /// For lint: when every argument is a constant (no names), returns the first `requires` of `fn` that is false for them,
    /// or null when they all hold or the answer cannot be computed here (an effect, an unsupported extern, a failing call).
    /// </summary>
    public static (Expr Requires, string Args)? FirstBrokenRequires(ProgramModel program, Fn fn, List<Expr> args)
    {
        if (fn.Requires.Count == 0 || args.Count != fn.Params.Count) return null;
        if (args.Any(a => Lint.All(a).Any(x => x is NameRef))) return null;
        var interp = new Interp(program);
        try
        {
            var values = args.Select(a => interp.Eval(a, new Dictionary<string, object?>())).ToList();
            var env = new Dictionary<string, object?>();
            for (int i = 0; i < fn.Params.Count; i++) env[fn.Params[i].Name] = values[i];
            foreach (var r in fn.Requires)
                if (interp.Eval(r, env) is false) return (r, Show(values));
        }
        catch (Exception ex) when (ex is Unsupported or Failed or InvalidCastException or KeyNotFoundException) { }
        return null;
    }

    static string Show(IEnumerable<object?> vs) => string.Join(", ", vs.Select(Show));

    static string Show(object? v) => v switch
    {
        null => "unit",
        string s => "\"" + s + "\"",
        bool b => b ? "true" : "false",
        Val x => x.Case.Name + "(" + Show(x.Fields) + ")",
        List<object?> l => "[" + Show(l) + "]",
        _ when IsNumber(v) => NumText(v),
        _ => v.ToString() ?? "",
    };

    static void CheckRange(TypeRef t, object? v, string what)
    {
        if (t.Range == "") return;
        var parts = t.Range.Split("..");
        long n = v switch { string s => s.Length, List<object?> l => l.Count, not null when IsInteger(v) && Big(v) >= long.MinValue && Big(v) <= long.MaxValue => (long)Big(v), _ => long.MinValue };
        if (n == long.MinValue) return;
        if ((parts[0] != "" && n < long.Parse(parts[0], CultureInfo.InvariantCulture)) || (parts[1] != "" && n > long.Parse(parts[1], CultureInfo.InvariantCulture)))
            throw new Failed("range-violated", $"{what} is {Show(v)}, outside {t.Range}");
    }

    object? Eval(Expr e, Dictionary<string, object?> env)
    {
        switch (e)
        {
            case IntLit i: return Coerce((BigInteger)i.Value, i.Type == "" ? "Int" : i.Type);
            case FloatLit fl: return fl.Type == "F32" ? (object)(float)fl.Value : fl.Value;
            case BoolLit b: return b.Value;
            case NameRef { Const: true } k: return CallFn(program.Fns[k.Name], new List<object?>());
            case FnRef fr: return new FnVal(program.Fns[fr.Name]);
            case NameRef n: return env[n.Name];
            case TextLit t:
                return string.Concat(t.Parts.Select(p => p is string s ? s : Text(Eval((Expr)p, env))));
            case Call { Ctor: not null } c:
                return new Val(program.Ctors[c.Name].Case, c.Args.Select(a => Eval(a, env)).ToArray());
            case Call { Indirect: true } ic:
                return CallIndirect(ic, env);
            case Call c:
                return CallFn(program.Fns[c.Name], c.Args.Select(a => Eval(a, env)).ToList());
            case Assign a:
                {
                    var v = (Val)Eval(a.Target.Target, env)!;
                    v.Fields[v.Case.Fields.FindIndex(p => p.Name == a.Target.Name)] = Eval(a.Value, env);
                    return null;
                }
            case Field f:
                {
                    var v = (Val)Eval(f.Target, env)!;
                    return v.Fields[v.Case.Fields.FindIndex(p => p.Name == f.Name)];
                }
            case MatchExpr m:
                {
                    var v = (Val)Eval(m.Subject, env)!;
                    var arm = m.Arms.First(a => a.Case == v.Case.Name);
                    var inner = new Dictionary<string, object?>(env);
                    for (int i = 0; i < arm.Binds.Count; i++) inner[arm.Binds[i]] = v.Fields[i];
                    return Eval(arm.Body, inner);
                }
            case Unary u:
                {
                    var v = Eval(u.Operand, env);
                    if (u.Op == "~") return BitOp("~", Big(v!), 0, u.Type == "" ? "Int" : u.Type);
                    if (u.Op != "-") return !(bool)v!;
                    if (v is double dv) return -dv;
                    if (v is float fv) return -fv;
                    return Fit(-Big(v!), u.Type == "" ? "Int" : u.Type, "negation");
                }
            case BlockExpr k:
                foreach (var s in k.Stmts) Eval(s, env);
                return null;
            case IfExpr i:
                return (bool)Eval(i.Cond, env)! ? Eval(i.Then, env) : Eval(i.Else, env);
            case LoopExpr lp: return EvalLoop(lp, env);
            case ForExpr fr: return EvalFor(fr, env);
            case Binary b:
                {
                    if (b.Op == "and") return (bool)Eval(b.Left, env)! && (bool)Eval(b.Right, env)!;
                    if (b.Op == "or") return (bool)Eval(b.Left, env)! || (bool)Eval(b.Right, env)!;
                    if (b.Op == "=>") return !(bool)Eval(b.Left, env)! || (bool)Eval(b.Right, env)!;
                    var l = Eval(b.Left, env);
                    var r = Eval(b.Right, env);
                    bool num = l != null && r != null && IsNumber(l) && IsNumber(r);
                    if (b.Op == "==") return num ? Compare(l!, r!) == 0 : Equals(l, r);
                    if (b.Op == "!=") return num ? Compare(l!, r!) != 0 : !Equals(l, r);
                    if (b.Op is "<" or "<=" or ">" or ">=")
                    {
                        int c = Compare(l!, r!);
                        return b.Op switch { "<" => c < 0, "<=" => c <= 0, ">" => c > 0, _ => c >= 0 };
                    }
                    if (l is double or float || r is double or float)
                    {
                        double p = ToDouble(l!), q = ToDouble(r!);
                        double res = b.Op switch { "+" => p + q, "-" => p - q, "*" => p * q, "/" => p / q, "%" => p % q, _ => throw new Unsupported($"operator {b.Op}") };
                        return b.Type == "F32" ? (object)(float)res : res;
                    }
                    BigInteger x = Big(l!), y = Big(r!);
                    if (b.Op is "&" or "|" or "^" or "<<" or ">>") return BitOp(b.Op, x, y, b.Type == "" ? "Int" : b.Type);
                    if (b.Op is "/" or "%" && y.IsZero) throw new Failed("arithmetic", $"`{x} {b.Op} {y}`: division by zero");
                    BigInteger z = b.Op switch { "+" => x + y, "-" => x - y, "*" => x * y, "/" => BigInteger.Divide(x, y), "%" => BigInteger.Remainder(x, y), _ => throw new Unsupported($"operator {b.Op}") };
                    return Fit(z, b.Type == "" ? "Int" : b.Type, $"`{x} {b.Op} {y}`");
                }
        }
        throw new Unsupported("expression " + e.GetType().Name);
    }

    /// <summary>`&amp; | ^ ~ &lt;&lt; &gt;&gt;` on a whole number of type `ty`: the result keeps the type, bits shifted out are lost, a bad shift count is an error.</summary>
    static object BitOp(string op, BigInteger x, BigInteger y, string ty)
    {
        var (_, max) = Limits[ty];
        int width = ty switch { "Int" or "U64" => 64, "I32" or "U32" => 32, "I16" or "U16" => 16, _ => 8 };
        BigInteger mod = BigInteger.One << width;
        BigInteger Wrap(BigInteger v) { v = ((v % mod) + mod) % mod; return v > max ? v - mod : v; }
        BigInteger res;
        if (op is "<<" or ">>")
        {
            if (y < 0 || y >= width) throw new InvalidOperationException($"shift count {y} must be 0 to {width - 1}");
            res = op == "<<" ? Wrap(x << (int)y) : x >> (int)y;
        }
        else if (op == "~") res = Wrap(-x - 1);
        else
        {
            BigInteger ux = (x + mod) % mod, uy = (y + mod) % mod;
            res = Wrap(op == "&" ? ux & uy : op == "|" ? ux | uy : ux ^ uy);
        }
        return Fit(res, ty, op);
    }

    static bool IsInteger(object v) => v is long or int or short or sbyte or ulong or uint or ushort or byte;
    static bool IsNumber(object v) => IsInteger(v) || v is double or float;
    static BigInteger Big(object v) => v switch
    {
        long x => x, int x => x, short x => x, sbyte x => x, ulong x => x, uint x => x, ushort x => x, byte x => x, BigInteger x => x,
        _ => throw new Unsupported("whole number expected"),
    };
    static double ToDouble(object v) => v switch { double d => d, float f => f, _ => (double)Big(v) };
    static string NumText(object v) => v switch
    {
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        _ => Big(v).ToString(CultureInfo.InvariantCulture),
    };
    static int Compare(object l, object r) => l is double or float || r is double or float ? ToDouble(l).CompareTo(ToDouble(r)) : Big(l).CompareTo(Big(r));

    static readonly Dictionary<string, (BigInteger Min, BigInteger Max)> Limits = new()
    {
        ["Int"] = (long.MinValue, long.MaxValue), ["I8"] = (sbyte.MinValue, sbyte.MaxValue), ["I16"] = (short.MinValue, short.MaxValue),
        ["I32"] = (int.MinValue, int.MaxValue), ["U8"] = (0, byte.MaxValue), ["U16"] = (0, ushort.MaxValue),
        ["U32"] = (0, uint.MaxValue), ["U64"] = (0, ulong.MaxValue),
    };

    /// <summary>The value as a whole number of the named type (the interpreter holds the same CLR types as the C# back end); out of range is a run-time error.</summary>
    static object Fit(BigInteger v, string type, string what)
    {
        var (min, max) = Limits[type];
        if (v < min || v > max) throw new Failed("arithmetic", $"{what}: {v} does not fit in {type}");
        return type switch
        {
            "Int" => (long)v, "I8" => (sbyte)v, "I16" => (short)v, "I32" => (int)v,
            "U8" => (byte)v, "U16" => (ushort)v, "U32" => (uint)v, _ => (ulong)v,
        };
    }

    /// <summary>Widens a number to the type a parameter or result declares; other values pass through.</summary>
    static object? Coerce(object? v, string type)
    {
        if (v == null) return null;
        if (v is BigInteger b) return Fit(b, type, "literal");
        if (type == "F64" && v is float f) return (double)f;
        if (Limits.ContainsKey(type) && IsInteger(v)) return Fit(Big(v), type, "value");
        return v;
    }

    static string Text(object? v) => v switch
    {
        string s => s,
        bool b => b ? "true" : "false",
        _ when v != null && IsNumber(v) => NumText(v),
        _ => throw new Unsupported("text of " + v),
    };

    // Same behaviour as the C# back end's Externs; effectful ones are refused before we get here.
    // Each family lives in Interp.Externs.*.cs and returns NotHandled for names it does not know.
    static readonly object NotHandled = new();

    static object? Extern(string name, List<object?> a)
    {
        try
        {
            foreach (var family in ExternFamilies)
            {
                var r = family(name, a);
                if (!ReferenceEquals(r, NotHandled)) return r;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new Failed("extern-failed", $"`{name}`: {ex.Message}");
        }
        throw new Unsupported($"the standard function `{name}` is not available in `aip test`");
    }

    static readonly Func<string, List<object?>, object?>[] ExternFamilies =
    {
        CoreExtern,
        ListExtern,
        MapExtern,
        QueueExtern,
        SortedExtern,
        NetExtern,
        TimeExtern,
        FnExtern,
    };

    /// <summary>
    /// Runs every `example` of every project function (on a big stack, like the compiled program).
    /// Returns how many passed, how many were skipped (they need an effect), the failures, and pure functions without any example.
    /// </summary>
    public static (int Passed, int Skipped, List<Diagnostic> Failures, List<string> NoExample) RunExamples(ProgramModel program)
    {
        int passed = 0, skipped = 0;
        var failures = new List<Diagnostic>();
        var none = new List<string>();
        var t = new Thread(() =>
        {
            var interp = new Interp(program);
            foreach (var fn in program.ProjectFns)
            {
                if (fn.Examples.Count == 0 && fn.Effects.Count == 0 && fn.Name != "main") none.Add(fn.Name);
                foreach (var ex in fn.Examples)
                {
                    try
                    {
                        interp._depth = 0;
                        if (interp.Eval(ex, new Dictionary<string, object?>()) is true) passed++;
                        else failures.Add(new Diagnostic(fn.File, ex.Line, "example-failed", $"an example of `{fn.Name}` is false"));
                    }
                    catch (Unsupported) { skipped++; }
                    catch (Failed f) { failures.Add(new Diagnostic(fn.File, ex.Line, f.Code, f.Message)); }
                }
            }
        }, 256 * 1024 * 1024);
        t.Start();
        t.Join();
        return (passed, skipped, failures, none);
    }
}
