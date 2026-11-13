namespace Aip;

/// <summary>Checks names, types, effects and completeness. Contracts are type-checked only; nothing is proved yet.</summary>
sealed partial class Checker(ProgramModel program, List<Diagnostic> diags)
{
    /// <summary>Whole-number types with an exact size. `Int` is the 64-bit signed one.</summary>
    static readonly Dictionary<string, (System.Numerics.BigInteger Min, System.Numerics.BigInteger Max)> SizedInts = new()
    {
        ["I8"] = (sbyte.MinValue, sbyte.MaxValue), ["I16"] = (short.MinValue, short.MaxValue), ["I32"] = (int.MinValue, int.MaxValue),
        ["U8"] = (byte.MinValue, byte.MaxValue), ["U16"] = (ushort.MinValue, ushort.MaxValue), ["U32"] = (uint.MinValue, uint.MaxValue),
        ["U64"] = (0, ulong.MaxValue),
    };

    static bool IsInt(string t) => t == "Int" || SizedInts.ContainsKey(t);
    static bool IsFloat(string t) => t is "F32" or "F64";
    static bool IsNum(string t) => IsInt(t) || IsFloat(t);
    /// <summary>Types a bare literal gets when nothing else is wanted.</summary>
    static bool IsDefaultLit(string t) => t is "Int" or "F64";
    static bool IsUnsigned(string t) => t.StartsWith('U') && SizedInts.ContainsKey(t);

    static readonly HashSet<string> KnownTypes = new(new[] { "Int", "F32", "F64", "Text", "Bool", "Unit", "Bytes", "TcpListener", "TcpConn", "UdpSocket" }.Concat(SizedInts.Keys));

    /// <summary>An integer literal (maybe negated) used where a sized integer is wanted takes that type if it fits.</summary>
    bool FitLiteral(Expr e, string want)
    {
        Expr lit = e is Unary { Op: "-", Operand: var inner } ? inner : e;
        if (want == "F32")
        {
            if (lit is not FloatLit f) return false;
            var d = e == lit ? f.Value : -f.Value;
            if (float.IsInfinity((float)d)) { Error(e.Line, "out-of-range", $"{d} does not fit in F32", "use F64 or a smaller number"); return true; }
            e.Type = want; lit.Type = want;
            return true;
        }
        if (!SizedInts.TryGetValue(want, out var range) || lit is not IntLit l) return false;
        System.Numerics.BigInteger v = e == lit ? l.Value : -(System.Numerics.BigInteger)l.Value;
        if (v < range.Min || v > range.Max)
        {
            Error(e.Line, "out-of-range", $"{v} does not fit in {want} ({range.Min} to {range.Max})", "use a smaller number or a bigger type");
            return true; // reported; do not add a type-mismatch on top
        }
        e.Type = want; lit.Type = want;
        return true;
    }

    /// <summary>Splits `Map<Text,List<Int>>` into its name and argument types; null for a plain type.</summary>
    static (string Name, List<string> Args)? Split(string t)
    {
        int lt = t.IndexOf('<');
        if (lt < 0 || !t.EndsWith('>')) return null;
        var args = new List<string>();
        int depth = 0, start = lt + 1;
        for (int i = start; i < t.Length - 1; i++)
        {
            if (t[i] == '<') depth++;
            else if (t[i] == '>') depth--;
            else if (t[i] == ',' && depth == 0) { args.Add(t[start..i]); start = i + 1; }
        }
        args.Add(t[start..^1]);
        return (t[..lt], args);
    }

    static bool IsList(string t) => Split(t) is { } s && GenericTypes.Arity.ContainsKey(s.Name); // a built-in container (List, Set, Map)
    static string Join(string name, IEnumerable<string> args) => $"{name}<{string.Join(",", args)}>";
    static bool IsVar(string t) => t.Length == 1 && char.IsAsciiLetterUpper(t[0]);

    /// <summary>Lossless widening: every value of `from` is a value of `to` (never Int to a float, never U64 to a signed type).</summary>
    static readonly Dictionary<string, string[]> WidensTo = new()
    {
        ["I8"] = new[] { "I16", "I32", "Int" },
        ["I16"] = new[] { "I32", "Int" },
        ["I32"] = new[] { "Int" },
        ["U8"] = new[] { "U16", "U32", "U64", "I16", "I32", "Int" },
        ["U16"] = new[] { "U32", "U64", "I32", "Int" },
        ["U32"] = new[] { "U64", "Int" },
        ["F32"] = new[] { "F64" },
    };
    static bool Widens(string from, string to) => WidensTo.TryGetValue(from, out var t) && t.Contains(to);

