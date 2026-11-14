namespace Aip;

/// <summary>
/// `loop` block (T-0994): `name = start` lines, then `body` and its lines. The last body line is the step: an `if` chain
/// whose leaves are `done value` (leave the loop with a result) or `next a, b` (the next value of every name, in order).
/// </summary>
sealed class LoopExpr : Expr
{
    public List<string> Names = new();
    public List<int> NameLines = new();
    public List<Expr> Inits = new();
    public List<string> StateTypes = new();
    public List<Stmt> Body = new();
    public IEnumerable<Expr> Parts => Inits.Concat(Body.Select(s => s.Value));
}

/// <summary>`for name in from..to`: runs the indented Unit statements for name = from, from+1, ... to-1 (Int only).</summary>
sealed class ForExpr : Expr
{
    public string Var = "";
    public Expr From = null!;
    public Expr To = null!;
    public List<Stmt> Body = new();
    public IEnumerable<Expr> Parts => new[] { From, To }.Concat(Body.Select(s => s.Value));
}

/// <summary>`done value`: leaves the enclosing `loop` with this value.</summary>
sealed class DoneExpr : Expr { public Expr Value = null!; }

/// <summary>`next a, b`: runs the body again with these values for the loop's names.</summary>
sealed class NextExpr : Expr { public List<Expr> Args = new(); }
