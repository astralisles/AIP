# Game basics: time, float math, random numbers, tail calls (T-0983)

First step of `game-gaps-T-0983.md` (items 1-3). All names follow one rule: group prefix + verb.

## Time (`std/time`)
- `timeMonotonic() -> I64` and `timeNow() -> I64`: milliseconds, effect `time.read` (breaks replay, so it is an effect).
  Use `timeMonotonic` for frame time (never goes backwards), `timeNow` for the date (since 1970 UTC).
- `timeSleep(ms I64) -> Unit`: effect `time.sleep`, separate from `time.read` as decided in `sleep-T-0981.md`.
  Zero or negative returns at once. Frame pacing: `timeSleep(16 - (timeMonotonic() - frameStart))`.
- Not in `aip test` (effects). Tested by `examples/clock`.

## Float math (`std/math`, all pure, F64)
`mathSqrt mathPow mathExp mathLog mathSin mathCos mathTan mathAtan2 mathFloor mathCeil mathRound mathAbs mathMin mathMax
mathClamp mathLerp mathIsNaN mathPi` and `textToFloat`. `mathFloor/Ceil/Round` return F64; convert with `numFromF64` when an
I64 is needed. `mathRound` rounds halves away from zero. Bad input follows IEEE (`mathSqrt(-1.0)` is NaN, test with
`mathIsNaN`); `mathClamp` with low above high and an unparsable `textToFloat` fail as an `extern-failed` error.
F32 is converted with `numF32ToF64` first (no F32 overloads: one spelling per function).

## Random (`std/random`)
SplitMix64, written so a game can be replayed exactly:
- `randomNext(state) -> I64` is the next state, `randomRange(state, bound) -> I64` is 0..bound-1, `randomFloat(state) -> F64` is 0..1 (never 1).
  All pure. Idiom: `let s1 = randomNext(s0)`, draw from `s1`, pass `s1` on. Drawing twice from the same state gives the same number.
- `randomSeed() -> I64`: effect `random.read`, from the operating system. Start from it for a different game each run, or from a
  constant for a reproducible one (tests, replays, lockstep networking).
- Not cryptographic; use `hash` functions for that.

## Tail calls (back end, no syntax change)
A function whose last statement calls itself (directly, or inside `if`/`else`, a `match` arm or a multi-line `if` block) is emitted
as a `while (true)` loop that reassigns the parameters, so a game loop or a million-step recursion uses constant stack.
Only direct self-calls are lowered; mutual recursion (`a` calls `b` calls `a`) and calls that are not last (`1 + f(n - 1)`) still use
one stack frame per step. `aip test` (interpreter) does not lower tail calls; it already runs on a big stack.
Tested by `examples/tailcalls` and `examples/gamemath`.

## Not done (next in `game-gaps-T-0983.md`)
Mutable `Array`/`Grid`, `Vec2` struct, non-blocking input, mutual-recursion tail calls, `Option`/`Result` returns for `textToFloat`.