    /// <summary>Types match; `_` (an element type not known yet, e.g. in an empty list) matches anything.</summary>
    static bool Compat(string a, string b)
    {
        if (a == b || a == "_" || b == "_") return true;
        var x = Split(a); var y = Split(b);
        return x != null && y != null && x.Value.Name == y.Value.Name && x.Value.Args.Count == y.Value.Args.Count
            && x.Value.Args.Zip(y.Value.Args).All(p => Compat(p.First, p.Second));
    }

    /// <summary>The more specific of two compatible types.</summary>
    static string Merge(string a, string b)
    {
        if (a == "_") return b;
        var x = Split(a); var y = Split(b);
        if (x != null && y != null && x.Value.Args.Count == y.Value.Args.Count)
            return Join(x.Value.Name, x.Value.Args.Zip(y.Value.Args).Select(p => Merge(p.First, p.Second)));
        return a;
    }

    static readonly System.Text.RegularExpressions.Regex NativeLibName = new(@"^[A-Za-z0-9_][A-Za-z0-9_.\-]*$");
    static readonly System.Text.RegularExpressions.Regex NativeSymName = new(@"^[A-Za-z_][A-Za-z0-9_]*$");

    /// <summary>Rules for `native "lib" "symbol"` (docs/design/native-libraries-T-0956.md).</summary>
    void CheckNative(Fn fn)
    {
        if (fn.IsStd) { if (fn.NativeLib != null) Error(fn.NativeLine, "native-name", "the standard library cannot use `native`"); return; }
        if (fn.IsConst) return;
        if (fn.IsExtern && fn.NativeLib == null)
        {
            Error(fn.Line, "extern-no-native", $"`{fn.Name}` is extern but names no library", "add `native \"library\" \"symbol\"` to the header");
            return;
        }
        if (fn.NativeLib == null) return;
        if (!fn.IsExtern) Error(fn.NativeLine, "native-body", $"`{fn.Name}` has `native` but also a body", "end the file with `extern` instead of `body`");
        if (!NativeLibName.IsMatch(fn.NativeLib) || !NativeSymName.IsMatch(fn.NativeSymbol))
            Error(fn.NativeLine, "native-name", "the library name may use letters, digits, `_`, `.`, `-` (no path, no extension needed); the symbol is a C identifier",
                "for example `native \"sqlite3\" \"sqlite3_libversion\"`");
        if (!fn.Effects.Any(e => e.StartsWith("native.", StringComparison.Ordinal)))
            Error(fn.EffectsLine, "native-effect", $"`{fn.Name}` calls native code but declares no `native.*` effect", $"declare `effects native.{System.Text.RegularExpressions.Regex.Replace(fn.NativeLib, "[^A-Za-z0-9_]", "_")}`");
        foreach (var t in fn.Params.Select(p => p.Type.Name).Append(fn.Ret.Name))
            if (t is not ("Int" or "Bool" or "Text" or "Unit"))
                Error(fn.Line, "native-type", $"native functions only take and return Int, Bool, Text (and Unit as result); `{t}` is not allowed", "convert in an AIP function around the native one");
    }

    static bool Unify(string pattern, string actual, Dictionary<string, string> vars)
    {
        if (actual == "") return true;
        if (IsVar(pattern))
        {
            if (!vars.TryGetValue(pattern, out var bound)) { vars[pattern] = actual; return true; }
            if (!Compat(bound, actual)) return false;
            vars[pattern] = Merge(bound, actual);
            return true;
        }
        var p = Split(pattern);
        if (p != null)
        {
            if (actual == "_") return true;
            var a = Split(actual);
            if (a == null || a.Value.Name != p.Value.Name || a.Value.Args.Count != p.Value.Args.Count) return false;
            for (int i = 0; i < p.Value.Args.Count; i++)
                if (!Unify(p.Value.Args[i], a.Value.Args[i], vars)) return false;
            return true;
        }
        return Compat(pattern, actual) || Widens(actual, pattern);
    }

    static string Subst(string t, Dictionary<string, string> vars)
    {
        if (IsVar(t)) return vars.GetValueOrDefault(t, "_");
        var s = Split(t);
        return s == null ? t : Join(s.Value.Name, s.Value.Args.Select(a => Subst(a, vars)));
    }

    HashSet<string> _typeVars = new(); // type parameters in scope while a type definition is checked
    HashSet<string> _erased = new();   // match-bound names whose declared field type is a type parameter (the back end casts them)

