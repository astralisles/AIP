using System.Globalization;
using System.Numerics;

namespace Aip;

/// <summary>A struct or variant value: which case, and its field values in declaration order.</summary>
sealed record Val(Case Case, object?[] Fields);

/// <summary>
/// Tree-walking evaluator for pure functions. It runs `example` lines and checks `requires`, `ensures` and
/// parameter/result ranges at every call. It follows the C# back end's rules (checked sized integers, truncating division, IEEE floats).
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
                    if (b.Op is "/" or "%" && y.IsZero) throw new Failed("arithmetic", $"`{x} {b.Op} {y}`: division by zero");
                    BigInteger z = b.Op switch { "+" => x + y, "-" => x - y, "*" => x * y, "/" => BigInteger.Divide(x, y), "%" => BigInteger.Remainder(x, y), _ => throw new Unsupported($"operator {b.Op}") };
                    return Fit(z, b.Type == "" ? "Int" : b.Type, $"`{x} {b.Op} {y}`");
                }
        }
        throw new Unsupported("expression " + e.GetType().Name);
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
    sealed class OrderCmp : IComparer<object>
    {
        public int Compare(object? x, object? y) => x is string s ? string.CompareOrdinal(s, (string)y!) : ((long)x!).CompareTo((long)y!);
    }
    static readonly IComparer<object> Order = new OrderCmp();

    static object? Extern(string name, List<object?> a)
    {
        List<object?> L(int i) => (List<object?>)a[i]!;
        byte[] IntBytes(List<object?> l) => l.Select(x => { var v = (long)x!; if (v < 0 || v > 255) throw new InvalidOperationException("byte out of range 0..255: " + v); return (byte)v; }).ToArray();
        Dictionary<object, object?> M(int i) => (Dictionary<object, object?>)a[i]!;
        SortedDictionary<object, object?> S(int i) => (SortedDictionary<object, object?>)a[i]!;
        SortedDictionary<object, object?> Copy(int i) => new(S(i), Order);
        try
        {
            switch (name)
            {
                case "noop": return null;
                case "bytesEmpty": return Array.Empty<byte>();
                case "bytesEncode": return new System.Text.UTF8Encoding(false).GetBytes((string)a[0]!);
                case "bytesDecode": return new System.Text.UTF8Encoding(false).GetString((byte[])a[0]!);
                case "bytesLength": return (long)((byte[])a[0]!).Length;
                case "bytesGet": return (long)((byte[])a[0]!)[(int)(long)a[1]!];
                case "bytesSlice":
                    {
                        var d = (byte[])a[0]!;
                        int st = (int)Math.Clamp((long)a[1]!, 0, d.Length), en = (int)Math.Clamp((long)a[2]!, st, d.Length);
                        return d[st..en];
                    }
                case "bytesJoin": return ((byte[])a[0]!).Concat((byte[])a[1]!).ToArray();
                case "bytesFromList": return L(0).Select(x => checked((byte)(long)x!)).ToArray();
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
                case "numToI8" or "numToI16" or "numToI32" or "numToU8" or "numToU16" or "numToU32" or "numToU64":
                    return Fit(Big(a[0]!), name[5..], $"`{name}`");
                case "numFromI8" or "numFromI16" or "numFromI32" or "numFromU8" or "numFromU16" or "numFromU32" or "numFromU64":
                    return Fit(Big(a[0]!), "Int", $"`{name}`");
                case "numToF32": return (float)(long)a[0]!;
                case "numToF64": return (double)(long)a[0]!;
                case "numFromF32" or "numFromF64":
                    {
                        double d = ToDouble(a[0]!);
                        if (double.IsNaN(d) || d >= 9223372036854775808.0 || d < -9223372036854775808.0) throw new OverflowException($"{d} does not fit in Int");
                        return (long)d;
                    }
                case "numF32ToF64": return (double)(float)a[0]!;
                case "numF64ToF32": return (float)(double)a[0]!;
                case "bitAnd" or "bitOr" or "bitXor" or "bitNot" or "bitShl" or "bitShr":
                    {
                        string ty = a[0] switch { long => "Int", sbyte => "I8", short => "I16", int => "I32", byte => "U8", ushort => "U16", uint => "U32", _ => "U64" };
                        var (_, max) = Limits[ty];
                        int width = ty switch { "Int" or "U64" => 64, "I32" or "U32" => 32, "I16" or "U16" => 16, _ => 8 };
                        BigInteger mod = BigInteger.One << width, x = Big(a[0]!);
                        BigInteger Wrap(BigInteger v) { v = ((v % mod) + mod) % mod; return v > max ? v - mod : v; }
                        BigInteger res;
                        if (name is "bitShl" or "bitShr")
                        {
                            long n = (long)Big(a[1]!);
                            if (n < 0 || n >= width) throw new InvalidOperationException($"shift count {n} must be 0 to {width - 1}");
                            res = name == "bitShl" ? Wrap(x << (int)n) : x >> (int)n;
                        }
                        else if (name == "bitNot") res = Wrap(-x - 1);
                        else
                        {
                            BigInteger y = Big(a[1]!), ux = (x + mod) % mod, uy = (y + mod) % mod;
                            res = Wrap(name == "bitAnd" ? ux & uy : name == "bitOr" ? ux | uy : ux ^ uy);
                        }
                        return Fit(res, ty, name);
                    }
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
                case "hashSha256": return System.Security.Cryptography.SHA256.HashData(IntBytes(L(0))).Select(x => (object?)(long)x).ToList();
                case "hashHmacSha256": return System.Security.Cryptography.HMACSHA256.HashData(IntBytes(L(0)), IntBytes(L(1))).Select(x => (object?)(long)x).ToList();
                case "bytesFromText": return System.Text.Encoding.UTF8.GetBytes((string)a[0]!).Select(x => (object?)(long)x).ToList();
                case "bytesToText": return System.Text.Encoding.UTF8.GetString(IntBytes(L(0)));
                case "bytesToHex": return Convert.ToHexString(IntBytes(L(0))).ToLowerInvariant();
                case "bytesFromHex":
                    {
                        var h = (string)a[0]!;
                        if (h.Length % 2 != 0) throw new InvalidOperationException("hex text must have an even number of digits");
                        return Convert.FromHexString(h).Select(x => (object?)(long)x).ToList();
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
