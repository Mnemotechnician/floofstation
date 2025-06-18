namespace Content.Server.FloofStation.NebulaComputing.Components;


// ReSharper disable once InconsistentNaming
/// <summary>
///     The memory bank of a programmable computer.
/// </summary>
[RegisterComponent]
public sealed partial class CPUComponent : Component
{
    [DataField(required: true)]
    public int InstructionRate;

    [NonSerialized]
    public int EntryPoint;

    [NonSerialized]
    public VirtualCPU.VirtualCPU? Executor;
}
