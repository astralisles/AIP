using System.Text;

namespace Aip;

/// <summary>Expression parsing.</summary>
sealed partial class Parser
{
    Expr ParseExprText(string text)
    {
        _matchAllowed = false;
        var saved = (_toks, _pos);
        Lex(text);
        var e = ParseExpr();
        ExpectEnd();
        (_toks, _pos) = saved;
        return e;
    }

    // ---- expressions, lowest precedence first ----

    Expr ParseExpr()
    {
        bool matchOk = _matchAllowed;
        _matchAllowed = false;
        if (matchOk && Peek.Kind == T.Ident && Peek.Text == "match")
        {
            _pos++;
            var m = new MatchExpr { Line = _line, Subject = ParseExpr() };
            ExpectEnd();
            return m;
        }
        if (Peek.Kind == T.Ident && Peek.Text == "if")
        {
            _pos++;
            var e = new IfExpr { Line = _line, Cond = ParseExpr() };
            ExpectWord("then");
            e.Then = ParseExpr();
            ExpectWord("else");
            e.Else = ParseExpr();
            return e;
        }
        return ParseBinary(0);
    }

    static readonly string[][] Levels =
    {
        new[] { "=>" },
        new[] { "or" },
        new[] { "and" },
        new[] { "==", "!=", "<", "<=", ">", ">=" },
        new[] { "+", "-" },
        new[] { "*", "/", "%" },
    };

    Expr ParseBinary(int level)
    {
        if (level == Levels.Length) return ParseUnary();
        var left = ParseBinary(level + 1);
        while ((Peek.Kind == T.Sym || Peek.Kind == T.Ident) && Levels[level].Contains(Peek.Text))
        {
            var op = Peek.Text;
            _pos++;
            left = new Binary { Line = _line, Op = op, Left = left, Right = ParseBinary(level + 1) };
        }
        return left;
    }

    Expr ParseUnary()
    {
        if (IsSym("-")) { _pos++; return new Unary { Line = _line, Op = "-", Operand = ParseUnary() }; }
        if (Peek.Kind == T.Ident && Peek.Text == "not") { _pos++; return new Unary { Line = _line, Op = "not", Operand = ParseUnary() }; }
        return ParsePostfix();
    }

    Expr ParsePostfix()
    {
        var e = ParsePrimary();
        while (IsSym("."))
        {
            _pos++;
            e = new Field { Line = _line, Target = e, Name = Expect(T.Ident, "a field name after `.`").Text };
        }
        return e;
    }

    Expr ParsePrimary()
    {
        var t = Peek;
        _pos++;
        switch (t.Kind)
        {
            case T.Int:
                if (!long.TryParse(t.Text, out var v)) throw Err("bad-number", $"`{t.Text}` is too large for Int");
                return new IntLit { Line = _line, Value = v };
            case T.Text:
                return ParseText(t.Text);
            case T.Ident:
                if (t.Text == "match") throw Err("syntax", "a `match` must start a line, follow `let name =`, or be the whole of a case after `=>`", "move it to its own line, or call a function that does the match");
                if (t.Text == "true" || t.Text == "false") return new BoolLit { Line = _line, Value = t.Text == "true" };
                if (IsSym("("))
                {
                    _pos++;
                    var call = new Call { Line = _line, Name = t.Text };
                    while (!IsSym(")"))
                    {
                        if (call.Args.Count > 0) ExpectSym(",");
                        call.Args.Add(ParseExpr());
                    }
                    ExpectSym(")");
                    return call;
                }
                return new NameRef { Line = _line, Name = t.Text };
            case T.Sym when t.Text == "(":
                var inner = ParseExpr();
                ExpectSym(")");
                return inner;
        }
        throw Err("syntax", t.Kind == T.End ? "the line ends where an expression is expected" : $"unexpected `{t.Text}`");
    }

    TextLit ParseText(string raw)
    {
        var lit = new TextLit { Line = _line };
        var sb = new StringBuilder();
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (c == '\\')
            {
                char n = i + 1 < raw.Length ? raw[++i] : '\\';
                sb.Append(n switch { 'n' => '\n', 't' => '\t', _ => n });
            }
            else if (c == '{')
            {
                int end = raw.IndexOf('}', i);
                if (end < 0) throw Err("syntax", "`{` in a text has no closing `}`", "write `\\{` for a literal brace");
                if (sb.Length > 0) { lit.Parts.Add(sb.ToString()); sb.Clear(); }
                lit.Parts.Add(ParseExprText(raw[(i + 1)..end]));
                i = end;
            }
            else sb.Append(c);
        }
        if (sb.Length > 0 || lit.Parts.Count == 0) lit.Parts.Add(sb.ToString());
        return lit;
    }
}
