namespace Aip;

/// <summary>Checks names, types, effects and completeness. Contracts are type-checked only; nothing is proved yet.</summary>
sealed class Checker(ProgramModel program, List<Diagnostic> diags)
{
    static readonly HashSet<string> KnownTypes = new() { "Int", "Text", "Bool", "Unit" };

    static readonly Dictionary<string, int> Generics = new() { ["List"] = 1, ["Set"] = 1, ["Map"] = 2 };

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

    static bool IsList(string t) => Split(t) != null; // any generic container (List, Set, Map)
    static string Join(string name, IEnumerable<string> args) => $"{name}<{string.Join(",", args)}>";
    static bool IsVar(string t) => t.Length == 1 && char.IsAsciiLetterUpper(t[0]);

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
        return Compat(pattern, actual);
    }

    static string Subst(string t, Dictionary<string, string> vars)
    {
        if (IsVar(t)) return vars.GetValueOrDefault(t, "_");
        var s = Split(t);
        return s == null ? t : Join(s.Value.Name, s.Value.Args.Select(a => Subst(a, vars)));
    }

    bool IsKnown(string t)
    {
        if (KnownTypes.Contains(t) || (_fn.IsStd && (t == "_" || IsVar(t)))) return true;
        var s = Split(t);
        return s != null && Generics.TryGetValue(s.Value.Name, out var n) && n == s.Value.Args.Count && s.Value.Args.All(IsKnown);
    }

    Fn _fn = null!;
    HashSet<string> _effects = new();
    bool _inContract;
    bool _unknownCall;

    void Error(int line, string code, string message, string? fix = null) =>
        diags.Add(new Diagnostic(_fn.File, line, code, message, fix));

    public void Check()
    {
        foreach (var fn in program.Fns.Values.OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            _fn = fn;
            CheckFn(fn);
        }
        CheckMainAndReachability();
    }

    void CheckFn(Fn fn)
    {
        var fileName = Path.GetFileNameWithoutExtension(fn.File);
        if (fileName != fn.Name)
            Error(fn.Line, "name-mismatch", $"file `{fileName}.fn` declares `{fn.Name}`", $"rename the function to `{fileName}` or the file to `{fn.Name}.fn`");

        var env = new Dictionary<string, string>();
        foreach (var p in fn.Params)
        {
            CheckType(p.Type, allowUnit: false);
            if (!env.TryAdd(p.Name, p.Type.Name)) Error(fn.Line, "duplicate-name", $"parameter `{p.Name}` is declared twice");
        }
        CheckType(fn.Ret, allowUnit: true);

        _inContract = true;
        foreach (var e in fn.Requires) ExpectType(e, env, "Bool", "a `requires` condition");
        var withResult = new Dictionary<string, string>(env);
        if (fn.Ret.Name != "Unit") withResult["result"] = fn.Ret.Name;
        foreach (var e in fn.Ensures) ExpectType(e, withResult, "Bool", "an `ensures` condition");
        foreach (var e in fn.Examples) ExpectType(e, new Dictionary<string, string>(), "Bool", "an `example`");
        _inContract = false;

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
                if (!env.TryAdd(s.Let, t)) Error(s.Line, "duplicate-name", $"`{s.Let}` is already defined", "choose a different name");
                else lets[s.Let] = s.Line;
                if (last) Error(s.Line, "missing-result", "the body ends with `let`", $"end the body with an expression of type {fn.Ret.Name}");
            }
            else if (last)
            {
                if (t != "" && !Compat(t, fn.Ret.Name))
                    Error(s.Line, "type-mismatch", $"the body ends with {t} but `{fn.Name}` returns {fn.Ret.Name}");
            }
            else if (t != "" && t != "Unit")
                Error(s.Line, "unused-value", $"this {t} value is not used", "bind it with `let` or remove the line");
        }
        foreach (var (name, line) in lets)
            if (!used.Contains(name)) Error(line, "unused-let", $"`{name}` is never used", "remove the line");

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
    }

    void CheckType(TypeRef t, bool allowUnit)
    {
        if (!IsKnown(t.Name))
            Error(_fn.Line, "unknown-type", $"unknown type `{t.Name}`", "types supported so far: Int, Text, Bool, Unit, List<T>, Set<T>, Map<K,V>");
        else if (t.Name == "Unit" && !allowUnit)
            Error(_fn.Line, "type-mismatch", "a parameter cannot be Unit");
        else if (t.Range != "" && t.Name != "Int" && t.Name != "Text" && !IsList(t.Name))
            Error(_fn.Line, "bad-range", $"`{t.Name}` cannot have a range");
    }

    void ExpectType(Expr e, Dictionary<string, string> env, string want, string what)
    {
        var t = TypeOf(e, env, new HashSet<string>());
        if (t != "" && t != want) Error(e.Line, "type-mismatch", $"{what} must be {want}, found {t}");
    }

    /// <summary>Returns the type name, or "" after reporting an error.</summary>
    string TypeOf(Expr e, Dictionary<string, string> env, HashSet<string> used)
    {
        e.Type = e switch
        {
            IntLit => "Int",
            BoolLit => "Bool",
            TextLit t => TextType(t, env, used),
            NameRef n => NameType(n, env, used),
            Call c => CallType(c, env, used),
            Unary u => UnaryType(u, env, used),
            Binary b => BinaryType(b, env, used),
            IfExpr i => IfType(i, env, used),
            _ => "",
        };
        return e.Type;
    }

    string TextType(TextLit t, Dictionary<string, string> env, HashSet<string> used)
    {
        foreach (var part in t.Parts.OfType<Expr>())
        {
            var pt = TypeOf(part, env, used);
            if (pt == "Unit") Error(t.Line, "type-mismatch", "a Unit value cannot be put into a text");
            else if (IsList(pt)) Error(t.Line, "type-mismatch", "a List, Set or Map cannot be put into a text", "build the text from the items");
        }
        return "Text";
    }

    string NameType(NameRef n, Dictionary<string, string> env, HashSet<string> used)
    {
        if (env.TryGetValue(n.Name, out var t)) { used.Add(n.Name); return t; }
        if (program.Fns.ContainsKey(n.Name))
            Error(n.Line, "unknown-name", $"`{n.Name}` is a function, not a value", $"call it: `{n.Name}(...)`");
        else
            Error(n.Line, "unknown-name", $"`{n.Name}` is not defined", Suggest(n.Name, env.Keys));
        return "";
    }

    string CallType(Call c, Dictionary<string, string> env, HashSet<string> used)
    {
        var argTypes = c.Args.Select(a => TypeOf(a, env, used)).ToList();
        if (!program.Fns.TryGetValue(c.Name, out var target))
        {
            Error(c.Line, "unknown-name", $"there is no function `{c.Name}`", Suggest(c.Name, program.Fns.Keys));
            _unknownCall = true;
            return "";
        }
        _fn.Calls.Add(c.Name);
        if (_inContract)
        {
            if (target.Effects.Count > 0)
                Error(c.Line, "contract-not-pure", $"`{c.Name}` has effects ({target.EffectsText}) and cannot be used in a contract or example");
        }
        else _effects.UnionWith(target.Effects);

        if (argTypes.Count != target.Params.Count)
            Error(c.Line, "wrong-arguments", $"`{c.Name}` takes {target.Params.Count} argument(s), found {argTypes.Count}", $"signature: {target.Signature}");
        else
        {
            var vars = new Dictionary<string, string>();
            for (int i = 0; i < argTypes.Count; i++)
                if (!Unify(target.Params[i].Type.Name, argTypes[i], vars))
                    Error(c.Line, "type-mismatch", $"argument `{target.Params[i].Name}` of `{c.Name}` must be {Subst(target.Params[i].Type.Name, vars)}, found {argTypes[i]}", $"signature: {target.Signature}");
            return Subst(target.Ret.Name, vars);
        }
        return Subst(target.Ret.Name, new Dictionary<string, string>());
    }

    string UnaryType(Unary u, Dictionary<string, string> env, HashSet<string> used)
    {
        var want = u.Op == "-" ? "Int" : "Bool";
        var t = TypeOf(u.Operand, env, used);
        if (t != "" && t != want) Error(u.Line, "type-mismatch", $"`{u.Op}` needs {want}, found {t}");
        return want;
    }

    string BinaryType(Binary b, Dictionary<string, string> env, HashSet<string> used)
    {
        var l = TypeOf(b.Left, env, used);
        var r = TypeOf(b.Right, env, used);
        bool known = l != "" && r != "";
        switch (b.Op)
        {
            case "+" or "-" or "*" or "/" or "%":
                if (known && (l != "Int" || r != "Int"))
                    Error(b.Line, "type-mismatch", $"`{b.Op}` needs Int on both sides, found {l} and {r}",
                        b.Op == "+" && (l == "Text" || r == "Text") ? "to join texts, write one text with `{...}` parts" : null);
                return "Int";
            case "<" or "<=" or ">" or ">=":
                if (known && (l != "Int" || r != "Int")) Error(b.Line, "type-mismatch", $"`{b.Op}` needs Int on both sides, found {l} and {r}");
                return "Bool";
            case "==" or "!=":
                if (known && (!Compat(l, r) || l == "Unit" || IsList(l)))
                    Error(b.Line, "type-mismatch", $"`{b.Op}` compares {l} with {r}", IsList(l) ? "lists, sets and maps cannot be compared; compare their sizes or items" : null);
                return "Bool";
            default: // and, or, =>
                if (known && (l != "Bool" || r != "Bool")) Error(b.Line, "type-mismatch", $"`{b.Op}` needs Bool on both sides, found {l} and {r}");
                return "Bool";
        }
    }

    string IfType(IfExpr i, Dictionary<string, string> env, HashSet<string> used)
    {
        var c = TypeOf(i.Cond, env, used);
        if (c != "" && c != "Bool") Error(i.Line, "type-mismatch", $"the `if` condition must be Bool, found {c}");
        var a = TypeOf(i.Then, env, used);
        var b = TypeOf(i.Else, env, used);
        if (a != "" && b != "" && !Compat(a, b)) Error(i.Line, "type-mismatch", $"`then` gives {a} but `else` gives {b}");
        return a == "" || b == "" ? a + b : Merge(a, b);
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
            Error(fn.Line, "unused-function", $"`{fn.Name}` is never used by `main`", "call it or delete the file");
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
