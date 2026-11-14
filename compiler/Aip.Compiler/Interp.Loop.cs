using System.Numerics;

namespace Aip;

/// <summary>Interpreter: `loop` and `for` (T-0994, docs/design/loops-T-0994.md).</summary>
sealed partial class Interp
{
    object? EvalLoop(LoopExpr l, Dictionary<string, object?> env)
    {
        var state = new object?[l.Names.Count];
        for (int i = 0; i < state.Length; i++) state[i] = Coerce(Eval(l.Inits[i], env), l.StateTypes[i]);
        while (true)
        {
            var inner = new Dictionary<string, object?>(env);
            for (int i = 0; i < state.Length; i++) inner[l.Names[i]] = state[i];
            for (int i = 0; i < l.Body.Count - 1; i++)
            {
                var s = l.Body[i];
                var v = Eval(s.Value, inner);
                if (s.Let != null) inner[s.Let] = v;
            }
            var (done, value, next) = Step(l.Body[^1].Value, inner);
            if (done) return value;
            for (int i = 0; i < state.Length; i++) state[i] = Coerce(next![i], l.StateTypes[i]);
        }
    }

    (bool Done, object? Value, object?[]? Next) Step(Expr e, Dictionary<string, object?> env)
    {
        switch (e)
        {
            case IfExpr i: return Step((bool)Eval(i.Cond, env)! ? i.Then : i.Else, env);
            case DoneExpr d: return (true, Eval(d.Value, env), null);
            case NextExpr n: return (false, null, n.Args.Select(a => Eval(a, env)).ToArray());
        }
        throw new Unsupported("loop step " + e.GetType().Name);
    }

    object? EvalFor(ForExpr f, Dictionary<string, object?> env)
    {
        var from = Big(Eval(f.From, env)!);
        var to = Big(Eval(f.To, env)!);
        for (BigInteger k = from; k < to; k++)
        {
            var inner = new Dictionary<string, object?>(env) { [f.Var] = Coerce(k, "I64") };
            foreach (var s in f.Body)
            {
                var v = Eval(s.Value, inner);
                if (s.Let != null) inner[s.Let] = v;
            }
        }
        return null;
    }
}
