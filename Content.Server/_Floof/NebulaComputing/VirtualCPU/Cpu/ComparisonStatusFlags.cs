namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;


[Flags]
public enum ComparisonStatusFlags : uint
{
    None = 0,
    // Arguments are equal
    Equal = 1,
    // Arg1 is greater than arg2
    Greater = 2,
}
