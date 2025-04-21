using Content.Server.FloofStation.NebulaComputing.Components;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU;


namespace Content.Server.FloofStation.NebulaComputing.Systems;


public sealed class VirtualCPUECSIOProvider(ProgrammableComputerHostComponent comp) : VirtualCPUIOProvider
{
    public ProgrammableComputerHostComponent Component => comp;

    public override bool TryWrite(int port, CPUMemoryCell message)
    {
        // TODO: write to pins or virtual console
        throw new CPUExecutionException(CPUErrorCode.IllegalInstruction);
    }

    public override (bool success, CPUMemoryCell data) TryRead(int port)
    {
        // TODO: read from pins or virtual console
        throw new CPUExecutionException(CPUErrorCode.IllegalInstruction);
    }
}
