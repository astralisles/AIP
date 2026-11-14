# OpenGL graphics (T-0986)

Goal: programs draw with OpenGL, and the same program source must compile for desktop, WASM (browser) and other targets.

## Decision: one portable subset, OpenGL ES 2.0 = WebGL 1

Desktop OpenGL, OpenGL ES (mobile, Linux EGL) and WebGL all share the ES 2.0 core. The std group `gfx` exposes only
that subset, so every function maps 1:1 to a WebGL call in a browser back end and to a desktop GL call here.
Left out on purpose because WebGL lacks them: immediate mode (`glBegin`), client-side vertex arrays (data always goes
through a buffer), fixed-function state, `GL_QUADS`, reading/writing GL state by raw number.

- **Handles:** `GfxWindow` is a built-in handle type (like `UdpSocket`); only `gfxOpen` creates one, so holding one proves
  `gfx.window` was declared. Shader programs, buffers and textures are plain `I64` ids.
- **Effects (security rule T-0936):** `gfx.window` (open/close/events/present), `gfx.draw` (anything that changes GPU state
  or draws), `gfx.read` (read pixels back). They show in the index; `aip test` refuses them.
- **Shaders:** written once in GLSL ES 1.00 (the WebGL dialect). Text literals use `\{` and `\}` for GLSL braces. The desktop
  runtime prepends `#version 120` and strips `precision` lines so the same source compiles on desktop GL 2.x.
  Compile or link failure is a `fails` error carrying the driver log.
- **Window:** `gfxOpen(title, w, h, visible)`; `visible=false` is a hidden window (offscreen rendering, tests, screenshots via
  `gfxReadPixels`). A visible window is shown without taking focus. Loop with `gfxShouldClose` / `gfxSwap` (recursion, no `while`).
- **Numbers:** scalars and buffers are `F64` in the signature and become 32-bit floats at the GL boundary (what WebGL uses).

| Function | Effect |
|---|---|
| `gfxOpen`, `gfxClose`, `gfxShouldClose`, `gfxSwap` | `gfx.window` |
| `gfxClear`, `gfxViewport`, `gfxCompile`, `gfxUse`, `gfxBuffer`, `gfxAttrib`, `gfxUniform`, `gfxUniformInt`, `gfxTexture`, `gfxTextureBind`, `gfxDraw` | `gfx.draw` |
| `gfxReadPixels` | `gfx.read` |

## Back ends

- **C# desktop, Windows (done):** plain P/Invoke into `user32`, `gdi32`, `opengl32` (`CsBackend.Runtime.Gfx.cs`); no NuGet package, no
  `unsafe`. Needs OpenGL 2.0 from the driver (any real GPU driver; the Microsoft software fallback is GL 1.1 and fails with a clear message).
- **C# Linux/macOS (not done):** the runtime throws `gfx: this OS has no OpenGL window back end yet`. Planned: GLFW (or X11/EGL) via
  the same `native` loading used by `native-libraries-T-0956.md`; only the window/context part changes, not the std functions.
- **WASM / browser (not done, no WASM back end exists yet):** each `gfx*` is a JS import over WebGL 1; `gfxOpen` binds a `<canvas>`;
  `gfxShouldClose` is always false and `gfxSwap` is a `requestAnimationFrame` yield. A blocking loop will need the future task
  runtime to yield to the browser; that is the one real difference to settle then.
- **Not done in the API:** depth/blending state, index buffers (`gfxDrawElements`), matrix uniforms, keyboard/mouse input (a separate
  `input` group), several windows at once (the runtime tracks one current context), texture parameters, deleting resources.

Example: `examples/gfx` (hidden window, red triangle, pixel read-back; `gfx show` keeps a visible window open). Test: its `expected-output.txt`.
