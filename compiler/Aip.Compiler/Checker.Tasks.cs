namespace Aip;

/// <summary>Checker: rules for `Task<R>` (docs/design/parallelism-T-0935.md). A task is started with `taskRun` and must be
/// awaited exactly once, on every path, by a `taskAwait` in the same function. It cannot be stored, returned or passed on,
/// so no task is ever forgotten and a failure always surfaces at a `taskAwait` the program wrote.</summary>
sealed partial class Checker
{
    static bool MentionsTask(string t) => t == "Task" || (Split(t) is { } s && (s.Name == "Task" || s.Args.Any(MentionsTask)));
    static bool IsTask(string t) => Split(t) is { Name: "Task" };

    void CheckTaskTypes(Fn fn)
    {
        if (fn.IsStd) return;
        foreach (var p in fn.Params)
            if (MentionsTask(p.Type.Name)) Error(fn.Line, "task-escape", $"parameter `{p.Name}` has a Task type; a task cannot be passed to another function", "await it in the function that started it and pass the result");
        if (MentionsTask(fn.Ret.Name)) Error(fn.Line, "task-escape", "a function cannot return a Task", "call `taskAwait` and return the result");
    }

    void CheckTasks(Fn fn)
    {
        var lets = new Dictionary<string, (int Line, int Cond)>();
        var awaits = new Dictionary<string, List<int>>();

        void Walk(Expr e, Expr? parent, bool letInit, int cond)
        {
            if (IsTask(e.Type))
            {
                bool awaited = parent is Call { Name: "taskAwait" };
                if (e is NameRef n)
                {
                    if (!awaited) Error(e.Line, "task-escape", $"task `{n.Name}` can only be used as the argument of `taskAwait`", "await it where you need the result");
                    else (awaits.TryGetValue(n.Name, out var l) ? l : awaits[n.Name] = new()).Add(cond);
                }
                else if (!letInit && !awaited)
                    Error(e.Line, "task-unawaited", "this task is started but never awaited", "bind it with `let t = ...` and later call `taskAwait(t)`, or write `taskAwait(taskRun(...))`");
            }
            switch (e)
            {
                case Call c: foreach (var a in c.Args) Walk(a, c, false, cond); break;
                case Unary u: Walk(u.Operand, u, false, cond); break;
                case Binary b: Walk(b.Left, b, false, cond); Walk(b.Right, b, false, cond); break;
                case IfExpr i: Walk(i.Cond, i, false, cond); Walk(i.Then, i, false, cond + 1); Walk(i.Else, i, false, cond + 1); break;
                case TextLit t: foreach (var p in t.Parts.OfType<Expr>()) Walk(p, t, false, cond); break;
                case Field f: Walk(f.Target, f, false, cond); break;
                case Assign a: Walk(a.Value, a, false, cond); break;
                case MatchExpr m: Walk(m.Subject, m, false, cond); foreach (var arm in m.Arms) Walk(arm.Body, m, false, cond + 1); break;
                case BlockExpr bl: foreach (var s in bl.Stmts) Walk(s, bl, false, cond); break;
                case LoopExpr lp: foreach (var x in lp.Parts) Walk(x, lp, false, cond + 1); break;
                case ForExpr fe: foreach (var x in fe.Parts) Walk(x, fe, false, cond + 1); break;
                case DoneExpr d: Walk(d.Value, d, false, cond); break;
                case NextExpr n: foreach (var x in n.Args) Walk(x, n, false, cond); break;
            }
        }

        foreach (var s in fn.Body)
        {
            Walk(s.Value, null, s.Let != null, 0);
            if (s.Let != null && IsTask(s.Value.Type)) lets[s.Let] = (s.Line, 0);
        }
        foreach (var (name, (line, cond)) in lets)
        {
            var uses = awaits.GetValueOrDefault(name) ?? new List<int>();
            if (uses.Count == 0) Error(line, "task-unawaited", $"task `{name}` is started but never awaited", $"call `taskAwait({name})`");
            else if (uses.Count > 1) Error(line, "task-double-await", $"task `{name}` is awaited {uses.Count} times; a task has one result", "await it once and keep the result in a `let`");
            else if (uses[0] != cond) Error(line, "task-unawaited", $"task `{name}` is awaited only in some branches of an `if` or `match`", "await it before the branch");
        }
    }
}
