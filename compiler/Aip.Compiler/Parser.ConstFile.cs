using System.Text;

namespace Aip;

/// <summary>Parses a .const file.</summary>
sealed partial class Parser
{
    public static Fn ParseConstFile(string file, string[]? lines = null)
    {
        var p = new Parser(file);
        return p.ParseConstLines(lines ?? File.ReadAllLines(file));
    }

    /// <summary>`const name Type`, a purpose line, then `value expr`, all but the first line indented by two spaces.
    /// A constant is kept as a parameterless pure Fn with IsConst set and one body statement.</summary>
    Fn ParseConstLines(string[] lines)
    {
        Fn? fn = null;
        for (int i = 0; i < lines.Length; i++)
        {
            _line = i + 1;
            var raw = lines[i];
            if (raw.Trim().Length == 0) continue;
            if (raw.Contains('\t')) throw Err("tab", "tabs are not allowed", "indent with two spaces");
            int indent = raw.Length - raw.TrimStart(' ').Length;
            var text = raw.Trim();
            if (fn == null)
            {
                if (indent != 0 || !text.StartsWith("const ", StringComparison.Ordinal))
                    throw Err("missing-signature", "the first line must declare the constant", "start the file with `const name Type`");
                Lex(text);
                _pos++; // const
                fn = new Fn { File = _file, Line = _line, IsConst = true, Name = Expect(T.Ident, "a constant name").Text };
                fn.Ret = ParseType();
                ExpectEnd();
                continue;
            }
            if (indent != 2) throw Err("bad-indent", $"expected an indent of 2 spaces, found {indent}");
            if (fn.Doc == "")
            {
                if (!text.StartsWith('"') || !text.EndsWith('"') || text.Length < 3)
                    throw Err("missing-doc", "line 2 must be the purpose of the constant", "add a one-line description in double quotes as line 2");
                fn.Doc = text[1..^1];
                continue;
            }
            if (fn.Body.Count > 0) throw Err("unexpected-line", $"unexpected line `{text}`", "a constant has exactly one `value` line");
            if (!text.StartsWith("value ", StringComparison.Ordinal))
                throw Err("unexpected-line", $"unknown line `{text}`", "after the purpose line write `value <expression>`");
            fn.EffectsLine = _line;
            fn.Body.Add(new Stmt { Line = _line, Value = ParseExprText(text["value ".Length..]) });
        }
        _line = Math.Max(1, lines.Length);
        if (fn == null) throw Err("missing-signature", "the file is empty", "start the file with `const name Type`");
        if (fn.Doc == "") throw Err("missing-doc", "the constant has no purpose line", "add a one-line description in double quotes as line 2");
        if (fn.Body.Count == 0) throw Err("missing-value", "the constant has no `value` line", "add a line `value <expression>`");
        return fn;
    }
}
