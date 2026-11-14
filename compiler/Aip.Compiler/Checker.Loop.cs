namespace Aip;

/// <summary>Checker: `loop` and `for` (T-0994, docs/design/loops-T-0994.md).</summary>
sealed partial class Checker
{
    /// <summary>Names a loop or `for` introduced in this function. A later `let` of the same name is rejected: the C# back end declares them in one scope.</summary>
    readonly HashSet<string> _loopNames = new();

    sealed class LoopCtx
    {
        public List<string> Names = new();
        public List<string> Types = new();
        public string Result = "";
        public bool Done;
    }

    LoopCtx? _loop;

    /// <summary>Adds a name that lives inside a loop. Returns false (after an error) when the name is taken.</summary>
    bool DeclareLoopName(string name, int line, string type, Dictionary<string, string> env, string what)
    {
        _loopNames.Add(name);
        if (IsConstName(name)) Error(line, "duplicate-name", $"{what} `{name}` is a constant ({Path.GetFileName(program.Fns[name].File)})", "choose a different name");
        else if (_patNames.Contains(name)) Error(line, "duplicate-name", $"{what} `{name}` is already used by a match case", "choose a different name");
        else if (!env.TryAdd(name, type)) Error(line, "duplicate-name", $"{what} `{name}` is already defined", "choose a different name");
        else return true;
        return false;
    }

    /// <summary>The statements of a loop or `for` body (all but the step of a loop): `let`s and Unit statements.</summary>
    Dictionary<string, int> CheckLoopStmts(List<Stmt> stmts, int count, Dictionary<string, string> inner, HashSet<string> used)
    {
        var lets = new Dictionary<string, int>();
        for (int i = 0; i < count; i++)
        {
            var s = stmts[i];
            var t = TypeOf(s.Value, inner, used);
            if (s.Let != null)
            {
                if (t == "Unit") Error(s.Line, "type-mismatch", $"`{s.Let}` would hold Unit, which is not a value");
                if (DeclareLoopName(s.Let, s.Line, t, inner, "name")) lets[s.Let] = s.Line;
            }
            else if (t != "" && t != "Unit")
                Error(s.Line, "unused-value", $"this {t} value is not used", "bind it with `let` or remove the line");
        }
        return lets;
    }

    void ReportUnused(Dictionary<string, int> lets, HashSet<string> used)
    {
        foreach (var (name, line) in lets)
            if (!used.Contains(name)) Error(line, "unused-let", $"`{name}` is never used", "remove the line");
    }

    string LoopType(LoopExpr l, Dictionary<string, string> env, HashSet<string> used)
    {
        var inner = new Dictionary<string, string>(env);
        var ctx = new LoopCtx();
        for (int i = 0; i < l.Names.Count; i++)
        {
            var t = TypeOf(l.Inits[i], env, used);
            if (t == "Unit") Error(l.NameLines[i], "type-mismatch", $"`{l.Names[i]}` would start as Unit, which is not a value");
            ctx.Names.Add(l.Names[i]);
            ctx.Types.Add(t);
        }
        for (int i = 0; i < l.Names.Count; i++) DeclareLoopName(l.Names[i], l.NameLines[i], ctx.Types[i], inner, "loop name");
        l.StateTypes = ctx.Types;

        var bodyLets = CheckLoopStmts(l.Body, l.Body.Count - 1, inner, used);
        var last = l.Body[^1];
        var saved = _loop;
        _loop = ctx;
        if (last.Let != null) Error(last.Line, "loop-no-exit", "the loop body ends with `let`", "end the body with `if ... then done ... else next ...`, or with `done ...` or `next ...`");
        else Step(last.Value, inner, used);
        _loop = saved;
        ReportUnused(bodyLets, used);
        if (!ctx.Done && last.Let == null) Error(l.Line, "loop-no-exit", "this loop never finishes: no path of its last line has `done`", "add `done value` for the case where the loop is finished");
        return ctx.Result;
    }

    /// <summary>The last line of a loop body: `if` chains whose leaves are `done value` or `next a, b`.</summary>
    void Step(Expr e, Dictionary<string, string> env, HashSet<string> used)
    {
        var ctx = _loop!;
        switch (e)
        {
            case IfExpr i:
                {
                    var c = TypeOf(i.Cond, env, used);
                    if (c != "" && c != "Bool") Error(i.Line, "type-mismatch", $"the `if` condition must be Bool, found {c}");
                    Step(i.Then, env, used);
                    Step(i.Else, env, used);
                    i.Type = "Exit";
                    break;
                }
            case DoneExpr d:
                {
                    var t = TypeOf(d.Value, env, used);
                    d.Type = t;
                    ctx.Done = true;
                    if (t == "Unit") Error(d.Line, "type-mismatch", "`done` needs a value, not Unit");
                    else if (t != "" && ctx.Result == "") ctx.Result = t;
                    else if (t != "" && !Compat(t, ctx.Result) && !Widens(t, ctx.Result) && !FitLiteral(d.Value, ctx.Result))
                        Error(d.Line, "type-mismatch", $"`done` gives {t} but an earlier `done` gives {ctx.Result}");
                    break;
                }
            case NextExpr n:
                {
                    var types = n.Args.Select(a => TypeOf(a, env, used)).ToList();
                    n.Type = "Exit";
                    if (n.Args.Count != ctx.Names.Count)
                    {
                        Error(n.Line, "loop-state-count", $"`next` gives {n.Args.Count} value(s) but the loop has {ctx.Names.Count}: {string.Join(", ", ctx.Names)}", "give the next value of every name, in the order they are declared");
                        break;
                    }
                    for (int k = 0; k < types.Count; k++)
                    {
                        var want = ctx.Types[k];
                        if (types[k] != "" && want != "" && !Compat(types[k], want) && !Widens(types[k], want) && !FitLiteral(n.Args[k], want))
                            Error(n.Line, "type-mismatch", $"`{ctx.Names[k]}` is {want} but `next` gives {types[k]}");
                    }
                    break;
                }
            default:
                TypeOf(e, env, used);
                Error(e.Line, "loop-no-exit", "this path of the loop ends without `done` or `next`", "end every path with `done value` (leave the loop) or `next a, b` (run the body again)");
                break;
        }
    }

    string ExitType(Expr e)
    {
        Error(e.Line, "loop-misplaced", $"`{(e is DoneExpr ? "done" : "next")}` only works as the last line of a `loop` body", "write it in the final `if ... then done ... else next ...` line of the loop");
        return "";
    }

    string ForType(ForExpr f, Dictionary<string, string> env, HashSet<string> used)
    {
        var from = TypeOf(f.From, env, used);
        var to = TypeOf(f.To, env, used);
        foreach (var (t, what) in new[] { (from, "start"), (to, "end") })
            if (t != "" && t != "Int") Error(f.Line, "type-mismatch", $"the {what} of a `for` range must be Int, found {t}");
        var inner = new Dictionary<string, string>(env);
        DeclareLoopName(f.Var, f.Line, "Int", inner, "loop name");
        ReportUnused(CheckLoopStmts(f.Body, f.Body.Count, inner, used), used);
        return "Unit";
    }
}