    bool IsKnown(string t)
    {
        if (KnownTypes.Contains(t)) return true;
        if (t == "_") return _fn?.IsStd == true;
        if (IsVar(t)) return _fn != null || _typeVars.Contains(t);
        var s = Split(t);
        if (s == null) return program.Types.TryGetValue(t, out var plain) && plain.Params.Count == 0;
        if (!s.Value.Args.All(IsKnown)) return false;
        if (GenericTypes.Arity.TryGetValue(s.Value.Name, out var n)) return n == s.Value.Args.Count;
        return program.Types.TryGetValue(s.Value.Name, out var g) && g.Params.Count == s.Value.Args.Count;
    }

    /// <summary>The struct or variant a type names (`Point` or `Pair<Int,Text>`), with its type parameters bound to the arguments.</summary>
    (TypeDef Def, Dictionary<string, string> Vars)? UserType(string t)
    {
        var s = Split(t);
        if (!program.Types.TryGetValue(s?.Name ?? t, out var def)) return null;
        var vars = new Dictionary<string, string>();
        if (s != null)
            for (int i = 0; i < def.Params.Count && i < s.Value.Args.Count; i++) vars[def.Params[i]] = s.Value.Args[i];
        return (def, vars);
    }

    static void TypeVars(string t, HashSet<string> into)
    {
        if (IsVar(t)) { into.Add(t); return; }
        if (Split(t) is { } s) foreach (var a in s.Args) TypeVars(a, into);
    }

    static readonly HashSet<string> ReservedTypeNames = new() { "Fn", "Rt", "Program", "Const", "ConstCell" };
    HashSet<string> _patNames = new();

    Fn _fn = null!;
    HashSet<string> _effects = new();
    bool _inContract;
    bool _unknownCall;

    void Error(int line, string code, string message, string? fix = null) =>
        diags.Add(new Diagnostic(_fn.File, line, code, message, fix));

    public void Check()
    {
        CheckTypes();
        // Each function is checked on its own (it only reads other functions' headers), so this runs on
        // all cores; errors are collected per function and merged in name order, as a serial run would.
        var fns = program.Fns.Values.OrderBy(f => f.Name, StringComparer.Ordinal).ToArray();
        var found = new List<Diagnostic>[fns.Length];
        Parallel.For(0, fns.Length, i =>
        {
            var c = new Checker(program, found[i] = new List<Diagnostic>()) { _fn = fns[i] };
            c.CheckFn(fns[i]);
        });
        foreach (var list in found) diags.AddRange(list);
        CheckConstCycles();
        CheckMainAndReachability();
    }

