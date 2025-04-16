namespace Content.Server.FloofStation.SpaceComputer.VirtualCPU;


// ReSharper disable InconsistentNaming
public enum InstructionSet : byte
{
    NOP    = 0x00,
    LOAD   = 0x01,
    STORE  = 0x02,
    DUP    = 0x03,
    DROP   = 0x04,
    // Note: operand type is explicitly specified in a parameter (see the OperandType enum)
    // Operation type is passed as the second parameter (see the OperationType enum)
    BINARY = 0x05,
    JMP    = 0x06,
    // Conditional jump, kind is explicitly specified in a parameter (see the JumpType enum)
    JMPC   = 0x07,
    OUT    = 0x08,
    IN     = 0x09,
    MAX_EXCEPT_HALT = IN,

    HALT   = 0xff,
}
// ReSharper restore InconsistentNaming

/// <summary>
///     Used to specify the type of operands for use with the BINARY instruction
/// </summary>
public enum OperandType : int
{
    Integer = 0,
    Float   = 1,
    Max = Float
}

/// <summary>
///     Used to specify the type of operations for use with the BINARY instruction
/// </summary>
public enum OperationType : int
{
    Add      = 0,
    Subtract = 1,
    Multiply = 2,
    Divide   = 3,
    Modulus  = 4,
    Max = Modulus
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
