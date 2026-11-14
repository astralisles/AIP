"""Times `aip query` on generated projects (each function calls the previous one plus 2 random earlier ones).
Usage: python scripts/bench-query.py <empty work dir> [sizes e.g. 1000,5000,20000,50000] [out.txt]
Build the compiler first (scripts/aip.ps1 check examples/hello). Run from bash or python directly;
timing from PowerShell can be wrong because capturing output is slow.
Columns: first check = first run after the files are written (on Windows mostly the antivirus scanning
new files, a one-off); no cache = query with build/sources.cache deleted; cached = nothing changed;
after 1 edit = one file changed since the last query (the usual case while an LLM works)."""
import os, random, subprocess, sys, time, statistics, shutil
work = sys.argv[1]; sizes = [int(x) for x in (sys.argv[2] if len(sys.argv) > 2 else "1000,5000,20000,50000").split(",")]
out = sys.argv[3] if len(sys.argv) > 3 else None
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
exe = os.path.join(root, "compiler", "Aip.Compiler", "bin", "Release", "net10.0", "aip.exe")
random.seed(1)
def run(*a):
    t = time.perf_counter(); r = subprocess.run([exe, *a], capture_output=True, text=True); return (time.perf_counter() - t) * 1000, r
def med(f, k=5): return statistics.median(f() for _ in range(k))
lines = ["functions | first check ms | query ms: no cache | cached | after 1 edit (median of 5) | sources on disk"]
for n in sizes:
    d = os.path.join(work, f"p{n}"); os.makedirs(d, exist_ok=True)
    for i in range(n):
        calls = ["printLine(\"hi\")"] if i == 0 else [f"f{i-1}(x)"] + [f"f{random.randrange(i)}(x)" for _ in range(2) if i > 2]
        with open(os.path.join(d, f"f{i}.fn"), "w", newline="\n") as f:
            f.write(f'fn f{i}(x I64) -> Unit\n  "Generated function {i}."\n  effects terminal.write\nbody\n  ' + "\n  ".join(calls) + "\n")
    with open(os.path.join(d, "main.fn"), "w", newline="\n") as f:
        f.write(f'fn main() -> Unit\n  "Entry."\n  effects terminal.write\nbody\n  f{n-1}(1)\n')
    mb = sum(os.path.getsize(os.path.join(d, x)) for x in os.listdir(d) if x.endswith(".fn")) / 1e6
    ms_check, r = run("check", d)
    if r.returncode != 0: lines.append(f"{n} | generated project fails to check: {r.stdout[:120]}"); continue
    cache = os.path.join(d, "build", "sources.cache")
    def nocache():
        if os.path.exists(cache): os.remove(cache)
        return run("query", d, "callers", "f0")[0]
    edits = iter(range(10**6))
    def edited():
        k = next(edits); p = os.path.join(d, "f1.fn")  # change the purpose text, as an LLM edit would
        with open(p, "w", newline="\n") as f: f.write(f'fn f1(x I64) -> Unit\n  "Edited {k}."\n  effects terminal.write\nbody\n  f0(x)\n')
        return run("query", d, "callers", "f0")[0]
    t_no = med(nocache); run("query", d, "list")
    t_cached = med(lambda: run("query", d, "callers", "f0")[0])
    t_edit = med(edited)
    lines.append(f"{n} | {ms_check:.0f} | {t_no:.0f} | {t_cached:.0f} | {t_edit:.0f} | {mb:.1f} MB")
    print(lines[-1], flush=True)
    shutil.rmtree(d)
print("\n".join(lines))
if out: open(out, "w").write("\n".join(lines) + "\n")