    void CheckFn(Fn fn)
    {
        var fileName = Path.GetFileNameWithoutExtension(fn.File);
        var ext = Path.GetExtension(fn.File);
        var kind = fn.IsConst ? "constant" : "function";
        if (fileName != fn.Name)
            Error(fn.Line, "name-mismatch", $"file `{fileName}{ext}` declares `{fn.Name}`", $"rename the {kind} to `{fileName}` or the file to `{fn.Name}{ext}`");

        _patNames = new HashSet<string>();
        var env = new Dictionary<string, string>();
        foreach (var p in fn.Params)
        {
            CheckType(p.Type, allowUnit: false);
            if (IsConstName(p.Name)) Error(fn.Line, "duplicate-name", $"parameter `{p.Name}` has the name of a constant ({Path.GetFileName(program.Fns[p.Name].File)})", "choose a different name");
            else if (!env.TryAdd(p.Name, p.Type.Name)) Error(fn.Line, "duplicate-name", $"parameter `{p.Name}` is declared twice");
        }
        if (fn.IsConst && fn.Ret.Name == "Unit") Error(fn.Line, "type-mismatch", "a constant cannot be Unit", "give it a value type such as Int, Text, Bool or a struct");
        else CheckType(fn.Ret, allowUnit: true);
        if (!fn.IsStd)
        {
            var inParams = new HashSet<string>(); var inRet = new HashSet<string>();
            foreach (var p in fn.Params) TypeVars(p.Type.Name, inParams);
            TypeVars(fn.Ret.Name, inRet);
            foreach (var v in inRet.Except(inParams).OrderBy(v => v, StringComparer.Ordinal))
                Error(fn.Line, "bad-type-param", $"the result type uses `{v}`, but no parameter does, so `{v}` can never be known", $"use `{v}` in a parameter type, or give the result a concrete type");
        }

        _inContract = true;
        foreach (var e in fn.Requires) ExpectType(e, env, "Bool", "a `requires` condition");
        var withResult = new Dictionary<string, string>(env);
        if (fn.Ret.Name != "Unit") withResult["result"] = fn.Ret.Name;
        foreach (var e in fn.Ensures) ExpectType(e, withResult, "Bool", "an `ensures` condition");
        foreach (var e in fn.Examples) ExpectType(e, new Dictionary<string, string>(), "Bool", "an `example`");
        _inContract = false;

        CheckNative(fn);
        if (fn.IsExtern) return;

        _effects = new HashSet<string>();
        _unknownCall = false;
        var used = new HashSet<string>();
        var lets = new Dictionary<string, int>();
        for (int i = 0; i < fn.Body.Count; i++)
        {
            var s = fn.Body[i];
            bool last = i == fn.Body.Count - 1;
            var t = TypeOf(s.Value, env, used);
            if (s.Let != null)
            {
                if (t == "Unit") Error(s.Line, "type-mismatch", $"`{s.Let}` would hold Unit, which is not a value");
                if (IsConstName(s.Let)) Error(s.Line, "duplicate-name", $"`{s.Let}` is a constant ({Path.GetFileName(program.Fns[s.Let].File)})", "choose a different name");
                else if (_patNames.Contains(s.Let)) Error(s.Line, "duplicate-name", $"`{s.Let}` is already used by a match case", "choose a different name");
                else if (!env.TryAdd(s.Let, t)) Error(s.Line, "duplicate-name", $"`{s.Let}` is already defined", "choose a different name");
                else lets[s.Let] = s.Line;
                if (last) Error(s.Line, "missing-result", "the body ends with `let`", $"end the body with an expression of type {fn.Ret.Name}");
            }
            else if (last)
            {
                if (t != "" && !Compat(t, fn.Ret.Name) && !Widens(t, fn.Ret.Name) && !FitLiteral(s.Value, fn.Ret.Name))
                    Error(s.Line, "type-mismatch", fn.IsConst ? $"the value is {t} but constant `{fn.Name}` is {fn.Ret.Name}" : $"the body ends with {t} but `{fn.Name}` returns {fn.Ret.Name}");
            }
            else if (t != "" && t != "Unit")
                Error(s.Line, "unused-value", $"this {t} value is not used", "bind it with `let` or remove the line");
        }
        foreach (var (name, line) in lets)
            if (!used.Contains(name)) Error(line, "unused-let", $"`{name}` is never used", "remove the line");

        if (fn.IsConst)
        {
            if (_effects.Count > 0)
            {
                var culprit = fn.Calls.Where(c => program.Fns.TryGetValue(c, out var f) && f.Effects.Count > 0).OrderBy(c => c).First();
                Error(fn.EffectsLine, "const-not-pure", $"constant `{fn.Name}` calls `{culprit}`, which needs {program.Fns[culprit].EffectsText}",
                    "a constant must be pure; compute this value in a function that declares the effect");
            }
            return;
        }

        var missing = _effects.Except(fn.Effects).OrderBy(e => e).ToList();
        var unused = fn.Effects.Except(_effects).OrderBy(e => e).ToList();
        if (missing.Count > 0)
        {
            var needed = string.Join(", ", fn.Effects.Union(_effects).OrderBy(e => e));
            var culprit = fn.Calls.Where(c => program.Fns.TryGetValue(c, out var f) && f.Effects.Intersect(missing).Any()).OrderBy(c => c).First();
            Error(fn.EffectsLine, "effect-missing",
                $"`{fn.Name}` is declared {(fn.Effects.Count == 0 ? "pure" : $"with effects {fn.EffectsText}")} but calls `{culprit}`, which needs {string.Join(", ", missing)}",
                $"declare `effects {needed}` or remove the call");
        }
        if (unused.Count > 0 && !_unknownCall)
        {
            var rest = fn.Effects.Except(unused).ToList();
            Error(fn.EffectsLine, "effect-unused", $"`{fn.Name}` declares {string.Join(", ", unused)} but nothing in its body needs it",
                rest.Count == 0 ? "declare `pure`" : $"declare `effects {string.Join(", ", rest)}`");
        }
        CheckRestraints(fn);
    }

