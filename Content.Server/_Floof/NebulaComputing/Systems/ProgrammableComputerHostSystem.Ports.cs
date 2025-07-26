using System.Linq;
using Content.Server.DeviceLinking.Components;
using Content.Server.DeviceLinking.Events;
using Content.Server.DeviceLinking.Systems;
using Content.Server.DeviceNetwork;
using Content.Server.DeviceNetwork.Systems;
using Content.Server._Floof.NebulaComputing.Components;
using Content.Server._Floof.NebulaComputing.VirtualCPU;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceNetwork;
using Robust.Shared.Timing;


namespace Content.Server._Floof.NebulaComputing.Systems;


public sealed partial class ProgrammableComputerHostSystem
{
    const string PortNamePrefix = "CustomPort";
    /// <summary>The name of the device network message containing the actual integer value that the sender has sent.</summary>
    const string DeviceNetworkPortValueMessage = "DeviceNetworkPortValueMessage";

    [Dependency] private readonly DeviceLinkSystem _links = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly HashSet<Entity<ProgrammableComputerHostComponent>> _pendingUpdates = new();
    private TimeSpan _lastPortsUpdate = TimeSpan.Zero;
    private TimeSpan _portsUpdateInterval = TimeSpan.FromSeconds(0.5);

    private void InitializePortsHandling()
    {
        SubscribeLocalEvent<ProgrammableComputerHostComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<ProgrammableComputerHostComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
    }

    public void UpdatePorts(float frameTime)
    {
        // We limit the refresh rate because device networking is insanely expensive.
        if (_timing.CurTime - _lastPortsUpdate < _portsUpdateInterval)
            return;

        _lastPortsUpdate = _timing.CurTime;

        Entity<ProgrammableComputerHostComponent>[] updates;
        lock (_pendingUpdates)
        {
            if (_pendingUpdates.Count == 0)
                return;

            updates = _pendingUpdates.ToArray();
            _pendingUpdates.Clear();
        }

        // Payload is reused because the link system is going to copy it anyway
        var payload = new NetworkPayload();
        foreach (var ent in updates)
        {
            // Read everything from their ports
            var messages = ent.Comp.IOProvider?.GetAndClearPortOutputs();
            if (messages == null)
                continue;

            foreach (var (port, queue) in messages)
            {
                var portName = PortNamePrefix + port;
                foreach (var message in queue)
                {
                    // I hate ss14's device network system.
                    payload.Clear();
                    payload[DeviceNetworkConstants.LogicState] = message > 0 ? SignalState.High : SignalState.Low;
                    payload[DeviceNetworkPortValueMessage] = message;
                    _links.InvokePort(ent, portName, payload);
                }
            }
        }
    }

    private void OnSignalReceived(Entity<ProgrammableComputerHostComponent> ent, ref SignalReceivedEvent ev)
    {
        if (ev.Data is null)
            return;

        DispatchPacket(ent, ev.Data, ev.Port);
    }

    private void OnPacketReceived(Entity<ProgrammableComputerHostComponent> ent, ref DeviceNetworkPacketEvent ev)
    {
        if (!ev.Data.TryGetValue<string>(SharedDeviceLinkSystem.InvokedPort, out var port))
            return;

        DispatchPacket(ent, ev.Data, port);
    }

    /// <summary>
    ///     Dispatches a received packet on a programmable computer.
    /// </summary>
    private void DispatchPacket(Entity<ProgrammableComputerHostComponent> ent, NetworkPayload data, string port)
    {
        if (!port.StartsWith(PortNamePrefix) || !int.TryParse(port[PortNamePrefix.Length..], out var portNumber))
            return;

        int msg;
        if (data.TryGetValue<int>(DeviceNetworkPortValueMessage, out var literalValue))
            msg = literalValue; // Another computer sent this <3
        else if (data.TryGetValue<SignalState>(DeviceNetworkConstants.LogicState, out var state))
            msg = state == SignalState.High ? 1 : -1; // Logic gate or similar
        else
            msg = 1; // Default is 1 for things like remote signallers and similar.

        ent.Comp.IOProvider?.TryWritePortInput(portNumber - 1, CPUMemoryCell.FromInt32(msg));
    }

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

    public void MarkForPortsUpdate(Entity<ProgrammableComputerHostComponent> ent)
    {
        lock (_pendingUpdates)
            _pendingUpdates.Add(ent);
    }
}
