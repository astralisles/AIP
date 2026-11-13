using System.Text;

namespace Aip;

/// <summary>Token helpers and the lexer.</summary>
sealed partial class Parser
{
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
                var kind = T.Int;
                if (j + 1 < s.Length && s[j] == '.' && char.IsAsciiDigit(s[j + 1]))
                {
                    kind = T.Float;
                    j++;
                    while (j < s.Length && char.IsAsciiDigit(s[j])) j++;
                }
                toks.Add(new Tok(kind, s[i..j]));
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