    void CheckTypes()
    {
        foreach (var def in program.Types.Values.OrderBy(d => d.Name, StringComparer.Ordinal))
        {
            void Err(int line, string code, string message, string? fix = null) => diags.Add(new Diagnostic(def.File, line, code, message, fix));
            var fileName = Path.GetFileNameWithoutExtension(def.File);
            if (fileName != def.Name)
                Err(def.Line, "name-mismatch", $"file `{Path.GetFileName(def.File)}` declares `{def.Name}`", $"rename the type to `{fileName}` or the file to `{def.Name}{Path.GetExtension(def.File)}`");
            _typeVars = new HashSet<string>(def.Params);
            var declared = new HashSet<string>();
            foreach (var tp in def.Params)
                if (!IsVar(tp)) Err(def.Line, "bad-type-param", $"`{tp}` is not a valid type parameter", "use a single capital letter such as T");
                else if (!declared.Add(tp)) Err(def.Line, "bad-type-param", $"type parameter `{tp}` is declared twice");
            if (def.IsVariant && (def.Name.Length < 2 || KnownTypes.Contains(def.Name) || ReservedTypeNames.Contains(def.Name)))
                Err(def.Line, "bad-type-name", $"`{def.Name}` is not a valid type or case name", "use two or more letters starting with a capital, not Fn, Rt, Program or a built-in type");
            foreach (var c in def.Cases)
            {
                if (c.Name.Length < 2 || !char.IsAsciiLetterUpper(c.Name[0]) || ReservedTypeNames.Contains(c.Name) || KnownTypes.Contains(c.Name))
                    Err(c.Line, "bad-type-name", $"`{c.Name}` is not a valid type or case name", "use two or more letters starting with a capital, not Fn, Rt, Program, Const, ConstCell or a built-in type");
                var seen = new HashSet<string>();
                foreach (var f in c.Fields)
                {
                    if (!seen.Add(f.Name)) Err(c.Line, "duplicate-name", $"field `{f.Name}` is declared twice in `{c.Name}`");
                    if (!IsKnown(f.Type.Name)) Err(c.Line, "unknown-type", $"unknown type `{f.Type.Name}` for field `{f.Name}`", "types: Int, I8, I16, I32, U8, U16, U32, U64, F32, F64, Text, Bool, List<T>, and your structs and variants");
                    else if (f.Type.Name == "Unit") Err(c.Line, "type-mismatch", $"field `{f.Name}` cannot be Unit");
                }
            }
            _typeVars = new HashSet<string>();
        }
    }

    void CheckType(TypeRef t, bool allowUnit)
    {
        if (!IsKnown(t.Name))
            Error(_fn.Line, "unknown-type", $"unknown type `{t.Name}`", "types supported so far: Int (64-bit signed), I8, I16, I32, U8, U16, U32, U64, F32, F64, Text, Bool, Unit, List<T>, Set<T>, Map<K,V>, structs and variants");
        else if (t.Name == "Unit" && !allowUnit)
            Error(_fn.Line, "type-mismatch", "a parameter cannot be Unit");
        else if (t.Range != "" && !IsInt(t.Name) && t.Name != "Text" && !IsList(t.Name))
            Error(_fn.Line, "bad-range", $"`{t.Name}` cannot have a range");
    }

    void ExpectType(Expr e, Dictionary<string, string> env, string want, string what)
    {
        var t = TypeOf(e, env, new HashSet<string>());
        if (t != "" && t != want) Error(e.Line, "type-mismatch", $"{what} must be {want}, found {t}");
    }

    void CheckMainAndReachability()
    {
        if (!program.Fns.TryGetValue("main", out var main) || main.IsStd)
        {
            diags.Add(new Diagnostic(Path.Combine(program.Dir, "main.fn"), 1, "no-main", "the program has no `main` function",
                "create main.fn starting with `fn main() -> Unit`"));
            return;
        }
        _fn = main;
        if (main.Params.Count > 0 || main.Ret.Name != "Unit")
            Error(main.Line, "bad-main", "`main` must take no parameters and return Unit", "declare `fn main() -> Unit`");

        var reached = new HashSet<string>();
        var todo = new Stack<string>();
        todo.Push("main");
        while (todo.Count > 0)
        {
            var name = todo.Pop();
            if (!reached.Add(name) || !program.Fns.TryGetValue(name, out var fn)) continue;
            foreach (var c in fn.Calls) todo.Push(c);
        }
        foreach (var fn in program.ProjectFns.Where(f => !reached.Contains(f.Name)))
        {
            _fn = fn;
            if (fn.IsConst) Error(fn.Line, "unused-const", $"constant `{fn.Name}` is never used by `main`", "use it or delete the file");
            else Error(fn.Line, "unused-function", $"`{fn.Name}` is never used by `main`", "call it or delete the file");
        }
    }


    static string? Suggest(string name, IEnumerable<string> candidates)
    {
        var best = candidates.Select(c => (c, d: Distance(name, c))).Where(x => x.d <= 2).OrderBy(x => x.d).ThenBy(x => x.c).FirstOrDefault();
        return best.c == null ? null : $"did you mean `{best.c}`?";
    }

    static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }
}
