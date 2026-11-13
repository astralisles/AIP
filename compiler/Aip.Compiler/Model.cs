namespace Aip;

sealed record TypeRef(string Name, string Range)
{
    public override string ToString() => Range == "" ? Name : $"{Name} {Range}";
}

sealed record Param(string Name, TypeRef Type);

abstract class Expr
{
    public int Line;
    public string Type = "";
}

sealed class IntLit : Expr { public long Value; }
sealed class BoolLit : Expr { public bool Value; }
/// <summary>Parts are either string (literal text) or Expr (interpolated).</summary>
sealed class TextLit : Expr { public List<object> Parts = new(); }
sealed class NameRef : Expr { public string Name = ""; }
sealed class Call : Expr { public string Name = ""; public List<Expr> Args = new(); }
sealed class Unary : Expr { public string Op = ""; public Expr Operand = null!; }
sealed class Binary : Expr { public string Op = ""; public Expr Left = null!; public Expr Right = null!; }
sealed class IfExpr : Expr { public Expr Cond = null!; public Expr Then = null!; public Expr Else = null!; }

sealed class Stmt
{
    public int Line;
    public string? Let;
    public Expr Value = null!;
}

sealed class Fn
{
    public string Name = "";
    public string File = "";
    public int Line = 1;
    public bool IsStd;
    public bool IsExtern;
    public List<Param> Params = new();
    public TypeRef Ret = new("Unit", "");
    public string Doc = "";
    public int EffectsLine;
    public List<string> Effects = new();
    public List<Expr> Requires = new();
    public List<Expr> Ensures = new();
    public List<Expr> Examples = new();
    public List<Stmt> Body = new();
    public HashSet<string> Calls = new();

    public string Signature =>
        $"{Name}({string.Join(", ", Params.Select(p => $"{p.Name} {p.Type}"))}) -> {Ret}";

    public string EffectsText => Effects.Count == 0 ? "pure" : string.Join(", ", Effects);
}

sealed class Diagnostic(string file, int line, string code, string message, string? fix = null)
{
    public string File = file;
    public int Line = line;
    public string Code = code;
    public string Message = message;
    public string? Fix = fix;

    public override string ToString()
    {
        var rel = Path.GetRelativePath(Environment.CurrentDirectory, File).Replace('\\', '/');
        var s = $"{rel}:{Line}: error {Code}: {Message}";
        return Fix == null ? s : s + "\n  fix: " + Fix;
    }
}

sealed class CompileException(Diagnostic d) : Exception(d.Message)
{
    public Diagnostic Diagnostic = d;
}

sealed class ProgramModel
{
    public string Name = "";
    public string Dir = "";
    public Dictionary<string, Fn> Fns = new();
    public IEnumerable<Fn> ProjectFns => Fns.Values.Where(f => !f.IsStd).OrderBy(f => f.Name, StringComparer.Ordinal);
}

/// <summary>A back end turns a checked program into something runnable for a target.</summary>
interface IBackend
{
    string Name { get; }
    /// <summary>Returns the path of the built program, or null after printing why it failed.</summary>
    string? Build(ProgramModel program, string outDir, string target);
}
