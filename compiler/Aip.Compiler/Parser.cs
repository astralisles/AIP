using System.Text;

namespace Aip;

enum T { Int, Text, Ident, Sym, End }

sealed record Tok(T Kind, string Text);

/// <summary>Parses one .fn file. The format is line based: signature, header lines, then `body` or `extern`.</summary>
sealed partial class Parser
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

    /// <summary>Parses source text already in memory; `file` is only used for names and errors.</summary>
    public static Fn ParseText(string file, string text)
    {
        var lines = new List<string>();
        using (var r = new StringReader(text))
            for (var l = r.ReadLine(); l != null; l = r.ReadLine()) lines.Add(l); // same split as File.ReadAllLines
        return ParseFile(file, lines.ToArray());
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
        IfExpr? openIf = null; // a multi-line `if` whose branch lines are still being read
        bool inElse = false;
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
            if (indent == 4 && state == 2 && openIf != null)
            {
                Lex(text);
                _matchAllowed = false;
                var line = ParseExpr();
                ExpectEnd();
                ((BlockExpr)(inElse ? openIf.Else : openIf.Then)).Stmts.Add(line);
                continue;
            }
            if (indent != 2) throw Err("bad-indent", $"expected an indent of 2 spaces, found {indent}");
            if (state == 2 && openIf != null)
            {
                if (text == "else" && !inElse) { inElse = true; continue; }
                CloseIf(openIf, inElse);
                openIf = null;
            }
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
            if (text.StartsWith("if ", StringComparison.Ordinal) && text.EndsWith(" then", StringComparison.Ordinal))
            {
                openIf = new IfExpr { Line = _line, Cond = ParseExprText(text["if ".Length..^" then".Length]), Then = new BlockExpr { Line = _line }, Else = new BlockExpr { Line = _line } };
                inElse = false;
                fn.Body.Add(new Stmt { Line = _line, Value = openIf });
                pending = null;
                continue;
            }
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

        if (openIf != null) CloseIf(openIf, inElse);
        _line = Math.Max(1, lines.Length);
        if (state == 0) throw Err("missing-signature", "the file is empty", "start the file with `fn name(params) -> Type`");
        if (state == 1) throw Err("missing-body", "the function has no `body` line", "add a line `body` followed by the indented body");
        if (state == 2 && fn.Body.Count == 0) throw Err("empty-body", "the body is empty");
        return fn;
    }

    /// <summary>Ends a multi-line `if`: it needs an `else` line and at least one statement in each branch.</summary>
    void CloseIf(IfExpr i, bool inElse)
    {
        _line = i.Line;
        if (!inElse) throw Err("missing-else", "this `if` block has no `else`", "add a line `else` (indented 2 spaces) and the statements for the other case indented 4");
        if (((BlockExpr)i.Then).Stmts.Count == 0 || ((BlockExpr)i.Else).Stmts.Count == 0)
            throw Err("empty-branch", "a branch of this `if` block has no statements", "indent at least one statement (4 spaces) under `then` and under `else`");
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
        if (GenericTypes.Arity.ContainsKey(name) && IsSym("<"))
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
}
