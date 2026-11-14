using System.Text;

namespace Aip;

/// <summary>Writes a Windows x64 console .exe (PE32+) byte by byte: no linker, no C# compiler.
/// Three sections: .text (code), .rdata (import table, then text literals), .bss (globals, then the heap).</summary>
static class PeWriter
{
    public const int HeapSize = 64 * 1024 * 1024;
    /// <summary>Offsets inside .bss: the heap pointer, the heap end, then the heap itself.</summary>
    public const int BssHeapPtr = 0, BssHeapEnd = 8, BssHeap = 16;
    public const int StdHandleSlot = 0, WriteFileSlot = 1, ExitProcessSlot = 2;
    static readonly string[] Imports = ["GetStdHandle", "WriteFile", "ExitProcess"];
    const ulong ImageBase = 0x140000000;
    const int SectionAlign = 0x1000, FileAlign = 0x200, HeadersSize = 0x200;
    // .rdata layout: 2 descriptors (40), ILT (8 per import + 8), IAT (same), DLL name, hint/name entries, then literals.
    const int IltOffset = 40;
    static readonly int IatOffset = IltOffset + (Imports.Length + 1) * 8;
    static readonly int NameOffset = IatOffset + (Imports.Length + 1) * 8;
    /// <summary>Offset inside .rdata of the first import address table slot of `slot`.</summary>
    public static int IatSlot(int slot) => IatOffset + slot * 8;
    /// <summary>Size of the import block; text literals start here (8-byte aligned).</summary>
    public static readonly int ImportSize = Align(HintNames(0).Length, 8);

    static int Align(int v, int a) => (v + a - 1) / a * a;

    static byte[] HintNames(int rdataRva)
    {
        // DLL name, then one hint/name entry per import. RVAs of the entries are needed for the ILT/IAT, so this
        // returns the whole block from NameOffset; callers slice it.
        var ms = new MemoryStream();
        ms.Write(Encoding.ASCII.GetBytes("KERNEL32.dll\0"));
        while (ms.Length % 2 != 0) ms.WriteByte(0);
        _ = rdataRva;
        foreach (var n in Imports)
        {
            ms.WriteByte(0); ms.WriteByte(0);
            ms.Write(Encoding.ASCII.GetBytes(n + "\0"));
            while (ms.Length % 2 != 0) ms.WriteByte(0);
        }
        var all = new byte[NameOffset + (int)ms.Length];
        ms.ToArray().CopyTo(all, NameOffset);
        return all;
    }

    static void W16(List<byte> o, int v) { o.Add((byte)v); o.Add((byte)(v >> 8)); }
    static void W32(List<byte> o, long v) { for (int i = 0; i < 4; i++) o.Add((byte)(v >> (8 * i))); }
    static void W64(List<byte> o, ulong v) { for (int i = 0; i < 8; i++) o.Add((byte)(v >> (8 * i))); }
    static void Name8(List<byte> o, string s) { var b = Encoding.ASCII.GetBytes(s); o.AddRange(b); for (int i = b.Length; i < 8; i++) o.Add(0); }

