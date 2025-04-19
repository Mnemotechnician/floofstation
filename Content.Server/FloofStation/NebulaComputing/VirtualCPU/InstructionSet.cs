namespace Content.Server.FloofStation.NebulaComputing.VirtualCPU;


// ReSharper disable InconsistentNaming
public enum InstructionSet : int
{
    // Flags
    // Indicates that the command should try to preserve the stack. Not all commands support this.
    PRESERVE_STACK_MASK = 0x100000,

    // Instructions
    NOP    = 0x00,
    LOAD   = 0x01,
    STORE  = 0x02,
    PUSH   = 0x03,
    DUP    = 0x04,
    DROP   = 0x05,
    // Note: operand type is explicitly specified in a parameter (see the OperandType enum)
    // Operation type is passed as the second parameter (see the OperationType enum)
    BINARY = 0x06,
    JMP    = 0x07,
    // Conditional jump, kind is explicitly specified in a parameter (see the JumpType enum)
    JMPC   = 0x08,
    OUT    = 0x09,
    IN     = 0x0A,
    CALL   = 0x0B,
    RET    = 0x0C,
    HALT   = 0x0D,

    MAX_OPCODE = HALT,
    // Always invalid, useful to separate sections
    INVALID = 0x1234ABCD,

    // Same but with flags
    LOAD_PSP   = LOAD   | PRESERVE_STACK_MASK,
    STORE_PSP  = STORE  | PRESERVE_STACK_MASK,
    BINARY_PSP = BINARY | PRESERVE_STACK_MASK,
    JMPC_PSP   = JMPC   | PRESERVE_STACK_MASK,
    OUT_PSP    = OUT    | PRESERVE_STACK_MASK,
    IN_PSP     = IN     | PRESERVE_STACK_MASK
}
// ReSharper restore InconsistentNaming

/// <summary>
///     Used to specify the type of operands for use with the BINARY instruction
/// </summary>
/// <remarks>Names are intentionally kept simple to allow reusing in the asm compiler.</remarks>
public enum OperandType : int
{
    Int   = 0,
    Float = 1,
    Max   = Float
}

/// <summary>
///     Used to specify the type of operations for use with the BINARY instruction
/// </summary>
/// <remarks>Names are intentionally kept simple to allow reusing in the asm compiler.</remarks>
public enum BinaryOperationType : int
{
    Add = 1,
    Sub = 2,
    Mul = 3,
    Div = 4,
    Mod = 5,
    MaxValue = Mod
}

/// <summary>
///     Used to specify the type of conditional jumps
/// </summary>
public enum JumpType : int
{
    Zero    = 0,
    NonZero = 1,
    Max = NonZero
}
