namespace Aip;

/// <summary>Checker: the type of each kind of expression.</summary>
sealed partial class Checker
{
    /// <summary>Returns the type name, or "" after reporting an error.</summary>
    string TypeOf(Expr e, Dictionary<string, string> env, HashSet<string> used)
    {
        e.Type = e switch
        {
            IntLit { Value: > long.MaxValue } => "U64", // too big for Int, so it can only be a U64
            IntLit => "Int",
            FloatLit => "F64",
            BoolLit => "Bool",
            TextLit t => TextType(t, env, used),
            NameRef n => NameType(n, env, used),
            FnRef r => FnRefType(r),
            Call c => CallType(c, env, used),
            Field f => FieldType(f, env, used),
            Assign a => AssignType(a, env, used),
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
            else if (IsFnType(pt)) Error(t.Line, "type-mismatch", "a function value cannot be put into a text");
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
        if (env.TryGetValue(c.Name, out var localType) && IsFnType(localType)) { used.Add(c.Name); return IndirectCallType(c, localType, argTypes); }
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
            {
                var want = target.Params[i].Type.Name;
                if (IsVar(want) && vars.TryGetValue(want, out var bound)) want = bound; // bitAnd(x, 255): the literal follows x
                if (IsDefaultLit(argTypes[i]) && !IsVar(want) && FitLiteral(c.Args[i], want)) { argTypes[i] = want; continue; }
                if (!Unify(target.Params[i].Type.Name, argTypes[i], vars))
                    Error(c.Line, "type-mismatch", $"argument `{target.Params[i].Name}` of `{c.Name}` must be {Subst(target.Params[i].Type.Name, vars)}, found {argTypes[i]}", $"signature: {target.Signature}");
            }
            if (target.IsStd && c.Name.StartsWith("bit", StringComparison.Ordinal) && vars.TryGetValue("T", out var bt) && bt != "" && bt != "_" && !IsInt(bt))
                Error(c.Line, "type-mismatch", $"`{c.Name}` needs a whole number (Int, I8..I32, U8..U64), found {bt}");
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
                if (!Unify(cs.Fields[i].Type.Name, argTypes[i], vars) && !FitLiteral(c.Args[i], cs.Fields[i].Type.Name))
                    Error(c.Line, "type-mismatch", $"field `{cs.Fields[i].Name}` of `{c.Name}` must be {Subst(cs.Fields[i].Type.Name, vars)}, found {argTypes[i]}", $"signature: {sig}");
        return def.Params.Count == 0 ? def.Name : Join(def.Name, def.Params.Select(p => vars.GetValueOrDefault(p, "_")));
    }

    string AssignType(Assign a, Dictionary<string, string> env, HashSet<string> used)
    {
        var ft = FieldType(a.Target, env, used);
        var vt = TypeOf(a.Value, env, used);
        if (_inContract) Error(a.Line, "contract-not-pure", "`set` changes data and cannot be used in a contract or example");
        else _effects.Add("mutate");
        if (ft != "" && vt != "" && !Compat(vt, ft) && !Widens(vt, ft) && !FitLiteral(a.Value, ft))
            Error(a.Line, "type-mismatch", $"field `{a.Target.Name}` is {ft} but the value is {vt}");
        return "Unit";
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
        var t = TypeOf(u.Operand, env, used);
        if (u.Op != "-")
        {
            if (t != "" && t != "Bool") Error(u.Line, "type-mismatch", $"`{u.Op}` needs Bool, found {t}");
            return "Bool";
        }
        if (t != "" && (!IsNum(t) || IsUnsigned(t))) Error(u.Line, "type-mismatch", $"`-` needs a signed number (Int, I8, I16, I32, F32, F64), found {t}");
        return IsNum(t) && !IsUnsigned(t) ? t : "Int";
    }

    string BinaryType(Binary b, Dictionary<string, string> env, HashSet<string> used)
    {
        var l = TypeOf(b.Left, env, used);
        var r = TypeOf(b.Right, env, used);
        if (IsDefaultLit(l) && l != r && FitLiteral(b.Left, r)) l = r;
        else if (IsDefaultLit(r) && l != r && FitLiteral(b.Right, l)) r = l;
        if (IsNum(l) && IsNum(r) && l != r && b.Op is "+" or "-" or "*" or "/" or "%" or "<" or "<=" or ">" or ">=" or "==" or "!=")
        {
            if (Widens(l, r)) l = r; else if (Widens(r, l)) r = l; // the narrower side is widened, never the other way
        }
        bool known = l != "" && r != "";
        switch (b.Op)
        {
            case "+" or "-" or "*" or "/" or "%":
                if (known && (!IsNum(l) || l != r))
                    Error(b.Line, "type-mismatch", $"`{b.Op}` needs the same number type on both sides, found {l} and {r}",
                        b.Op == "+" && (l == "Text" || r == "Text") ? "to join texts, write one text with `{...}` parts"
                        : IsNum(l) && IsNum(r) ? "neither type fits in the other; convert one side first, for example with numFromI32 / numToF64" : null);
                return IsNum(l) ? l : IsNum(r) ? r : "Int";
            case "<" or "<=" or ">" or ">=":
                if (known && (!IsNum(l) || l != r)) Error(b.Line, "type-mismatch", $"`{b.Op}` needs the same number type on both sides, found {l} and {r}",
                    IsNum(l) && IsNum(r) ? "neither type fits in the other; convert one side first, for example with numFromI32 / numToF64" : null);
                return "Bool";
            case "==" or "!=":
                if (known && (!Compat(l, r) || l == "Unit" || IsFnType(l) || IsList(l) || IsVar(l) || UserType(l) != null))
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
}