    /// <summary>Links: patches every RIP-relative field of <paramref name="asm"/> and returns the file bytes.
    /// <paramref name="literals"/> is placed after the import block in .rdata. Execution starts at code offset 0.</summary>
    public static byte[] Build(X64 asm, byte[] literals)
    {
        var code = asm.Code.ToArray();
        var textRva = SectionAlign;
        var rdataRva = textRva + Align(code.Length, SectionAlign);
        var rdata = new byte[ImportSize + literals.Length];
        var bssRva = rdataRva + Align(rdata.Length, SectionAlign);
        var bssSize = BssHeap + HeapSize;
        var imageSize = bssRva + Align(bssSize, SectionAlign);

        // Link the code to its data.
        foreach (var (at, seg, off) in asm.Fixups)
        {
            long target = seg switch { Seg.Text => textRva, Seg.Rdata => rdataRva, _ => bssRva } + off;
            var rel = (int)(target - (textRva + at + 4));
            for (int i = 0; i < 4; i++) code[at + i] = (byte)(rel >> (8 * i));
        }

        // Import table.
        var hn = HintNames(rdataRva);
        hn.CopyTo(rdata, 0);
        var imp = new List<byte>();
        W32(imp, rdataRva + IltOffset); W32(imp, 0); W32(imp, 0); W32(imp, rdataRva + NameOffset); W32(imp, rdataRva + IatOffset);
        for (int i = 0; i < 20; i++) imp.Add(0);
        imp.CopyTo(rdata, 0);
        var entry = NameOffset + Align("KERNEL32.dll\0".Length, 2);
        for (int i = 0; i < Imports.Length; i++)
        {
            var rva = (ulong)(rdataRva + entry);
            for (int b = 0; b < 8; b++) { rdata[IltOffset + i * 8 + b] = (byte)(rva >> (8 * b)); rdata[IatOffset + i * 8 + b] = (byte)(rva >> (8 * b)); }
            entry += Align(2 + Imports[i].Length + 1, 2);
        }
        literals.CopyTo(rdata, ImportSize);

        var codeRaw = Align(code.Length, FileAlign);
        var rdataRaw = Align(rdata.Length, FileAlign);
        var o = new List<byte>();
        // DOS header: "MZ" and the offset of the PE header (0x40).
        o.Add((byte)'M'); o.Add((byte)'Z');
        while (o.Count < 0x3C) o.Add(0);
        W32(o, 0x40);
        o.AddRange("PE\0\0"u8.ToArray());
        W16(o, 0x8664); W16(o, 3); W32(o, 0); W32(o, 0); W32(o, 0); W16(o, 0xF0); W16(o, 0x0022);
        // Optional header (PE32+).
        W16(o, 0x20B); o.Add(14); o.Add(0);
        W32(o, codeRaw); W32(o, rdataRaw); W32(o, bssSize);
        W32(o, textRva); W32(o, textRva); // entry point = start of .text, base of code
        W64(o, ImageBase); W32(o, SectionAlign); W32(o, FileAlign);
        W16(o, 6); W16(o, 0); W16(o, 0); W16(o, 0); W16(o, 6); W16(o, 0); W32(o, 0);
        W32(o, imageSize); W32(o, HeadersSize); W32(o, 0);
        W16(o, 3); W16(o, 0x8100);                 // console; NX compatible, terminal server aware (fixed base, so no relocations)
        W64(o, 256UL * 1024 * 1024); W64(o, 1024 * 1024); W64(o, 1024 * 1024); W64(o, 4096);
        W32(o, 0); W32(o, 16);
        for (int i = 0; i < 16; i++)
        {
            if (i == 1) { W32(o, rdataRva); W32(o, 40); } else { W32(o, 0); W32(o, 0); }
        }
        // Section headers.
        void Section(string name, int vsize, int rva, int rawSize, int rawPtr, uint flags)
        {
            Name8(o, name); W32(o, vsize); W32(o, rva); W32(o, rawSize); W32(o, rawPtr); W32(o, 0); W32(o, 0); W16(o, 0); W16(o, 0); W32(o, flags);
        }
        Section(".text", code.Length, textRva, codeRaw, HeadersSize, 0x60000020);
        Section(".rdata", rdata.Length, rdataRva, rdataRaw, HeadersSize + codeRaw, 0x40000040);
        Section(".bss", bssSize, bssRva, 0, 0, 0xC0000080);
        while (o.Count < HeadersSize) o.Add(0);
        o.AddRange(code); while (o.Count < HeadersSize + codeRaw) o.Add(0);
        o.AddRange(rdata); while (o.Count < HeadersSize + codeRaw + rdataRaw) o.Add(0);
        return o.ToArray();
    }
}
