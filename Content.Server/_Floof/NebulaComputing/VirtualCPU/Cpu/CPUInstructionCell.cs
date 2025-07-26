using System.Runtime.InteropServices;
using Robust.Shared.Utility;


namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;


/// <summary>
///    A subset of CPUMemoryCell, an interpretation of a CPU cell that stores additional info.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public struct CPUInstructionCell
{
    [FieldOffset(0)]
    public uint UInt32;

    /// <summary>
    ///     Byte 1. Encodes the instruction contained in this cell.
    /// </summary>
    [FieldOffset(0)]
    public byte RawOpcode;

    /// <summary>
    ///     Byte 2. Specifies where to fetch arguments from. Each argument has its data location encoded as 2 bits.
    /// </summary>
    /// <remarks>
    ///     Even if the instruction has 2 formal parameters, it still encodes 4 due to the fixed size of the field.
    ///     If the instruction has exactly 1 formal parameter, it is okay to just assign this field to the respective DataLocation of that argument.
    ///     If the instruction has no arguments, the value of this field does not matter.
    /// </remarks>
    /// <see cref="DataLocation"/>, <see cref="GetArgumentLocation"/>
    [FieldOffset(1)]
    public byte DataLocationSpecifiers;

    // Byte 2 is reserved for future use

    /// <summary>
    ///     A reserved field, used on a per-instruction basis.
    ///     The jump and binary instructions use it to encode their types.
    /// </summary>
    [FieldOffset(3)]
    public byte Reserved2;

    public InstructionSet OpCode => (InstructionSet) RawOpcode;
    public CPUMemoryCell Cell => CPUMemoryCell.FromUInt32(UInt32);

    /// <summary>
    ///     Given the argument number (between 0 and 3 - instructions don't support more than 4 formal arguments),
    ///     return where the data for it is stored.
    /// </summary>
    /// <param name="argc"></param>
    /// <returns></returns>
    public DataLocation GetArgumentLocation(int argc)
    {
        DebugTools.Assert(argc >= 0 && argc <= 3, "Argument count violated");
        return (DataLocation) ((DataLocationSpecifiers >> (argc << 1)) & 0b11);
    }

    /// <summary>
    ///     Given the data locations of up to 4 arguments, retur
    /// </summary>
    public static byte EncodeDataLocations(
        DataLocation arg1,
        DataLocation arg2,
        DataLocation arg3 = DataLocation.Immediate,
        DataLocation arg4 = DataLocation.Immediate)
    {
        // Note: C# converts those bytes to ints under the hood when doing bitwise arithmetics
        return (byte) ((int) arg1 | ((int) arg2 << 2) | ((int) arg3 << 4) | ((int) arg4 << 6));
    }

    public static CPUInstructionCell FromUInt32(uint value) => new() { UInt32 = value };

    public override string ToString() => $"[{OpCode} | {DataLocationSpecifiers} | {Reserved2}]";

    public enum DataLocation : byte
    {
        /// <summary>Data is stored in-line after this instruction.</summary>
        Immediate,
        /// <summary>Data is stored in a register specified after this instruction.</summary>
        Register,
        /// <summary>Data is stored in a static location in the memory.</summary>
        Static,
        /// <summary>
        ///     Data is stored in a location in the memory specified by the value of a register;
        ///     the name of the register is specified after this instruction.
        /// </summary>
        Dynamic,
    }
}
