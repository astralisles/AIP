using System.Globalization;
using System.Text;

namespace Aip;

/// <summary>`aip ast file`: the parsed requires / ensures / example lines and body as flat text, one line per statement or arm.
/// `aipc ast file` (written in AIP) prints the same text; scripts/compare-ast.ps1 checks they are equal.</summary>
static class AstDump
{
    public static string Dump(string file)
    {
        var fn = Parser.ParseFile(file);
        var sb = new StringBuilder();
        foreach (var e in fn.Requires) sb.Append("requires ").Append(S(e)).Append('\n');
        foreach (var e in fn.Ensures) sb.Append("ensures ").Append(S(e)).Append('\n');
        foreach (var e in fn.Examples) sb.Append("example ").Append(S(e)).Append('\n');
        foreach (var st in fn.Body)
        {
            if (st.Value is IfExpr { Then: BlockExpr thenBlock, Else: BlockExpr elseBlock } block)
            {
                sb.Append("if ").Append(S(block.Cond)).Append('\n');
                foreach (var s in thenBlock.Stmts) sb.Append("stmt ").Append(S(s)).Append('\n');
                sb.Append("else\n");
                foreach (var s in elseBlock.Stmts) sb.Append("stmt ").Append(S(s)).Append('\n');
                continue;
            }
            sb.Append("stmt ").Append(st.Let != null ? $"let {st.Let} " : "").Append(S(st.Value)).Append('\n');
            if (st.Value is MatchExpr m) Arms(sb, m);
        }
        return sb.ToString();
    }

    static void Arms(StringBuilder sb, MatchExpr m)
    {
        foreach (var a in m.Arms)
        {
            sb.Append("arm ").Append(a.Case).Append('[').Append(string.Join(",", a.Binds)).Append("] ").Append(S(a.Body)).Append('\n');
            if (a.Body is MatchExpr nested) Arms(sb, nested);
        }
    }

    static string Quote(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\n").Replace("\t", "\t") + "\"";

    static string S(Expr e) => e switch
    {
        IntLit i => $"(int {i.Value})",
        FloatLit f => "(float " + FloatText(f.Value) + ")",
        BoolLit b => b.Value ? "(bool true)" : "(bool false)",
        TextLit t => "(text" + string.Concat(t.Parts.Select(p => " " + (p is string s ? Quote(s) : S((Expr)p)))) + ")",
        NameRef n => $"(name {n.Name})",
        Call c => $"(call {c.Name}" + string.Concat(c.Args.Select(a => " " + S(a))) + ")",
        Field f => $"(field {S(f.Target)} {f.Name})",
        Unary u => $"(unary {u.Op} {S(u.Operand)})",
        Binary b => $"(binary {b.Op} {S(b.Left)} {S(b.Right)})",
        IfExpr i => $"(if {S(i.Cond)} {S(i.Then)} {S(i.Else)})",
        MatchExpr m => $"(match {S(m.Subject)})",
        _ => throw new InvalidOperationException("unexpected expression " + e.GetType().Name),
    };

    static string FloatText(double d)
    {
        var s = d.ToString("R", CultureInfo.InvariantCulture);
        return s.Contains('.') || s.Contains('E') ? s : s + ".0";
    }
}
