namespace Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;


public enum CPURegister : byte
{
    NULL = 0,

    // General-purpose registers
    RAX = 1,
    RBX = 2,
    RCX = 3,
    RDX = 4,

    // Instruction pointer
    RIP = 5,

    // Stack pointer
    RSP = 6,
    // Stack frame
    RBP = 7,
    // Stack start and size
    RSS = 8,
    RSZ = 9,

    // Source and destination registers
    RDST = 10,
    RSRC = 11,

    // Comparison flags
    RFLAGS = 12,

    Count = RFLAGS + 1
}

public static class RegisterHelpers
{
    public static bool TryParse(string name, out CPURegister ret)
    {
        if (name.Length == 0 || char.ToLower(name[0]) != 'R' || !Enum.TryParse(name, true, out ret))
        {
            ret = CPURegister.NULL;
            return false;
        }

        return true;
    }
}
