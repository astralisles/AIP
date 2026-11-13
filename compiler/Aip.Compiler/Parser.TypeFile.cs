using System.Text;

namespace Aip;

/// <summary>Parses a .type file (struct or variant).</summary>
sealed partial class Parser
{
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
}
