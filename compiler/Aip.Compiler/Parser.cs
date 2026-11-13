using System.Text;

namespace Aip;

enum T { Int, Text, Ident, Sym, End }

sealed record Tok(T Kind, string Text);

/// <summary>Parses one .fn file. The format is line based: signature, header lines, then `body` or `extern`.</summary>
sealed class Parser
{
    static readonly string[] Symbols = { "->", "=>", "==", "!=", "<=", ">=", "..", ".", "(", ")", ",", "<", ">", "+", "-", "*", "/", "%", "=" };

    readonly string _file;
    int _line;
    List<Tok> _toks = new();
    int _pos;

    bool _matchAllowed;

    Parser(string file) { _file = file; }

    CompileException Err(string code, string message, string? fix = null) =>
        new(new Diagnostic(_file, _line, code, message, fix));

    public static Fn ParseFile(string file, string[]? lines = null)
    {
        var p = new Parser(file);
        return p.ParseLines(lines ?? File.ReadAllLines(file));
    }

    /// <summary>One line per token (`Kind text`), and `Newline` after each non-blank line. Used to compare with the lexer written in AIP.</summary>
    public static string Tokens(string file)
    {
        var p = new Parser(file);
        var sb = new StringBuilder();
        foreach (var raw in File.ReadAllLines(file))
        {
            p._line++;
            var text = raw.Trim();
            if (text.Length == 0) continue;
            p.Lex(text);
            foreach (var t in p._toks.Where(t => t.Kind != T.End)) sb.Append(System.Globalization.CultureInfo.InvariantCulture, $"{t.Kind} {t.Text}\n");
            sb.Append("Newline\n");
        }
        return sb.ToString();
    }

    Fn ParseLines(string[] lines)
    {
        var fn = new Fn { File = _file };
        // 0 = expect signature, 1 = header, 2 = body, 3 = after extern
        int state = 0;
        bool sawEffects = false;
        MatchExpr? pending = null;
        for (int i = 0; i < lines.Length; i++)
        {
            _line = i + 1;
            var raw = lines[i];
            if (raw.Trim().Length == 0) continue;
            if (raw.Contains('\t')) throw Err("tab", "tabs are not allowed", "indent with two spaces");
            int indent = raw.Length - raw.TrimStart(' ').Length;
            var text = raw.Trim();

            if (state == 0)
            {
                if (indent != 0 || !text.StartsWith("fn ", StringComparison.Ordinal))
                    throw Err("missing-signature", "the first line must declare the function", "start the file with `fn name(params) -> Type`");
                Lex(text);
                ParseSignature(fn);
                state = 1;
                continue;
            }

            if (indent == 0)
            {
                if (state != 1 || (text != "body" && text != "extern"))
                    throw Err("unexpected-line", $"unexpected line `{text}`", "header and body lines are indented by two spaces");
                if (fn.Doc == "") throw Err("missing-doc", "the function has no purpose line", "add a one-line description in double quotes as line 2");
                if (!sawEffects) throw Err("missing-effects", "the function does not declare its effects", "add `pure` or `effects name, ...` to the header");
                fn.IsExtern = text == "extern";
                state = fn.IsExtern ? 3 : 2;
                continue;
            }

            if (indent == 4 && state == 2 && pending != null)
            {
                ParseArm(pending, text);
                continue;
            }
            if (indent != 2) throw Err("bad-indent", $"expected an indent of 2 spaces, found {indent}");
            if (state == 3) throw Err("unexpected-line", "an extern function has no body");

            if (state == 1)
            {
                if (fn.Doc == "")
                {
                    if (!text.StartsWith('"') || !text.EndsWith('"') || text.Length < 3)
                        throw Err("missing-doc", "line 2 must be the purpose of the function", "add a one-line description in double quotes as line 2");
                    fn.Doc = text[1..^1];
                }
                else if (text == "pure" || text.StartsWith("effects ", StringComparison.Ordinal))
                {
                    if (sawEffects) throw Err("duplicate-effects", "effects are declared twice");
                    sawEffects = true;
                    fn.EffectsLine = _line;
                    if (text != "pure")
                        fn.Effects = text["effects ".Length..].Split(',').Select(e => e.Trim()).Where(e => e != "").ToList();
                }
                else if (text.StartsWith("requires ", StringComparison.Ordinal)) fn.Requires.Add(ParseExprText(text["requires ".Length..]));
                else if (text.StartsWith("ensures ", StringComparison.Ordinal)) fn.Ensures.Add(ParseExprText(text["ensures ".Length..]));
                else if (text.StartsWith("example ", StringComparison.Ordinal)) fn.Examples.Add(ParseExprText(text["example ".Length..]));
                else throw Err("unexpected-line", $"unknown header line `{text}`", "header lines are: purpose, `pure` or `effects`, `requires`, `ensures`, `example`; then `body`");
                continue;
            }

            // state 2: a statement
            Lex(text);
            var stmt = new Stmt { Line = _line };
            if (Peek.Kind == T.Ident && Peek.Text == "let")
            {
                _pos++;
                stmt.Let = Expect(T.Ident, "a name after `let`").Text;
                ExpectSym("=");
            }
            _matchAllowed = true;
            stmt.Value = ParseExpr();
            ExpectEnd();
            fn.Body.Add(stmt);
            pending = stmt.Value as MatchExpr;
        }

        _line = Math.Max(1, lines.Length);
        if (state == 0) throw Err("missing-signature", "the file is empty", "start the file with `fn name(params) -> Type`");
        if (state == 1) throw Err("missing-body", "the function has no `body` line", "add a line `body` followed by the indented body");
        if (state == 2 && fn.Body.Count == 0) throw Err("empty-body", "the body is empty");
        return fn;
    }

