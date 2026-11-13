namespace Aip;

/// <summary>Checks names, types, effects and completeness. Contracts are type-checked only; nothing is proved yet.</summary>
sealed class Checker(ProgramModel program, List<Diagnostic> diags)
{
    static readonly HashSet<string> KnownTypes = new() { "Int", "Text", "Bool", "Unit" };

    static readonly Dictionary<string, int> Generics = new() { ["List"] = 1, ["Set"] = 1, ["Map"] = 2, ["SortedSet"] = 1, ["SortedMap"] = 2, ["Queue"] = 1, ["PriorityQueue"] = 1 };

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

    static bool IsList(string t) => Split(t) is { } s && Generics.ContainsKey(s.Name); // a built-in container (List, Set, Map)
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
        return Compat(pattern, actual);
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
        if (Generics.TryGetValue(s.Value.Name, out var n)) return n == s.Value.Args.Count;
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
                if (t != "" && !Compat(t, fn.Ret.Name))
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
                    if (!IsKnown(f.Type.Name)) Err(c.Line, "unknown-type", $"unknown type `{f.Type.Name}` for field `{f.Name}`", "types: Int, Text, Bool, List<T>, and your structs and variants");
                    else if (f.Type.Name == "Unit") Err(c.Line, "type-mismatch", $"field `{f.Name}` cannot be Unit");
                }
            }
            _typeVars = new HashSet<string>();
        }
    }

    void CheckType(TypeRef t, bool allowUnit)
    {
        if (!IsKnown(t.Name))
            Error(_fn.Line, "unknown-type", $"unknown type `{t.Name}`", "types supported so far: Int, Text, Bool, Unit, List<T>, Set<T>, Map<K,V>, structs and variants");
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
            Field f => FieldType(f, env, used),
            MatchExpr m => MatchType(m, env, used),
            Unary u => UnaryType(u, env, used),
            Binary b => BinaryType(b, env, used),
            IfExpr i => IfType(i, env, used),
            BlockExpr k => BlockType(k, env, used),
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
            else if (IsVar(pt)) Error(t.Line, "type-mismatch", $"a value of type parameter `{pt}` cannot be put into a text", "a generic function can only store, pass and return such values");
            else if (UserType(pt) != null) Error(t.Line, "type-mismatch", $"a {pt} cannot be put into a text", "put its fields into the text instead");
        }
        return "Text";
    }

    string NameType(NameRef n, Dictionary<string, string> env, HashSet<string> used)
    {
        if (env.TryGetValue(n.Name, out var t)) { used.Add(n.Name); n.Erased = _erased.Contains(n.Name); return t; }
        if (program.Fns.TryGetValue(n.Name, out var k) && k.IsConst)
        {
            // A constant reads like a value; it counts as a call so reachability and cycle checks see it.
            n.Const = true;
            _fn.Calls.Add(n.Name);
            return k.Ret.Name;
        }
        if (program.Ctors.ContainsKey(n.Name))
            Error(n.Line, "unknown-name", $"`{n.Name}` is a constructor, not a value", $"call it: `{n.Name}(...)`");
        else if (program.Fns.ContainsKey(n.Name))
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
            if (program.Ctors.TryGetValue(c.Name, out var ctor)) return CtorType(c, ctor.Def, ctor.Case, argTypes);
            Error(c.Line, "unknown-name", $"there is no function `{c.Name}`", Suggest(c.Name, program.Fns.Keys));
            _unknownCall = true;
            return "";
        }
        if (target.IsConst)
        {
            Error(c.Line, "type-mismatch", $"`{c.Name}` is a constant, not a function", $"write `{c.Name}` without parentheses");
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
            c.Erased = !target.IsStd && IsVar(target.Ret.Name);
            for (int i = 0; i < argTypes.Count; i++)
                if (!Unify(target.Params[i].Type.Name, argTypes[i], vars))
                    Error(c.Line, "type-mismatch", $"argument `{target.Params[i].Name}` of `{c.Name}` must be {Subst(target.Params[i].Type.Name, vars)}, found {argTypes[i]}", $"signature: {target.Signature}");
            return Subst(target.Ret.Name, vars);
        }
        return Subst(target.Ret.Name, new Dictionary<string, string>());
    }

    string CtorType(Call c, TypeDef def, Case cs, List<string> argTypes)
    {
        var vars = new Dictionary<string, string>();
        c.Ctor = def.IsVariant ? $"{def.Name}.{cs.Name}" : def.Name;
        var sig = $"{cs.Name}({string.Join(", ", cs.Fields.Select(f => $"{f.Name} {f.Type.Name}"))})";
        if (argTypes.Count != cs.Fields.Count)
            Error(c.Line, "wrong-arguments", $"`{c.Name}` takes {cs.Fields.Count} argument(s), found {argTypes.Count}", $"signature: {sig}");
        else
            for (int i = 0; i < argTypes.Count; i++)
                if (!Unify(cs.Fields[i].Type.Name, argTypes[i], vars))
                    Error(c.Line, "type-mismatch", $"field `{cs.Fields[i].Name}` of `{c.Name}` must be {Subst(cs.Fields[i].Type.Name, vars)}, found {argTypes[i]}", $"signature: {sig}");
        return def.Params.Count == 0 ? def.Name : Join(def.Name, def.Params.Select(p => vars.GetValueOrDefault(p, "_")));
    }

    string FieldType(Field f, Dictionary<string, string> env, HashSet<string> used)
    {
        var t = TypeOf(f.Target, env, used);
        if (t == "") return "";
        if (UserType(t) is not { } user)
        {
            Error(f.Line, "type-mismatch", $"{t} has no fields");
            return "";
        }
        var def = user.Def;
        if (def.IsVariant)
        {
            Error(f.Line, "type-mismatch", $"`{def.Name}` is a variant, so `.{f.Name}` cannot read it", "use `match` to look inside a variant");
            return "";
        }
        var field = def.Cases[0].Fields.FirstOrDefault(p => p.Name == f.Name);
        if (field == null)
        {
            Error(f.Line, "unknown-field", $"`{def.Name}` has no field `{f.Name}`", Suggest(f.Name, def.Cases[0].Fields.Select(p => p.Name)) ?? $"fields: {string.Join(", ", def.Cases[0].Fields.Select(p => p.Name))}");
            return "";
        }
        f.Erased = IsVar(field.Type.Name);
        return Subst(field.Type.Name, user.Vars);
    }

    string MatchType(MatchExpr m, Dictionary<string, string> env, HashSet<string> used)
    {
        var st = TypeOf(m.Subject, env, used);
        TypeDef? def = null;
        var tv = new Dictionary<string, string>();
        if (st != "")
        {
            var u = UserType(st);
            if (u == null || !u.Value.Def.IsVariant)
                Error(m.Line, "type-mismatch", $"`match` needs a variant, found {st}", u != null ? "read the fields of a struct with `.name`" : null);
            else { def = u.Value.Def; tv = u.Value.Vars; }
        }
        if (m.Arms.Count == 0) Error(m.Line, "match-empty", "this match has no cases", "add one line per case, indented by four spaces: `Case(a, b) => result`");
        string? result = null;
        var handled = new HashSet<string>();
        foreach (var arm in m.Arms)
        {
            var armEnv = new Dictionary<string, string>(env);
            Case? cs = null;
            if (def != null)
            {
                cs = def.Cases.FirstOrDefault(c => c.Name == arm.Case);
                if (cs == null) Error(arm.Line, "unknown-case", $"`{arm.Case}` is not a case of `{def.Name}`", Suggest(arm.Case, def.Cases.Select(c => c.Name)) ?? $"cases: {string.Join(", ", def.Cases.Select(c => c.Name))}");
                else
                {
                    arm.Path = $"{def.Name}.{cs.Name}";
                    if (!handled.Add(cs.Name)) Error(arm.Line, "duplicate-case", $"`{cs.Name}` is handled twice", "remove one of the lines");
                    if (arm.Binds.Count != cs.Fields.Count)
                        Error(arm.Line, "wrong-arguments", $"`{cs.Name}` has {cs.Fields.Count} field(s) but this line names {arm.Binds.Count}", $"write `{cs.Name}({string.Join(", ", cs.Fields.Select(f => f.Name))})`");
                }
            }
            var restore = new List<(string Name, bool Was)>();
            for (int i = 0; i < arm.Binds.Count; i++)
            {
                var type = cs != null && i < cs.Fields.Count ? Subst(cs.Fields[i].Type.Name, tv) : "";
                restore.Add((arm.Binds[i], _erased.Contains(arm.Binds[i])));
                if (cs != null && i < cs.Fields.Count && IsVar(cs.Fields[i].Type.Name)) _erased.Add(arm.Binds[i]); else _erased.Remove(arm.Binds[i]);
                if (IsConstName(arm.Binds[i])) Error(arm.Line, "duplicate-name", $"`{arm.Binds[i]}` is a constant ({Path.GetFileName(program.Fns[arm.Binds[i]].File)})", "choose a different name");
                else if (!armEnv.TryAdd(arm.Binds[i], type)) Error(arm.Line, "duplicate-name", $"`{arm.Binds[i]}` is already defined", "choose a different name");
                _patNames.Add(arm.Binds[i]);
            }
            var bt = TypeOf(arm.Body, armEnv, used);
            for (int i = restore.Count - 1; i >= 0; i--) if (restore[i].Was) _erased.Add(restore[i].Name); else _erased.Remove(restore[i].Name);
            if (bt == "") continue;
            if (result == null) result = bt;
            else if (!Compat(result, bt)) Error(arm.Line, "type-mismatch", $"this case gives {bt} but earlier cases give {result}");
            else result = Merge(result, bt);
        }
        if (def != null)
        {
            var missing = def.Cases.Where(c => !handled.Contains(c.Name)).Select(c => c.Name).ToList();
            if (missing.Count > 0 && m.Arms.Count > 0)
                Error(m.Line, "match-incomplete", $"this match does not handle: {string.Join(", ", missing)}", "add a line for each missing case");
        }
        return result ?? "";
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
                if (known && (!Compat(l, r) || l == "Unit" || IsList(l) || IsVar(l) || UserType(l) != null))
                    Error(b.Line, "type-mismatch", $"`{b.Op}` compares {l} with {r}",
                        IsList(l) ? "lists, sets and maps cannot be compared; compare their sizes or items" : IsVar(l) ? "a value of a type parameter cannot be compared" : UserType(l) != null ? "structs and variants cannot be compared; compare their fields, or use `match`" : null);
                return "Bool";
            default: // and, or, =>
                if (known && (l != "Bool" || r != "Bool")) Error(b.Line, "type-mismatch", $"`{b.Op}` needs Bool on both sides, found {l} and {r}");
                return "Bool";
        }
    }

    string BlockType(BlockExpr k, Dictionary<string, string> env, HashSet<string> used)
    {
        foreach (var s in k.Stmts)
        {
            var t = TypeOf(s, env, used);
            if (t != "" && t != "Unit") Error(s.Line, "unused-value", $"this {t} value is not used", "a branch of an `if` block holds Unit statements; remove the line");
        }
        return "Unit";
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
            if (fn.IsConst) Error(fn.Line, "unused-const", $"constant `{fn.Name}` is never used by `main`", "use it or delete the file");
            else Error(fn.Line, "unused-function", $"`{fn.Name}` is never used by `main`", "call it or delete the file");
        }
    }

    bool IsConstName(string name) => program.Fns.TryGetValue(name, out var f) && f.IsConst;

    /// <summary>A constant's value may not depend on itself, directly or through functions and other constants.</summary>
    void CheckConstCycles()
    {
        var reported = new HashSet<string>();
        foreach (var k in program.Fns.Values.Where(f => f.IsConst).OrderBy(f => f.Name, StringComparer.Ordinal))
        {
            if (reported.Contains(k.Name)) continue; // one error per cycle, not one per constant in it
            // Breadth-first from the constant's direct uses; keep the path to report it.
            var from = new Dictionary<string, string>();
            var queue = new Queue<string>();
            foreach (var c in k.Calls.OrderBy(c => c, StringComparer.Ordinal)) if (from.TryAdd(c, k.Name)) queue.Enqueue(c);
            while (queue.Count > 0)
            {
                var name = queue.Dequeue();
                if (name == k.Name)
                {
                    var path = new List<string> { k.Name };
                    for (var at = from[k.Name]; at != k.Name; at = from[at]) path.Insert(0, at);
                    path.Insert(0, k.Name);
                    reported.UnionWith(path);
                    _fn = k;
                    Error(k.Line, "const-cycle", $"constant `{k.Name}` depends on itself: {string.Join(" -> ", path)}", "compute one of the values without the other");
                    break;
                }
                if (!program.Fns.TryGetValue(name, out var f)) continue;
                foreach (var c in f.Calls.OrderBy(c => c, StringComparer.Ordinal)) if (from.TryAdd(c, name)) queue.Enqueue(c);
            }
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
