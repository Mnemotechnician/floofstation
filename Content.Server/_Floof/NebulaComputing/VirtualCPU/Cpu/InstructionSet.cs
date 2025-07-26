namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;


public enum InstructionSet : byte
{
    // Instructions
    Nop    = 0x00,
    Mov    = 0x01,
    Push   = 0x02,
    Pop    = 0x03,
    /// Note: operation type is specified in the Reserved2 byte (see the OperandType enum)
    /// Operand type is specified as the 8th byte (1 = floating point mode, 0 = int mode)
    Binary = 0x04,
    /// Note: operation type is specified in the Reserved2 byte (see the OperandType enum)
    /// Operand type is specified as the 8th byte (1 = floating point mode, 0 = int mode)
    Unary  = 0x05,

    Jmp    = 0x06,
    /// Conditional jump, kind is explicitly specified in the Reserved2 byte (see the JumpType enum)
    JmpC   = 0x07,
    /// Arithmetic comparison. The reserved byte is an <see cref="OperandType"/>
    Cmp    = 0x08,
    Out    = 0x09,
    In     = 0x0A,

    Call   = 0x0B,
    Ret    = 0x0C,
    Enter  = 0x0D,
    Leave  = 0x0E,

    Halt   = 0xF,

    MaxOpcode = Halt,
    // Always invalid, useful to separate sections
    Invalid = 0xff,
    SectionBoundary = 0xfe
}

/// <summary>
///     Used to specify the type of operands for use with certain instructions.
/// </summary>
/// <remarks>Names are intentionally kept simple to allow reusing in the asm compiler.</remarks>
public enum OperandType : int
{
    Int   = 0,
    Float = 1,
    Uint  = 2,
    MaxValue   = Uint
}

/// <summary>
///     Used to specify the type of operations for use with the BINARY instruction
/// </summary>
/// <remarks>Names are intentionally kept simple to allow reusing in the asm compiler.</remarks>
public enum BinaryOperationType : byte
{
    Add = 0,
    Sub = 1,
    Mul = 2,
    Div = 3,
    Mod = 4,
    And = 5,
    Or  = 6,
    Xor = 7,
    Pow = 8
}

/// <summary>
///     Used to specify the type of operations for use with the UNARY instruction
/// </summary>
/// <remarks>Names are intentionally kept simple to allow reusing in the asm compiler.</remarks>
public enum UnaryOperationType : byte
{
    Neg = 0,
    Abs = 1,
    Not = 2,
    Sqrt = 3,
    Sin = 4,
    Cos = 5,
    Tan = 6,
    Asin = 7,
    Acos = 8,
    Atan = 9,
    Inc = 10,
    Dec = 11,
    Floor = 12,
    Ceil = 13,
    Round = 14,
    Trunc = 15,
    Log = 16
}

/// <summary>
///     Used to specify the type of conditional jumps
/// </summary>
public enum JumpType : byte
{
    Equal     = 0,
    NotEqual  = 1,
    Greater   = 2,
    Lower     = 3,
    GreaterEq = 4,
    LowerEq   = 5,

    Max = LowerEq
}
