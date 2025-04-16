using System.Runtime.InteropServices;


namespace Content.Server.FloofStation.SpaceComputer.VirtualCPU;

// Like a C union, but more verbose
[StructLayout(LayoutKind.Explicit)]
public struct CPUMemoryCell
{
    [FieldOffset(0)]
    public uint UInt32;

    [FieldOffset(0)]
    public int Int32;

    [FieldOffset(0)]
    public float Single;

    /// <remarks>Use with caution, may lose some bits of data.</remarks>
    public char Char => (char) Int32;

    public static CPUMemoryCell FromUInt32(uint x) => new() { UInt32 = x };

    public static CPUMemoryCell FromInt32(int x) => new() { Int32 = x };

    public static CPUMemoryCell FromSingle(float x) => new() { Single = x };

    /// <remarks>Use with caution, may lose some bits of data.</remarks>
    public static CPUMemoryCell FromChar(char x) => new() { Int32 = x };
}
