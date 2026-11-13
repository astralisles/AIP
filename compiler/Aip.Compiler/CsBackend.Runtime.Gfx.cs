namespace Aip;

sealed partial class CsBackend
{
    /// <summary>
    /// Graphics runtime (T-0986): an OpenGL ES 2.0 / WebGL 1 subset. Desktop Windows is implemented with plain
    /// P/Invoke into user32, gdi32 and opengl32 (no package, no unsafe). Other OSes throw a clear error until a
    /// GLFW/EGL window is added; a browser back end maps each call 1:1 to WebGL (see docs/design/opengl-T-0986.md).
    /// </summary>
    const string RtGfx = """
        public static partial class Rt
        {
            public sealed class GfxWin
            {
                public System.IntPtr Hwnd, Hdc, Ctx;
                public int Width, Height;
            }

            static void GfxNeedWindows()
            {
                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    throw new System.PlatformNotSupportedException("gfx: this OS has no OpenGL window back end yet (Windows desktop only for now)");
            }

            public static object GfxOpen(string title, long width, long height, bool visible)
            {
                GfxNeedWindows();
                if (width < 1 || height < 1 || width > 16384 || height > 16384) throw new System.ArgumentException("gfxOpen: size must be 1..16384");
                return GfxWinApi.Open(title, (int)width, (int)height, visible);
            }
            public static void GfxClose(object w) => GfxWinApi.Close((GfxWin)w);
            public static bool GfxShouldClose(object w) => GfxWinApi.ShouldClose((GfxWin)w);
            public static void GfxSwap(object w) => GfxWinApi.Swap((GfxWin)w);

            static GfxWin Cur(object w) { var g = (GfxWin)w; GfxWinApi.MakeCurrent(g); return g; }

            public static void GfxClear(object w, double r, double g, double b, double a)
            {
                Cur(w);
                GfxWinApi.glClearColor((float)r, (float)g, (float)b, (float)a);
                GfxWinApi.glClear(0x4100); // COLOR_BUFFER_BIT | DEPTH_BUFFER_BIT
            }
            public static void GfxViewport(object w, long x, long y, long width, long height)
            {
                Cur(w);
                GfxWinApi.glViewport((int)x, (int)y, (int)width, (int)height);
            }
            public static long GfxCompile(object w, string vertex, string fragment)
            {
                Cur(w);
                uint vs = GfxWinApi.Shader(0x8B31, vertex), fs = GfxWinApi.Shader(0x8B30, fragment);
                var p = GfxWinApi.Fn<GfxWinApi.D_Create>("glCreateProgram")();
                GfxWinApi.Fn<GfxWinApi.D_Attach>("glAttachShader")(p, vs);
                GfxWinApi.Fn<GfxWinApi.D_Attach>("glAttachShader")(p, fs);
                GfxWinApi.Fn<GfxWinApi.D_Void1>("glLinkProgram")(p);
                GfxWinApi.Fn<GfxWinApi.D_Void1>("glDeleteShader")(vs);
                GfxWinApi.Fn<GfxWinApi.D_Void1>("glDeleteShader")(fs);
                GfxWinApi.Fn<GfxWinApi.D_GetiV>("glGetProgramiv")(p, 0x8B82, out int ok);
                if (ok == 0)
                {
                    var sb = new System.Text.StringBuilder(4096);
                    GfxWinApi.Fn<GfxWinApi.D_Log>("glGetProgramInfoLog")(p, 4096, out _, sb);
                    throw new System.InvalidOperationException("gfxCompile: link failed: " + sb);
                }
                return p;
            }
            public static void GfxUse(object w, long program)
            {
                Cur(w);
                GfxWinApi.Fn<GfxWinApi.D_Void1>("glUseProgram")((uint)program);
            }
            public static long GfxBuffer(object w, Aip.Persistent.PList values)
            {
                Cur(w);
                var data = new float[values.Count];
                for (int i = 0; i < data.Length; i++) data[i] = (float)(double)values[i];
                GfxWinApi.Fn<GfxWinApi.D_GenN>("glGenBuffers")(1, out uint id);
                GfxWinApi.Fn<GfxWinApi.D_Bind>("glBindBuffer")(0x8892, id);
                GfxWinApi.Fn<GfxWinApi.D_BufData>("glBufferData")(0x8892, (System.IntPtr)(data.Length * 4), data, 0x88E4);
                return id;
            }
            public static void GfxAttrib(object w, long program, string name, long buffer, long size, long stride, long offset)
            {
                Cur(w);
                if (size < 1 || size > 4) throw new System.ArgumentException("gfxAttrib: size must be 1..4");
                int loc = GfxWinApi.Fn<GfxWinApi.D_Loc>("glGetAttribLocation")((uint)program, name);
                if (loc < 0) throw new System.ArgumentException("gfxAttrib: the program has no attribute " + name);
                GfxWinApi.Fn<GfxWinApi.D_Bind>("glBindBuffer")(0x8892, (uint)buffer);
                GfxWinApi.Fn<GfxWinApi.D_Void1>("glEnableVertexAttribArray")((uint)loc);
                GfxWinApi.Fn<GfxWinApi.D_Ptr>("glVertexAttribPointer")((uint)loc, (int)size, 0x1406, 0, (int)stride * 4, (System.IntPtr)(offset * 4));
            }
            public static void GfxUniform(object w, long program, string name, Aip.Persistent.PList values)
            {
                Cur(w);
                int loc = GfxWinApi.Fn<GfxWinApi.D_Loc>("glGetUniformLocation")((uint)program, name);
                if (loc < 0) throw new System.ArgumentException("gfxUniform: the program has no uniform " + name);
                var v = new float[values.Count];
                for (int i = 0; i < v.Length; i++) v[i] = (float)(double)values[i];
                switch (v.Length)
                {
                    case 1: GfxWinApi.Fn<GfxWinApi.D_U1>("glUniform1f")(loc, v[0]); break;
                    case 2: GfxWinApi.Fn<GfxWinApi.D_U2>("glUniform2f")(loc, v[0], v[1]); break;
                    case 3: GfxWinApi.Fn<GfxWinApi.D_U3>("glUniform3f")(loc, v[0], v[1], v[2]); break;
                    case 4: GfxWinApi.Fn<GfxWinApi.D_U4>("glUniform4f")(loc, v[0], v[1], v[2], v[3]); break;
                    default: throw new System.ArgumentException("gfxUniform: 1 to 4 values, got " + v.Length);
                }
            }
            public static void GfxUniformInt(object w, long program, string name, long value)
            {
                Cur(w);
                int loc = GfxWinApi.Fn<GfxWinApi.D_Loc>("glGetUniformLocation")((uint)program, name);
                if (loc < 0) throw new System.ArgumentException("gfxUniformInt: the program has no uniform " + name);
                GfxWinApi.Fn<GfxWinApi.D_U1i>("glUniform1i")(loc, (int)value);
            }
            public static long GfxTexture(object w, long width, long height, byte[] rgba)
            {
                Cur(w);
                if (width < 1 || height < 1 || (long)rgba.Length != width * height * 4) throw new System.ArgumentException("gfxTexture: pixels must be width*height*4 bytes (RGBA)");
                GfxWinApi.glGenTextures(1, out uint id);
                GfxWinApi.glBindTexture(0x0DE1, id);
                GfxWinApi.glTexParameteri(0x0DE1, 0x2801, 0x2601); // MIN_FILTER LINEAR
                GfxWinApi.glTexParameteri(0x0DE1, 0x2800, 0x2601); // MAG_FILTER LINEAR
                GfxWinApi.glTexParameteri(0x0DE1, 0x2802, 0x812F); // WRAP_S CLAMP_TO_EDGE
                GfxWinApi.glTexParameteri(0x0DE1, 0x2803, 0x812F);
                GfxWinApi.glTexImage2D(0x0DE1, 0, 0x1908, (int)width, (int)height, 0, 0x1908, 0x1401, rgba);
                return id;
            }
            public static void GfxTextureBind(object w, long texture, long unit)
            {
                Cur(w);
                if (unit < 0 || unit > 7) throw new System.ArgumentException("gfxTextureBind: unit must be 0..7");
                GfxWinApi.Fn<GfxWinApi.D_Void1>("glActiveTexture")((uint)(0x84C0 + unit));
                GfxWinApi.glBindTexture(0x0DE1, (uint)texture);
            }
            public static void GfxDraw(object w, string mode, long first, long count)
            {
                Cur(w);
                uint m = mode switch
                {
                    "points" => 0u, "lines" => 1u, "lineStrip" => 3u, "triangles" => 4u, "triangleStrip" => 5u, "triangleFan" => 6u,
                    _ => throw new System.ArgumentException("gfxDraw: mode must be points, lines, lineStrip, triangles, triangleStrip or triangleFan"),
                };
                GfxWinApi.glDrawArrays(m, (int)first, (int)count);
            }
            public static byte[] GfxReadPixels(object w, long x, long y, long width, long height)
            {
                Cur(w);
                if (width < 1 || height < 1 || width > 16384 || height > 16384) throw new System.ArgumentException("gfxReadPixels: size must be 1..16384");
                var buf = new byte[checked((int)(width * height * 4))];
                GfxWinApi.glReadPixels((int)x, (int)y, (int)width, (int)height, 0x1908, 0x1401, buf);
                return buf;
            }
        }

        static class GfxWinApi
        {
            public delegate uint D_Create();
            public delegate uint D_Create1(uint a);
            public delegate void D_Void1(uint a);
            public delegate void D_Attach(uint a, uint b);
            public delegate void D_Bind(uint a, uint b);
            public delegate void D_GenN(int n, out uint id);
            public delegate void D_BufData(uint target, System.IntPtr size, float[] data, uint usage);
            public delegate void D_GetiV(uint o, uint pname, out int v);
            public delegate void D_Log(uint o, int max, out int len, System.Text.StringBuilder sb);
            public delegate void D_Src(uint s, int n, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPArray, ArraySubType = System.Runtime.InteropServices.UnmanagedType.LPStr)] string[] src, int[] len);
            public delegate int D_Loc(uint p, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPStr)] string name);
            public delegate void D_Ptr(uint idx, int size, uint type, byte norm, int stride, System.IntPtr offset);
            public delegate void D_U1(int l, float a);
            public delegate void D_U2(int l, float a, float b);
            public delegate void D_U3(int l, float a, float b, float c);
            public delegate void D_U4(int l, float a, float b, float c, float d);
            public delegate void D_U1i(int l, int a);

            const string G = "opengl32.dll";
            [System.Runtime.InteropServices.DllImport(G)] public static extern void glClearColor(float r, float g, float b, float a);
            [System.Runtime.InteropServices.DllImport(G)] public static extern void glClear(uint mask);
            [System.Runtime.InteropServices.DllImport(G)] public static extern void glViewport(int x, int y, int w, int h);
            [System.Runtime.InteropServices.DllImport(G)] public static extern void glDrawArrays(uint mode, int first, int count);
            [System.Runtime.InteropServices.DllImport(G)] public static extern void glGenTextures(int n, out uint id);
            [System.Runtime.InteropServices.DllImport(G)] public static extern void glBindTexture(uint target, uint id);
            [System.Runtime.InteropServices.DllImport(G)] public static extern void glTexParameteri(uint target, uint pname, int v);
            [System.Runtime.InteropServices.DllImport(G)] public static extern void glTexImage2D(uint target, int level, int internalFormat, int w, int h, int border, uint format, uint type, byte[] data);
            [System.Runtime.InteropServices.DllImport(G)] public static extern void glReadPixels(int x, int y, int w, int h, uint format, uint type, byte[] data);
            [System.Runtime.InteropServices.DllImport(G)] static extern System.IntPtr wglCreateContext(System.IntPtr dc);
            [System.Runtime.InteropServices.DllImport(G)] static extern bool wglMakeCurrent(System.IntPtr dc, System.IntPtr rc);
            [System.Runtime.InteropServices.DllImport(G)] static extern bool wglDeleteContext(System.IntPtr rc);
            [System.Runtime.InteropServices.DllImport(G, CharSet = System.Runtime.InteropServices.CharSet.Ansi)] static extern System.IntPtr wglGetProcAddress(string name);

            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            struct Pfd
            {
                public ushort Size, Version; public uint Flags; public byte PixelType, ColorBits, RedBits, RedShift, GreenBits, GreenShift, BlueBits, BlueShift,
                    AlphaBits, AlphaShift, AccumBits, AccumRedBits, AccumGreenBits, AccumBlueBits, AccumAlphaBits, DepthBits, StencilBits, AuxBuffers, LayerType, Reserved;
                public uint LayerMask, VisibleMask, DamageMask;
            }
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
            struct WndClass
            {
                public uint Size, Style; public System.IntPtr WndProc; public int ClsExtra, WndExtra; public System.IntPtr Instance, Icon, Cursor, Background;
                public string? Menu; public string Name; public System.IntPtr IconSm;
            }
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            struct Rect { public int L, T, R, B; }
            [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
            struct Msg { public System.IntPtr Hwnd; public uint Message; public System.IntPtr W, L; public uint Time; public int X, Y; }

            const string U = "user32.dll", Gd = "gdi32.dll", K = "kernel32.dll";
            [System.Runtime.InteropServices.DllImport(U, CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern ushort RegisterClassExW(ref WndClass c);
            [System.Runtime.InteropServices.DllImport(U, CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern System.IntPtr CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, System.IntPtr parent, System.IntPtr menu, System.IntPtr inst, System.IntPtr param);
            [System.Runtime.InteropServices.DllImport(U)] static extern System.IntPtr GetDC(System.IntPtr hwnd);
            [System.Runtime.InteropServices.DllImport(U)] static extern bool DestroyWindow(System.IntPtr hwnd);
            [System.Runtime.InteropServices.DllImport(U)] static extern bool IsWindow(System.IntPtr hwnd);
            [System.Runtime.InteropServices.DllImport(U)] static extern bool AdjustWindowRect(ref Rect rc, uint style, bool menu);
            [System.Runtime.InteropServices.DllImport(U)] static extern bool PeekMessageW(out Msg m, System.IntPtr hwnd, uint min, uint max, uint remove);
            [System.Runtime.InteropServices.DllImport(U)] static extern System.IntPtr DispatchMessageW(ref Msg m);
            [System.Runtime.InteropServices.DllImport(U)] static extern bool ShowWindow(System.IntPtr hwnd, int cmd);
            [System.Runtime.InteropServices.DllImport(Gd)] static extern int ChoosePixelFormat(System.IntPtr dc, ref Pfd p);
            [System.Runtime.InteropServices.DllImport(Gd)] static extern bool SetPixelFormat(System.IntPtr dc, int fmt, ref Pfd p);
            [System.Runtime.InteropServices.DllImport(Gd)] static extern bool SwapBuffers(System.IntPtr dc);
            [System.Runtime.InteropServices.DllImport(K, CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern System.IntPtr GetModuleHandleW(string? n);
            [System.Runtime.InteropServices.DllImport(K)] static extern System.IntPtr GetProcAddress(System.IntPtr mod, string name);

            const string Cls = "AipGfxWindow";
            static bool registered;
            static Rt.GfxWin? current;
            static readonly System.Collections.Generic.Dictionary<string, System.Delegate> cache = new();

            public static T Fn<T>(string name) where T : System.Delegate
            {
                if (cache.TryGetValue(name, out var d)) return (T)d;
                var p = wglGetProcAddress(name);
                if (p == System.IntPtr.Zero) throw new System.InvalidOperationException("gfx: OpenGL function " + name + " is not available (needs OpenGL 2.0)");
                var t = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<T>(p);
                cache[name] = t;
                return t;
            }

            public static uint Shader(uint type, string src)
            {
                // Programs are written once in GLSL ES 1.00 (the WebGL dialect). Desktop GLSL 1.20 has no precision qualifiers: neutralise them.
                var full = "#version 120\n#define precision\n#define highp\n#define mediump\n#define lowp\n" + System.Text.RegularExpressions.Regex.Replace(src, @"precision\s+(lowp|mediump|highp)\s+\w+\s*;", "");
                var s = Fn<D_Create1>("glCreateShader")(type);
                Fn<D_Src>("glShaderSource")(s, 1, new[] { full }, null!);
                Fn<D_Void1>("glCompileShader")(s);
                Fn<D_GetiV>("glGetShaderiv")(s, 0x8B81, out int ok);
                if (ok == 0)
                {
                    var sb = new System.Text.StringBuilder(4096);
                    Fn<D_Log>("glGetShaderInfoLog")(s, 4096, out _, sb);
                    throw new System.InvalidOperationException("gfxCompile: " + (type == 0x8B31 ? "vertex" : "fragment") + " shader failed: " + sb);
                }
                return s;
            }

            public static Rt.GfxWin Open(string title, int width, int height, bool visible)
            {
                var inst = GetModuleHandleW(null);
                if (!registered)
                {
                    var defProc = GetProcAddress(GetModuleHandleW("user32.dll"), "DefWindowProcW");
                    var wc = new WndClass { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<WndClass>(), Style = 0x20 /*OWNDC*/, WndProc = defProc, Instance = inst, Name = Cls };
                    if (RegisterClassExW(ref wc) == 0) throw new System.InvalidOperationException("gfxOpen: RegisterClassEx failed");
                    registered = true;
                }
                const uint style = 0x00CF0000; // WS_OVERLAPPEDWINDOW
                var rc = new Rect { L = 0, T = 0, R = width, B = height };
                AdjustWindowRect(ref rc, style, false);
                // Created hidden and never activated: the window only appears (without taking focus) when `visible` is true.
                var hwnd = CreateWindowExW(0, Cls, title, style, 100, 100, rc.R - rc.L, rc.B - rc.T, System.IntPtr.Zero, System.IntPtr.Zero, inst, System.IntPtr.Zero);
                if (hwnd == System.IntPtr.Zero) throw new System.InvalidOperationException("gfxOpen: CreateWindow failed");
                var dc = GetDC(hwnd);
                var pfd = new Pfd { Size = (ushort)System.Runtime.InteropServices.Marshal.SizeOf<Pfd>(), Version = 1, Flags = 0x4 | 0x20 | 0x1, PixelType = 0, ColorBits = 32, DepthBits = 24 };
                int fmt = ChoosePixelFormat(dc, ref pfd);
                if (fmt == 0 || !SetPixelFormat(dc, fmt, ref pfd)) throw new System.InvalidOperationException("gfxOpen: no usable pixel format");
                var ctx = wglCreateContext(dc);
                if (ctx == System.IntPtr.Zero) throw new System.InvalidOperationException("gfxOpen: wglCreateContext failed");
                var g = new Rt.GfxWin { Hwnd = hwnd, Hdc = dc, Ctx = ctx, Width = width, Height = height };
                MakeCurrent(g);
                if (visible) ShowWindow(hwnd, 4 /*SW_SHOWNOACTIVATE*/);
                glViewport(0, 0, width, height);
                return g;
            }
            public static void MakeCurrent(Rt.GfxWin g)
            {
                if (ReferenceEquals(current, g)) return;
                if (g.Ctx == System.IntPtr.Zero) throw new System.InvalidOperationException("gfx: the window is closed");
                wglMakeCurrent(g.Hdc, g.Ctx);
                current = g;
            }
            public static void Pump()
            {
                while (PeekMessageW(out var m, System.IntPtr.Zero, 0, 0, 1)) DispatchMessageW(ref m);
            }
            public static bool ShouldClose(Rt.GfxWin g) { Pump(); return g.Ctx == System.IntPtr.Zero || !IsWindow(g.Hwnd); }
            public static void Swap(Rt.GfxWin g) { MakeCurrent(g); SwapBuffers(g.Hdc); Pump(); }
            public static void Close(Rt.GfxWin g)
            {
                if (g.Ctx == System.IntPtr.Zero) return;
                wglMakeCurrent(System.IntPtr.Zero, System.IntPtr.Zero);
                wglDeleteContext(g.Ctx);
                if (IsWindow(g.Hwnd)) DestroyWindow(g.Hwnd);
                g.Ctx = System.IntPtr.Zero;
                current = null;
            }
        }
        """;
}