    void ParseSignature(Fn fn)
    {
        _pos++; // fn
        fn.Line = _line;
        fn.Name = Expect(T.Ident, "a function name").Text;
        ExpectSym("(");
        while (!IsSym(")"))
        {
            if (fn.Params.Count > 0) ExpectSym(",");
            var name = Expect(T.Ident, "a parameter name").Text;
            fn.Params.Add(new Param(name, ParseType()));
        }
        ExpectSym(")");
        ExpectSym("->");
        fn.Ret = ParseType();
        ExpectEnd();
    }

    TypeRef ParseType()
    {
        var name = Expect(T.Ident, "a type name").Text;
        if ((name == "List" || name == "Set" || name == "Map") && IsSym("<"))
        {
            _pos++;
            var args = new List<string>();
            do
            {
                if (args.Count > 0) _pos++; // the comma
                var inner = ParseType();
                if (inner.Range != "") throw Err("syntax", "an element type cannot have a range");
                args.Add(inner.Name);
            } while (IsSym(","));
            ExpectSym(">");
            name = $"{name}<{string.Join(",", args)}>";
        }
        var range = new StringBuilder();
        if (Peek.Kind == T.Int || IsSym("-") || IsSym(".."))
        {
            if (!IsSym("..")) range.Append(ParseBound());
            ExpectSym("..");
            range.Append("..");
            if (Peek.Kind == T.Int || IsSym("-")) range.Append(ParseBound());
        }
        return new TypeRef(name, range.ToString());
    }

    string ParseBound()
    {
        var sign = "";
        if (IsSym("-")) { _pos++; sign = "-"; }
        var digits = Expect(T.Int, "a number").Text;
        if (!long.TryParse(sign + digits, out _)) throw Err("bad-number", $"`{sign}{digits}` is too large for Int");
        return sign + digits;
    }

    void ParseArm(MatchExpr m, string text)
    {
        Lex(text);
        var arm = new Arm { Line = _line, Case = Expect(T.Ident, "a case name").Text };
        if (IsSym("("))
        {
            _pos++;
            while (!IsSym(")"))
            {
                if (arm.Binds.Count > 0) ExpectSym(",");
                arm.Binds.Add(Expect(T.Ident, "a name for the value").Text);
            }
            ExpectSym(")");
        }
        ExpectSym("=>");
        _matchAllowed = false;
        arm.Body = ParseExpr();
        ExpectEnd();
        m.Arms.Add(arm);
    }

    public static TypeDef ParseTypeFile(string file, string[]? lines = null)
    {
        var p = new Parser(file);
        return p.ParseTypeLines(lines ?? File.ReadAllLines(file));
    }

