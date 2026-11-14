namespace Aip;

sealed partial class CsBackend
{
    /// <summary>OpenGL ES 2 / WebGL 1 subset (T-0986). All effectful; implementation in CsBackend.Runtime.Gfx.cs.</summary>
    static void AddGfxExterns(Dictionary<string, string> d)
    {
        d["gfxOpen"] = "Rt.GfxOpen({0}, {1}, {2}, {3})";
        d["gfxClose"] = "Rt.GfxClose({0})";
        d["gfxShouldClose"] = "Rt.GfxShouldClose({0})";
        d["gfxSwap"] = "Rt.GfxSwap({0})";
        d["gfxClear"] = "Rt.GfxClear({0}, {1}, {2}, {3}, {4})";
        d["gfxViewport"] = "Rt.GfxViewport({0}, {1}, {2}, {3}, {4})";
        d["gfxCompile"] = "Rt.GfxCompile({0}, {1}, {2})";
        d["gfxUse"] = "Rt.GfxUse({0}, {1})";
        d["gfxBuffer"] = "Rt.GfxBuffer({0}, {1})";
        d["gfxAttrib"] = "Rt.GfxAttrib({0}, {1}, {2}, {3}, {4}, {5}, {6})";
        d["gfxUniform"] = "Rt.GfxUniform({0}, {1}, {2}, {3})";
        d["gfxUniformInt"] = "Rt.GfxUniformInt({0}, {1}, {2}, {3})";
        d["gfxTexture"] = "Rt.GfxTexture({0}, {1}, {2}, {3})";
        d["gfxTextureBind"] = "Rt.GfxTextureBind({0}, {1}, {2})";
        d["gfxDraw"] = "Rt.GfxDraw({0}, {1}, {2}, {3})";
        d["gfxReadPixels"] = "Rt.GfxReadPixels({0}, {1}, {2}, {3}, {4})";
    }
}
