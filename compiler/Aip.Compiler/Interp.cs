using System.Globalization;

namespace Aip;

/// <summary>A struct or variant value: which case, and its field values in declaration order.</summary>
sealed record Val(Case Case, object?[] Fields);

/// <summary>
/// Tree-walking evaluator for pure functions. It runs `example` lines and checks `requires`, `ensures` and
/// parameter/result ranges at every call. It follows the C# back end's rules (64-bit checked Int, truncating division).
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

    int _depth;
    const int MaxDepth = 100_000;

    object? CallFn(Fn fn, List<object?> args)
    {
        if (fn.Effects.Count > 0) throw new Unsupported($"`{fn.Name}` has effects ({fn.EffectsText})");
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
        long l => l.ToString(CultureInfo.InvariantCulture),
        Val x => x.Case.Name + "(" + Show(x.Fields) + ")",
        List<object?> l => "[" + Show(l) + "]",
        _ => v.ToString() ?? "",
    };

    static void CheckRange(TypeRef t, object? v, string what)
    {
        if (t.Range == "") return;
        var parts = t.Range.Split("..");
        long n = v switch { long l => l, string s => s.Length, List<object?> l => l.Count, _ => long.MinValue };
        if (n == long.MinValue) return;
        if ((parts[0] != "" && n < long.Parse(parts[0], CultureInfo.InvariantCulture)) || (parts[1] != "" && n > long.Parse(parts[1], CultureInfo.InvariantCulture)))
            throw new Failed("range-violated", $"{what} is {Show(v)}, outside {t.Range}");
    }

    object? Eval(Expr e, Dictionary<string, object?> env)
    {
        switch (e)
        {
            case IntLit i: return i.Value;
            case BoolLit b: return b.Value;
            case NameRef { Const: true } k: return CallFn(program.Fns[k.Name], new List<object?>());
            case NameRef n: return env[n.Name];
            case TextLit t:
                return string.Concat(t.Parts.Select(p => p is string s ? s : Text(Eval((Expr)p, env))));
            case Call { Ctor: not null } c:
                return new Val(program.Ctors[c.Name].Case, c.Args.Select(a => Eval(a, env)).ToArray());
            case Call c:
                return CallFn(program.Fns[c.Name], c.Args.Select(a => Eval(a, env)).ToList());
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
                    try { return u.Op == "-" ? checked(-(long)v!) : !(bool)v!; }
                    catch (OverflowException) { throw new Failed("arithmetic", "negation overflows Int"); }
                }
            case BlockExpr k:
                foreach (var s in k.Stmts) Eval(s, env);
                return null;
            case IfExpr i:
                return (bool)Eval(i.Cond, env)! ? Eval(i.Then, env) : Eval(i.Else, env);
            case Binary b:
                {
                    if (b.Op == "and") return (bool)Eval(b.Left, env)! && (bool)Eval(b.Right, env)!;
                    if (b.Op == "or") return (bool)Eval(b.Left, env)! || (bool)Eval(b.Right, env)!;
                    if (b.Op == "=>") return !(bool)Eval(b.Left, env)! || (bool)Eval(b.Right, env)!;
                    var l = Eval(b.Left, env);
                    var r = Eval(b.Right, env);
                    if (b.Op == "==") return Equals(l, r);
                    if (b.Op == "!=") return !Equals(l, r);
                    long x = (long)l!, y = (long)r!;
                    try
                    {
                        return b.Op switch
                        {
                            "+" => checked(x + y),
                            "-" => checked(x - y),
                            "*" => checked(x * y),
                            "/" => x / y,
                            "%" => x % y,
                            "<" => x < y,
                            "<=" => x <= y,
                            ">" => x > y,
                            ">=" => x >= y,
                            _ => throw new Unsupported($"operator {b.Op}"),
                        };
                    }
                    catch (Exception ex) when (ex is DivideByZeroException or OverflowException)
                    {
                        throw new Failed("arithmetic", $"`{x} {b.Op} {y}`: {ex.Message}");
                    }
                }
        }
        throw new Unsupported("expression " + e.GetType().Name);
    }

    static string Text(object? v) => v switch
    {
        string s => s,
        long l => l.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
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
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or KeyNotFoundException or FormatException or OverflowException)
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
