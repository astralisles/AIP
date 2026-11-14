using System.Text;

namespace Aip;

/// <summary>C# back end: `loop` and `for` (T-0994, docs/design/loops-T-0994.md).</summary>
sealed partial class CsBackend
{
    static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

    /// <summary>A loop is an expression, so it becomes an immediately called lambda with a `while (true)` inside.</summary>
    static string EmitLoop(LoopExpr l)
    {
        var sb = new StringBuilder();
        var ret = CsType(l.Type);
        sb.Append(Inv, $"((System.Func<{ret}>)(() =>\n{{\n");
        for (int i = 0; i < l.Names.Count; i++)
            sb.Append(Inv, $"    {CsType(l.StateTypes[i])} @{l.Names[i]} = {Emit(l.Inits[i])};\n");
        sb.Append("    while (true)\n    {\n");
        sb.Append(EmitBody(l.Body, l.Body.Count - 1, "        "));
        sb.Append(EmitStep(l, l.Body[^1].Value, "        "));
        sb.Append("    }\n}))()");
        return sb.ToString();
    }

    static string EmitBody(List<Stmt> body, int count, string indent)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < count; i++)
        {
            var s = body[i];
            if (s.Let != null) sb.Append(Inv, $"{indent}var @{s.Let} = {Emit(s.Value)};\n");
            else sb.Append(EmitUnit(s.Value, indent));
        }
        return sb.ToString();
    }

    static string EmitStep(LoopExpr l, Expr e, string indent)
    {
        switch (e)
        {
            case IfExpr i:
                return $"{indent}if ({Emit(i.Cond)})\n{indent}{{\n{EmitStep(l, i.Then, indent + "    ")}{indent}}}\n{indent}else\n{indent}{{\n{EmitStep(l, i.Else, indent + "    ")}{indent}}}\n";
            case DoneExpr d:
                return $"{indent}return {Emit(d.Value)};\n";
            case NextExpr n:
                {
                    var sb = new StringBuilder();
                    for (int k = 0; k < n.Args.Count; k++) sb.Append(Inv, $"{indent}var __n{k} = ({CsType(l.StateTypes[k])})({Emit(n.Args[k])});\n");
                    for (int k = 0; k < n.Args.Count; k++) sb.Append(Inv, $"{indent}@{l.Names[k]} = __n{k};\n");
                    return sb.Append(indent).Append("continue;\n").ToString();
                }
        }
        throw new InvalidOperationException("unknown loop step");
    }

    static string EmitFor(ForExpr f, string indent)
    {
        var to = $"__to{f.Line}";
        return $"{indent}{{\n{indent}    long {to} = {Emit(f.To)};\n{indent}    for (long @{f.Var} = {Emit(f.From)}; @{f.Var} < {to}; @{f.Var}++)\n{indent}    {{\n"
            + EmitBody(f.Body, f.Body.Count, indent + "        ") + $"{indent}    }}\n{indent}}}\n";
    }
}
