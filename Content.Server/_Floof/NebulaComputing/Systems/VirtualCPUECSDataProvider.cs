using Content.Server._Floof.NebulaComputing.Components;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;


namespace Content.Server._Floof.NebulaComputing.Systems;


public sealed class VirtualCPUECSDataProvider(ProgrammableComputerHostComponent comp) : VirtualCPUDataProvider
{
    public ProgrammableComputerHostComponent Component => comp;
    public CPUMemoryCell[]? Memory => Component.MemoryData;

    public override CPUMemoryCell GetValue(uint address)
    {
        if (Memory is not {} mem || address >= mem.Length)
            throw new CPUExecutionException(CPUErrorCode.SegmentationFault);

        return mem[address];
    }

    public override void SetValue(uint address, CPUMemoryCell value)
    {
        if (Memory is not {} mem || address >= mem.Length)
            throw new CPUExecutionException(CPUErrorCode.SegmentationFault);

        mem[address] = value;
    }
}
