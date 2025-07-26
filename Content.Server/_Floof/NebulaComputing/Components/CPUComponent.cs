namespace Content.Server._Floof.NebulaComputing.Components;


// ReSharper disable once InconsistentNaming
/// <summary>
///     The CPU of a programmable computer.
/// </summary>
[RegisterComponent]
public sealed partial class CPUComponent : Component
{
    [DataField(required: true)]
    public int InstructionRate;

    [NonSerialized]
    public uint EntryPoint;

    [NonSerialized]
    public VirtualCPU.Cpu.VirtualCPU? Executor;
}
