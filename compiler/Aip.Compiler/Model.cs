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

sealed class IntLit : Expr { public ulong Value; }
sealed class FloatLit : Expr { public double Value; }
sealed class BoolLit : Expr { public bool Value; }
/// <summary>Parts are either string (literal text) or Expr (interpolated).</summary>
sealed class TextLit : Expr { public List<object> Parts = new(); }
/// <summary>A local name, or (when Const is set by the checker) a `.const` file.</summary>
sealed class NameRef : Expr { public string Name = ""; public bool Erased; public bool Const; }
/// <summary>A function call, or (when Ctor is set by the checker) the construction of a struct or variant case.</summary>
sealed class Call : Expr { public string Name = ""; public List<Expr> Args = new(); public string? Ctor; public bool Erased; }
/// <summary>`target.name`: reads a field of a struct.</summary>
sealed class Field : Expr { public Expr Target = null!; public string Name = ""; public bool Erased; }
/// <summary>`set target.name = value`: overwrites a field of a struct in place (needs the `mutate` effect). Unit.</summary>
sealed class Assign : Expr { public Field Target = null!; public Expr Value = null!; }
/// <summary>One line of a match: `Case(a, b) => body`. Path is the case's type path for the back end.</summary>
sealed class Arm { public int Line; public string Case = ""; public List<string> Binds = new(); public Expr Body = null!; public string Path = ""; }
/// <summary>`match subject` followed by one indented arm per case of a variant.</summary>
sealed class MatchExpr : Expr { public Expr Subject = null!; public List<Arm> Arms = new(); }
sealed class Unary : Expr { public string Op = ""; public Expr Operand = null!; }
sealed class Binary : Expr { public string Op = ""; public Expr Left = null!; public Expr Right = null!; }
/// <summary>The indented lines of one branch of a multi-line `if`: Unit statements run in order.</summary>
sealed class BlockExpr : Expr { public List<Expr> Stmts = new(); }
sealed class IfExpr : Expr { public Expr Cond = null!; public Expr Then = null!; public Expr Else = null!; }

/// <summary>One shape of a type: all of a struct, or one case of a variant.</summary>
sealed class Case { public string Name = ""; public int Line; public List<Param> Fields = new(); }

/// <summary>A `.struct` (one case named like the type) or a `.variant` (one or more cases).</summary>
sealed class TypeDef
{
    public string Name = "";
    public string File = "";
    public int Line = 1;
    public string Doc = "";
    public bool IsVariant;
    /// <summary>Type parameters of a generic type, e.g. `T` in `variant Option<T>`.</summary>
    public List<string> Params = new();
    public List<Case> Cases = new();
}

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
    /// <summary>`native "lib" "symbol"` header line: the function is a symbol in a shared library (dll, so, dylib). Null otherwise.</summary>
    public string? NativeLib;
    public string NativeSymbol = "";
    public int NativeLine;
    /// <summary>A `.const` file: no parameters, Ret is its type, Body is one statement (its value), always pure.</summary>
    public bool IsConst;
    public List<Param> Params = new();
    public TypeRef Ret = new("Unit", "");
    public string Doc = "";
    public int EffectsLine;
    public List<string> Effects = new();
    public List<Expr> Requires = new();
    public List<Expr> Ensures = new();
    public List<Expr> Examples = new();
    /// <summary>`decreases param` header line: the parameter that shrinks on every recursive call (T-0966). Null when absent.</summary>
    public Expr? Decreases;
    public int DecreasesLine;
    /// <summary>`cannot-fail` header line: the function promises never to crash and always to finish (T-0966).</summary>
    public bool CannotFail;
    /// <summary>`fails` header line: the function can crash at run time (T-0966).</summary>
    public bool Fails;
    public int RestraintLine;
    public List<Stmt> Body = new();
    public HashSet<string> Calls = new();

    public string Signature => IsConst ? $"const {Name} {Ret}" :
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
    /// <summary>"error" or "warning" (warnings come from `aip lint`).</summary>
    public string Kind = "error";

    public override string ToString()
    {
        var rel = Path.GetRelativePath(Environment.CurrentDirectory, File).Replace('\\', '/');
        var s = $"{rel}:{Line}: {Kind} {Code}: {Message}";
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
    public Dictionary<string, TypeDef> Types = new();
    /// <summary>Constructor name (struct name or variant case name) to its type and case.</summary>
    public Dictionary<string, (TypeDef Def, Case Case)> Ctors = new();
    public IEnumerable<Fn> ProjectFns => Fns.Values.Where(f => !f.IsStd).OrderBy(f => f.Name, StringComparer.Ordinal);
}

/// <summary>A back end turns a checked program into something runnable for a target.</summary>
interface IBackend
{
    string Name { get; }
    /// <summary>Returns the path of the built program, or null after printing why it failed.</summary>
    string? Build(ProgramModel program, string outDir, string target);
}
