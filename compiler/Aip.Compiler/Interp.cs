using System.Globalization;

namespace Aip;

/// <summary>A struct or variant value: which case, and its field values in declaration order.</summary>
sealed record Val(Case Case, object?[] Fields);

/// <summary>
/// Tree-walking evaluator for pure functions. It runs `example` lines and checks `requires`, `ensures` and
/// parameter/result ranges at every call. It follows the C# back end's rules (64-bit checked Int, truncating division).
/// </summary>
sealed class Interp(ProgramModel program)
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
    sealed class OrderCmp : IComparer<object>
    {
        public int Compare(object? x, object? y) => x is string s ? string.CompareOrdinal(s, (string)y!) : ((long)x!).CompareTo((long)y!);
    }
    static readonly IComparer<object> Order = new OrderCmp();

    static object? Extern(string name, List<object?> a)
    {
        List<object?> L(int i) => (List<object?>)a[i]!;
        Dictionary<object, object?> M(int i) => (Dictionary<object, object?>)a[i]!;
        SortedDictionary<object, object?> S(int i) => (SortedDictionary<object, object?>)a[i]!;
        SortedDictionary<object, object?> Copy(int i) => new(S(i), Order);
        try
        {
            switch (name)
            {
                case "noop": return null;
                case "textLength": return (long)((string)a[0]!).Length;
                case "textSlice":
                    {
                        var s = (string)a[0]!;
                        int st = (int)Math.Clamp((long)a[1]!, 0, s.Length), en = (int)Math.Clamp((long)a[2]!, st, s.Length);
                        return s.Substring(st, en - st);
                    }
                case "textCode": return (long)((string)a[0]!)[(int)(long)a[1]!];
                case "textFromCode": return ((char)(long)a[0]!).ToString();
                case "textToInt": return long.Parse((string)a[0]!, CultureInfo.InvariantCulture);
                case "listEmpty": return new List<object?>();
                case "listLength": return (long)L(0).Count;
                case "listGet": return L(0)[(int)(long)a[1]!];
                case "listAppend": return new List<object?>(L(0)) { a[1] };
                case "textFind": return (long)((string)a[0]!).IndexOf((string)a[1]!, StringComparison.Ordinal);
                case "textContains": return ((string)a[0]!).Contains((string)a[1]!, StringComparison.Ordinal);
                case "textStartsWith": return ((string)a[0]!).StartsWith((string)a[1]!, StringComparison.Ordinal);
                case "textEndsWith": return ((string)a[0]!).EndsWith((string)a[1]!, StringComparison.Ordinal);
                case "textTrim": return ((string)a[0]!).Trim();
                case "textReplace": return ((string)a[0]!).Replace((string)a[1]!, (string)a[2]!, StringComparison.Ordinal);
                case "textToUpper": return ((string)a[0]!).ToUpperInvariant();
                case "textToLower": return ((string)a[0]!).ToLowerInvariant();
                case "textCompare": return (long)Math.Sign(string.CompareOrdinal((string)a[0]!, (string)a[1]!));
                case "textFromInt": return ((long)a[0]!).ToString(CultureInfo.InvariantCulture);
                case "textRepeat": return string.Concat(Enumerable.Repeat((string)a[0]!, (int)(long)a[1]!));
                case "textSplit": return ((string)a[0]!).Split((string)a[1]!, StringSplitOptions.None).Select(x => (object?)x).ToList();
                case "textJoin": return string.Join((string)a[1]!, L(0).Cast<string>());
                case "intMin": return Math.Min((long)a[0]!, (long)a[1]!);
                case "intMax": return Math.Max((long)a[0]!, (long)a[1]!);
                case "intAbs": return Math.Abs((long)a[0]!);
                case "intSign": return (long)Math.Sign((long)a[0]!);
                case "intClamp": return Math.Clamp((long)a[0]!, (long)a[1]!, (long)a[2]!);
                case "intPow": return (long)System.Numerics.BigInteger.Pow((long)a[0]!, (int)(long)a[1]!);
                case "intGcd": return (long)System.Numerics.BigInteger.GreatestCommonDivisor((long)a[0]!, (long)a[1]!);
                case "intAnd": return (long)a[0]! & (long)a[1]!;
                case "intOr": return (long)a[0]! | (long)a[1]!;
                case "intXor": return (long)a[0]! ^ (long)a[1]!;
                case "intNot": return ~(long)a[0]!;
                case "intShiftLeft": return (long)a[0]! << (int)(long)a[1]!;
                case "intShiftRight": return (long)a[0]! >> (int)(long)a[1]!;
                case "listSet": { var r = new List<object?>(L(0)); r[(int)(long)a[1]!] = a[2]; return r; }
                case "listInsert": { var r = new List<object?>(L(0)); r.Insert((int)(long)a[1]!, a[2]); return r; }
                case "listRemoveAt": { var r = new List<object?>(L(0)); r.RemoveAt((int)(long)a[1]!); return r; }
                case "listConcat": return L(0).Concat(L(1)).ToList();
                case "listSlice":
                    {
                        int st = (int)Math.Clamp((long)a[1]!, 0, L(0).Count), en = (int)Math.Clamp((long)a[2]!, st, L(0).Count);
                        return L(0).GetRange(st, en - st);
                    }
                case "listReverse": { var r = new List<object?>(L(0)); r.Reverse(); return r; }
                case "listIndexOf": return (long)L(0).FindIndex(x => Equals(x, a[1]));
                case "listContains": return L(0).Any(x => Equals(x, a[1]));
                case "listRange": { var r = new List<object?>(); for (long i = (long)a[0]!; i < (long)a[1]!; i++) r.Add(i); return r; }
                case "queueEmpty" or "priorityQueueEmpty": return new List<object?>();
                case "queueSize" or "priorityQueueSize": return (long)L(0).Count;
                case "queuePush": return new List<object?>(L(0)) { a[1] };
                case "queueFront": return L(0).Count > 0 ? L(0)[0] : throw new KeyNotFoundException("queueFront: the queue is empty");
                case "priorityQueueFront": return L(0).Count > 0 ? ((object?[])L(0)[0]!)[1] : throw new KeyNotFoundException("priorityQueueFront: the queue is empty");
                case "queuePop" or "priorityQueuePop": return L(0).Count > 0 ? L(0).GetRange(1, L(0).Count - 1) : throw new KeyNotFoundException(name + ": the queue is empty");
                case "priorityQueuePush":
                    {
                        var l = L(0); int i = l.Count; long p = (long)a[2]!;
                        while (i > 0 && (long)((object?[])l[i - 1]!)[0]! > p) i--;
                        var r = new List<object?>(l); r.Insert(i, new object?[] { p, a[1] }); return r;
                    }
                case "sortedMapEmpty" or "sortedSetEmpty": return new SortedDictionary<object, object?>(Order);
                case "sortedMapSize" or "sortedSetSize": return (long)S(0).Count;
                case "sortedMapHas" or "sortedSetHas": return S(0).ContainsKey(a[1]!);
                case "sortedMapGet": return S(0).TryGetValue(a[1]!, out var sv) ? sv : throw new KeyNotFoundException("sortedMapGet: key not in the map");
                case "sortedMapSet": { var r = Copy(0); r[a[1]!] = a[2]; return r; }
                case "sortedSetAdd": { var r = Copy(0); r[a[1]!] = true; return r; }
                case "sortedMapRemove" or "sortedSetRemove": { var r = Copy(0); r.Remove(a[1]!); return r; }
                case "sortedMapKeys" or "sortedSetItems": return S(0).Keys.Select(k => (object?)k).ToList();
                case "mapEmpty" or "setEmpty": return new Dictionary<object, object?>();
                case "mapSize" or "setSize": return (long)M(0).Count;
                case "mapHas" or "setHas": return M(0).ContainsKey(a[1]!);
                case "mapGet": return M(0).TryGetValue(a[1]!, out var v) ? v : throw new KeyNotFoundException("mapGet: key not in the map");
                case "mapSet": return new Dictionary<object, object?>(M(0)) { [a[1]!] = a[2] };
                case "setAdd": return new Dictionary<object, object?>(M(0)) { [a[1]!] = true };
                case "mapRemove" or "setRemove": return M(0).Where(p => !p.Key.Equals(a[1])).ToDictionary(p => p.Key, p => p.Value);
                case "mapKeys" or "setItems": return M(0).Keys.Select(k => (object?)k).ToList();
            }
        }
        catch (Exception ex) when (ex is ArgumentException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new Failed("extern-failed", $"`{name}`: {ex.Message}");
        }
        throw new Unsupported($"the standard function `{name}` is not available in `aip test`");
    }

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
