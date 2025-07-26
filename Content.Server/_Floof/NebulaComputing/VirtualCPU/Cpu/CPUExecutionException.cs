namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;


// ReSharper disable once InconsistentNaming   shut up!!!!
public sealed class CPUExecutionException(CPUErrorCode code) : Exception
{
    public CPUErrorCode ErrorCode = code;
}


// ReSharper disable once InconsistentNaming   shut up!!!!
public enum CPUErrorCode : byte
{
    IllegalInstruction = 0x01,
    SegmentationFault  = 0x02,
    StackOverflow      = 0x03,
    StackUnderflow     = 0x04,
    DivisionByZero     = 0x05,
    InvalidType        = 0x06,
    InvalidPort        = 0x07,
    // Hit a section boundary, a subtype of IllegalInstruction
    SectionBoundary    = 0x08,
    IllegalRegister    = 0x09
}