    /// <summary>`struct Name` or `variant Name`, a purpose line, then fields (`name Type`) or cases (`Case(name Type, ...)`), all indented by two spaces.</summary>
    TypeDef ParseTypeLines(string[] lines)
    {
        TypeDef? def = null;
        for (int i = 0; i < lines.Length; i++)
        {
            _line = i + 1;
            var raw = lines[i];
            if (raw.Trim().Length == 0) continue;
            if (raw.Contains('\t')) throw Err("tab", "tabs are not allowed", "indent with two spaces");
            int indent = raw.Length - raw.TrimStart(' ').Length;
            var text = raw.Trim();
            if (def == null)
            {
                if (indent != 0 || !(text.StartsWith("struct ", StringComparison.Ordinal) || text.StartsWith("variant ", StringComparison.Ordinal)))
                    throw Err("missing-signature", "the first line must declare the type", "start the file with `struct Name` or `variant Name`");
                Lex(text);
                bool isVariant = Peek.Text == "variant";
                _pos++;
                def = new TypeDef { File = _file, Line = _line, IsVariant = isVariant, Name = Expect(T.Ident, "a type name").Text };
                ExpectEnd();
                if (!isVariant) def.Cases.Add(new Case { Name = def.Name, Line = _line });
                continue;
            }
            if (indent != 2) throw Err("bad-indent", $"expected an indent of 2 spaces, found {indent}");
            if (def.Doc == "")
            {
                if (!text.StartsWith('"') || !text.EndsWith('"') || text.Length < 3)
                    throw Err("missing-doc", "line 2 must be the purpose of the type", "add a one-line description in double quotes as line 2");
                def.Doc = text[1..^1];
                continue;
            }
            Lex(text);
            if (!def.IsVariant)
            {
                var name = Expect(T.Ident, "a field name").Text;
                def.Cases[0].Fields.Add(new Param(name, ParseType()));
            }
            else
            {
                var c = new Case { Name = Expect(T.Ident, "a case name").Text, Line = _line };
                if (IsSym("("))
                {
                    _pos++;
                    while (!IsSym(")"))
                    {
                        if (c.Fields.Count > 0) ExpectSym(",");
                        var name = Expect(T.Ident, "a field name").Text;
                        c.Fields.Add(new Param(name, ParseType()));
                    }
                    ExpectSym(")");
                }
                def.Cases.Add(c);
            }
            ExpectEnd();
        }
        _line = Math.Max(1, lines.Length);
        if (def == null) throw Err("missing-signature", "the file is empty", "start the file with `struct Name` or `variant Name`");
        if (def.Doc == "") throw Err("missing-doc", "the type has no purpose line", "add a one-line description in double quotes as line 2");
        if (!def.IsVariant && def.Cases[0].Fields.Count == 0) throw Err("empty-type", "a struct needs at least one field", "add a line `name Type`");
        if (def.IsVariant && def.Cases.Count == 0) throw Err("empty-type", "a variant needs at least one case", "add a line `CaseName(field Type)`");
        return def;
    }

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
                if (t.Text == "match") throw Err("syntax", "a `match` must start a line or follow `let name =`", "move it to its own line, or call a function that does the match");
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

    // ---- tokens ----

    Tok Peek => _toks[_pos];
    bool IsSym(string s) => Peek.Kind == T.Sym && Peek.Text == s;

    Tok Expect(T kind, string what)
    {
        if (Peek.Kind != kind) throw Err("syntax", $"expected {what}, found {Describe(Peek)}");
        return _toks[_pos++];
    }

    void ExpectSym(string s)
    {
        if (!IsSym(s)) throw Err("syntax", $"expected `{s}`, found {Describe(Peek)}");
        _pos++;
    }

    void ExpectWord(string w)
    {
        if (Peek.Kind != T.Ident || Peek.Text != w) throw Err("syntax", $"expected `{w}`, found {Describe(Peek)}");
        _pos++;
    }

    void ExpectEnd()
    {
        if (Peek.Kind != T.End) throw Err("syntax", $"unexpected {Describe(Peek)} at the end of the line");
    }

    static string Describe(Tok t) => t.Kind == T.End ? "the end of the line" : $"`{t.Text}`";

    void Lex(string s)
    {
        var toks = new List<Tok>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == ' ') { i++; continue; }
            if (char.IsAsciiDigit(c))
            {
                int j = i;
                while (j < s.Length && char.IsAsciiDigit(s[j])) j++;
                toks.Add(new Tok(T.Int, s[i..j]));
                i = j;
            }
            else if (char.IsAsciiLetter(c) || c == '_')
            {
                int j = i;
                while (j < s.Length && (char.IsAsciiLetterOrDigit(s[j]) || s[j] == '_')) j++;
                toks.Add(new Tok(T.Ident, s[i..j]));
                i = j;
            }
            else if (c == '"')
            {
                int j = i + 1;
                while (j < s.Length && s[j] != '"') j += s[j] == '\\' ? 2 : 1;
                if (j >= s.Length) throw Err("syntax", "a text is missing its closing double quote");
                toks.Add(new Tok(T.Text, s[(i + 1)..j]));
                i = j + 1;
            }
            else
            {
                var sym = Symbols.FirstOrDefault(x => string.CompareOrdinal(s, i, x, 0, x.Length) == 0)
                    ?? throw Err("syntax", $"unexpected character `{c}`");
                toks.Add(new Tok(T.Sym, sym));
                i += sym.Length;
            }
        }
        toks.Add(new Tok(T.End, ""));
        _toks = toks;
        _pos = 0;
    }
}
