using System.Text.RegularExpressions;

namespace Aip;

/// <summary>`loop` and `for` blocks (T-0994, docs/design/loops-T-0994.md). They nest by indentation, so a block is parsed recursively.</summary>
sealed partial class Parser
{
    int _inLoopBody;

    static readonly Regex LetLoop = new(@"^let ([A-Za-z_][A-Za-z0-9_]*) = loop$", RegexOptions.Compiled);

    /// <summary>`loop`, `let name = loop` or `for name in a..b` on a line of its own.</summary>
    static bool IsCompoundHeader(string text, out string? let)
    {
        let = null;
        if (text == "loop") return true;
        if (text.StartsWith("for ", StringComparison.Ordinal)) return true;
        var m = LetLoop.Match(text);
        if (!m.Success) return false;
        let = m.Groups[1].Value;
        return true;
    }

    /// <summary>Parses the block that starts at the header lines[i] (at `indent`). On return `i` is the first line not used.</summary>
    Expr ParseCompound(string[] lines, ref int i, int indent, string text)
    {
        int line = i + 1;
        _line = line;
        if (text.StartsWith("for ", StringComparison.Ordinal))
        {
            Lex(text);
            _pos++; // for
            var f = new ForExpr { Line = line, Var = Expect(T.Ident, "a name after `for`").Text };
            ExpectWord("in");
            _matchAllowed = false;
            f.From = ParseExpr();
            ExpectSym("..");
            f.To = ParseExpr();
            ExpectEnd();
            i++;
            f.Body = ParseBlock(lines, ref i, indent + 2);
            _line = line;
            if (f.Body.Count == 0) throw Err("empty-body", "a `for` has no statements", "indent the statements to repeat by 2 spaces under the `for` line");
            return f;
        }

        var loop = new LoopExpr { Line = line };
        i++;
        while (true)
        {
            while (i < lines.Length && lines[i].Trim().Length == 0) i++;
            if (i >= lines.Length) { _line = line; throw Err("loop-no-body", "this `loop` has no `body` line", "write the names with their start values (`name = 0`), then a line `body`"); }
            var raw = lines[i];
            _line = i + 1;
            if (raw.Contains('\t')) throw Err("tab", "tabs are not allowed", "indent with two spaces");
            int ind = raw.Length - raw.TrimStart(' ').Length;
            if (ind != indent + 2) throw Err("bad-indent", $"expected an indent of {indent + 2} spaces, found {ind}", "a `loop` is followed by `name = start` lines and a `body` line, indented 2 spaces");
            var t = raw.Trim();
            i++;
            if (t == "body") break;
            Lex(t);
            loop.Names.Add(Expect(T.Ident, "a name for a loop value").Text);
            loop.NameLines.Add(_line);
            ExpectSym("=");
            _matchAllowed = false;
            loop.Inits.Add(ParseExpr());
            ExpectEnd();
        }
        int bodyLine = _line;
        _inLoopBody++; // `done` and `next` are keywords only here, so existing names like `next` keep working elsewhere
        try { loop.Body = ParseBlock(lines, ref i, indent + 4); }
        finally { _inLoopBody--; }
        _line = bodyLine;
        if (loop.Names.Count == 0) throw Err("loop-no-state", "this `loop` has no names", "add a line such as `n = 0` before `body`");
        if (loop.Body.Count == 0) throw Err("empty-body", "the `loop` body is empty", "indent the body lines by 4 spaces under `body`");
        return loop;
    }

    /// <summary>Statements at exactly `indent`, until a line indented less. A line may start a nested `loop` or `for`.</summary>
    List<Stmt> ParseBlock(string[] lines, ref int i, int indent)
    {
        var list = new List<Stmt>();
        while (i < lines.Length)
        {
            var raw = lines[i];
            if (raw.Trim().Length == 0) { i++; continue; }
            _line = i + 1;
            if (raw.Contains('\t')) throw Err("tab", "tabs are not allowed", "indent with two spaces");
            int ind = raw.Length - raw.TrimStart(' ').Length;
            if (ind < indent) break;
            if (ind > indent) throw Err("bad-indent", $"expected an indent of {indent} spaces, found {ind}");
            var text = raw.Trim();
            var st = new Stmt { Line = _line };
            if (IsCompoundHeader(text, out var let))
            {
                st.Let = let;
                st.Value = ParseCompound(lines, ref i, indent, text);
                list.Add(st);
                continue;
            }
            i++;
            Lex(text);
            if (Peek.Kind == T.Ident && Peek.Text == "let")
            {
                _pos++;
                st.Let = Expect(T.Ident, "a name after `let`").Text;
                ExpectSym("=");
            }
            _matchAllowed = false;
            st.Value = st.Let == null ? ParseSetOrExpr() : ParseExpr();
            ExpectEnd();
            list.Add(st);
        }
        return list;
    }
}
