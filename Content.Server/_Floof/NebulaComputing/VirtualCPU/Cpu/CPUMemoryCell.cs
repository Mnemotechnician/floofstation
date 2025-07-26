using System.Runtime.InteropServices;


namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;

/// <summary>
///    A 4-byte register whose value can be interpreted as 32-bit integer, 32-bit float, or a character.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct CPUMemoryCell
{
    public static CPUMemoryCell Zero => new();
    public static CPUMemoryCell One => FromInt32(1);

    [FieldOffset(0)]
    public uint UInt32;

    [FieldOffset(0)]
    public int Int32;

    [FieldOffset(0)]
    public float Single;

    /// <remarks>Use with caution, may lose some bits of data.</remarks>
    public char Char => (char) Int32;

    public CPUInstructionCell Instruction => CPUInstructionCell.FromUInt32(UInt32);

    static CPUMemoryCell()
    {
        var size = Marshal.SizeOf<CPUMemoryCell>();
        var size2 = Marshal.SizeOf<CPUInstructionCell>();
        if (size != sizeof(int) || size2 != size)
            Logger.Error("CPU memory cell size constraint is violated. This lead to undefined behavior and may crash the game.");
    }

    public static CPUMemoryCell FromUInt32(uint x) => new() { UInt32 = x };

    public static CPUMemoryCell FromInt32(int x) => new() { Int32 = x };

    public static CPUMemoryCell FromSingle(float x) => new() { Single = x };

    /// <remarks>Use with caution, may lose some bits of data.</remarks>
    public static CPUMemoryCell FromChar(char x) => new() { Int32 = x };

    // Mostly for debugging via VV
    public override string ToString() => $"{Int32:x8} | c {Char} | f {Single}";
}
