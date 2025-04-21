using Content.Server.DeviceNetwork.Systems;
using Content.Server.FloofStation.NebulaComputing.Components;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU;
using Content.Shared.DeviceLinking;


namespace Content.Server.FloofStation.NebulaComputing.Systems;


public sealed partial class ProgrammableComputerHostSystem
{
    const string PortNamePrefix = "CustomPort";

    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;



    private void SetupPorts(Entity<ProgrammableComputerHostComponent> ent)
    {
        // Count inputs and outputs
        // We don't ensure that port indices are correct and ordinal, that is up to the programmer.
        var sinkComp = EnsureComp<DeviceLinkSinkComponent>(ent);
        var sourceComp = EnsureComp<DeviceLinkSourceComponent>(ent);

        int outputs = 0, inputs = 0;
        foreach (var port in sourceComp.Ports)
            if (port.Id.StartsWith(PortNamePrefix))
                outputs++;

        foreach (var port in sinkComp.Ports)
            if (port.Id.StartsWith(PortNamePrefix))
                inputs++;

        ent.Comp.OutputPorts = outputs;
        ent.Comp.InputPorts = inputs;
    }

    public void TryWritePort(int port, CPUMemoryCell message)
    {
        _deviceNetwork.
    }
}
