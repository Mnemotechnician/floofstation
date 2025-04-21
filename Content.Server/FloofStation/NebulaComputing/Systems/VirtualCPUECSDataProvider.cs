using Content.Server.FloofStation.NebulaComputing.Components;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU;


namespace Content.Server.FloofStation.NebulaComputing.Systems;


public sealed class VirtualCPUECSDataProvider(ProgrammableComputerHostComponent comp) : VirtualCPUDataProvider
{
    public ProgrammableComputerHostComponent Component => comp;
    public CPUMemoryCell[]? Memory => Component.MemoryData;

    public override CPUMemoryCell GetValue(int address)
    {
        if (address < 0 || Memory is not {} mem || address >= mem.Length)
            throw new CPUExecutionException(CPUErrorCode.SegmentationFault);

        return mem[address];
    }

    public override void SetValue(int address, CPUMemoryCell value)
    {
        if (address < 0 || Memory is not {} mem || address >= mem.Length)
            throw new CPUExecutionException(CPUErrorCode.SegmentationFault);

        mem[address] = value;
    }
}
