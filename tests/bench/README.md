# tests/bench: does a model write AIP from the spec alone?

Open question 1 of `docs/design/open-questions-T-0944.md`. One folder per task: `task.md` (what to build) and
`expected-output.txt` (exact output of `aip run`). Not run by `scripts\test.ps1`: it needs a model.

How to run one task with a fresh model (no knowledge of this repo):
1. Give it `task.md` and `expected-output.txt`, an empty project folder, and one rule: learn the language only
   through `scripts\bench-attempt.ps1 <projectDir> spec|find|check|run`; read no other file of the repo.
2. It stops when `run` prints the expected output (or gives up).
3. `scripts\bench-attempt.ps1 -Summary <folder of projects>` prints attempts per task from the logs; compare the
   final `run` output with `expected-output.txt` yourself. Results so far: `docs/design/right-path-T-0974.md`.