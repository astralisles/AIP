namespace Aip;

/// <summary>Where a RIP-relative reference points: the code itself, read-only data (imports, text literals) or zeroed data.</summary>
enum Seg { Text, Rdata, Bss }

/// <summary>A tiny x86-64 assembler: only the instructions the native back end needs, written as raw bytes
/// (docs/design/native-backend-T-1033.md). Labels are ints; RIP-relative references to data are patched at link time.</summary>
sealed class X64
{
    public const int Rax = 0, Rcx = 1, Rdx = 2, Rbx = 3, Rsp = 4, Rbp = 5, Rsi = 6, Rdi = 7, R8 = 8, R9 = 9, R10 = 10, R11 = 11;
    /// <summary>ALU opcodes of the "op r/m64, r64" form.</summary>
    public const byte Add = 0x01, Or = 0x09, And = 0x21, Sub = 0x29, Xor = 0x31, Cmp = 0x39;
    /// <summary>Condition codes (the low nibble of Jcc / SETcc).</summary>
    public const int CcB = 2, CcE = 4, CcNe = 5, CcA = 7, CcNs = 9, CcL = 0xC, CcGe = 0xD, CcLe = 0xE, CcG = 0xF;

    public readonly List<byte> Code = new();
    /// <summary>A 32-bit RIP-relative field at <c>At</c> that must point at <c>Offset</c> inside <c>Seg</c>.</summary>
    public readonly List<(int At, Seg Seg, int Offset)> Fixups = new();
    readonly List<int> _labels = new();
    readonly List<(int At, int Label)> _jumps = new();

    public int NewLabel() { _labels.Add(-1); return _labels.Count - 1; }
    public void Bind(int label) => _labels[label] = Code.Count;

    /// <summary>Resolves every jump and call to a label. Call once, after all code is written.</summary>
    public void ResolveLabels()
    {
        foreach (var (at, label) in _jumps)
        {
            var target = _labels[label];
            if (target < 0) throw new InvalidOperationException("label never bound");
            Patch32(at, target - (at + 4));
        }
    }

    public void Patch32(int at, int value)
    {
        for (int i = 0; i < 4; i++) Code[at + i] = (byte)(value >> (8 * i));
    }

    void B(params byte[] bytes) => Code.AddRange(bytes);
    void I32(int v) { for (int i = 0; i < 4; i++) Code.Add((byte)(v >> (8 * i))); }
    void Rex(bool w, int reg, int rm) => Code.Add((byte)(0x40 | (w ? 8 : 0) | ((reg >> 3) << 2) | (rm >> 3)));
    void ModRm(int mod, int reg, int rm) => Code.Add((byte)((mod << 6) | ((reg & 7) << 3) | (rm & 7)));

    public void Push(int r) { if (r >= 8) Code.Add(0x41); Code.Add((byte)(0x50 + (r & 7))); }
    public void Pop(int r) { if (r >= 8) Code.Add(0x41); Code.Add((byte)(0x58 + (r & 7))); }
    public void Ret() => B(0xC3);
    public void Leave() => B(0xC9);
    public void Cqo() => B(0x48, 0x99);
    public void RepMovsb() => B(0xF3, 0xA4);

    /// <summary>mov dst, src</summary>
    public void MovRR(int dst, int src) { Rex(true, src, dst); B(0x89); ModRm(3, src, dst); }
    /// <summary>mov dst, imm64</summary>
    public void MovRI(int dst, long imm)
    {
        Rex(true, 0, dst); B((byte)(0xB8 + (dst & 7)));
        for (int i = 0; i < 8; i++) Code.Add((byte)(imm >> (8 * i)));
    }
    /// <summary>op dst, src with op one of Add, Or, And, Sub, Xor, Cmp.</summary>
    public void Alu(byte op, int dst, int src) { Rex(true, src, dst); B(op); ModRm(3, src, dst); }
    /// <summary>op dst, imm32 with ext: 0 add, 1 or, 4 and, 5 sub, 6 xor, 7 cmp.</summary>
    public void AluRI(int ext, int dst, int imm) { Rex(true, 0, dst); B(0x81); ModRm(3, ext, dst); I32(imm); }
    public void Test(int a, int b) { Rex(true, b, a); B(0x85); ModRm(3, b, a); }
    public void Imul(int dst, int src) { Rex(true, dst, src); B(0x0F, 0xAF); ModRm(3, dst, src); }
    /// <summary>F7 group on one register: 2 not, 3 neg, 6 div, 7 idiv.</summary>
    public void GroupF7(int ext, int r) { Rex(true, 0, r); B(0xF7); ModRm(3, ext, r); }
    public void SetCc(int cc) => B(0x0F, (byte)(0x90 + cc), 0xC0);
    public void MovzxAl() => B(0x0F, 0xB6, 0xC0);

    void Mem(byte op, int reg, int baseReg, int disp)
    {
        Rex(true, reg, baseReg); B(op); ModRm(2, reg, baseReg);
        if ((baseReg & 7) == 4) B(0x24);
        I32(disp);
    }
    /// <summary>mov reg, [base+disp]</summary>
    public void Load(int reg, int baseReg, int disp) => Mem(0x8B, reg, baseReg, disp);
    /// <summary>mov [base+disp], reg</summary>
    public void Store(int baseReg, int disp, int reg) => Mem(0x89, reg, baseReg, disp);
    /// <summary>lea reg, [base+disp]</summary>
    public void Lea(int reg, int baseReg, int disp) => Mem(0x8D, reg, baseReg, disp);
    /// <summary>mov byte [base+disp], low byte of reg (reg must be rax, rcx or rdx)</summary>
    public void StoreByte(int baseReg, int disp, int reg)
    {
        Rex(false, reg, baseReg); B(0x88); ModRm(2, reg, baseReg);
        if ((baseReg & 7) == 4) B(0x24);
        I32(disp);
    }

    void RipField(Seg seg, int offset) { Fixups.Add((Code.Count, seg, offset)); I32(0); }
    public void LoadRip(int reg, Seg seg, int offset) { Rex(true, reg, 0); B(0x8B); ModRm(0, reg, 5); RipField(seg, offset); }
    public void StoreRip(Seg seg, int offset, int reg) { Rex(true, reg, 0); B(0x89); ModRm(0, reg, 5); RipField(seg, offset); }
    public void LeaRip(int reg, Seg seg, int offset) { Rex(true, reg, 0); B(0x8D); ModRm(0, reg, 5); RipField(seg, offset); }
    /// <summary>call [rip+x] through an import address table slot.</summary>
    public void CallRip(Seg seg, int offset) { B(0xFF, 0x15); RipField(seg, offset); }

    public void Call(int label) { B(0xE8); _jumps.Add((Code.Count, label)); I32(0); }
    public void Jmp(int label) { B(0xE9); _jumps.Add((Code.Count, label)); I32(0); }
    public void Jcc(int cc, int label) { B(0x0F, (byte)(0x80 + cc)); _jumps.Add((Code.Count, label)); I32(0); }
}
