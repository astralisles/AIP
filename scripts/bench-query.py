"""Times `aip query` on generated projects (each function calls the previous one plus 2 random earlier ones).
Usage: python scripts/bench-query.py <empty work dir> [sizes e.g. 1000,5000,20000,50000] [out.txt]
Run from bash or python directly; timing from PowerShell can be wrong because capturing output is slow."""
import os, random, subprocess, sys, time, statistics, shutil
work = sys.argv[1]; sizes = [int(x) for x in (sys.argv[2] if len(sys.argv) > 2 else "1000,5000,20000,50000").split(",")]
out = sys.argv[3] if len(sys.argv) > 3 else None
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
exe = os.path.join(root, "compiler", "Aip.Compiler", "bin", "Release", "net10.0", "aip.exe")
random.seed(1)
def run(*a):
    t = time.perf_counter(); r = subprocess.run([exe, *a], capture_output=True, text=True); return (time.perf_counter() - t) * 1000, r
lines = ["functions | check ms | query ms (median of 5) | project size on disk"]
for n in sizes:
    d = os.path.join(work, f"p{n}"); os.makedirs(d, exist_ok=True)
    for i in range(n):
        calls = ["printLine(\"hi\")"] if i == 0 else [f"f{i-1}(x)"] + [f"f{random.randrange(i)}(x)" for _ in range(2) if i > 2]
        with open(os.path.join(d, f"f{i}.fn"), "w", newline="\n") as f:
            f.write(f'fn f{i}(x Int) -> Unit\n  "Generated function {i}."\n  effects terminal.write\nbody\n  ' + "\n  ".join(calls) + "\n")
    with open(os.path.join(d, "main.fn"), "w", newline="\n") as f:
        f.write(f'fn main() -> Unit\n  "Entry."\n  effects terminal.write\nbody\n  f{n-1}(1)\n')
    ms_check, r = run("check", d)
    if r.returncode != 0: lines.append(f"{n} | generated project fails to check: {r.stdout[:120]}"); continue
    qs = [run("query", d, "callers", "f0")[0] for _ in range(5)]
    mb = sum(os.path.getsize(os.path.join(d, x)) for x in os.listdir(d)) / 1e6
    lines.append(f"{n} | {ms_check:.0f} | {statistics.median(qs):.0f} | {mb:.1f} MB")
    shutil.rmtree(d)
print("\n".join(lines))
if out: open(out, "w").write("\n".join(lines) + "\n")
