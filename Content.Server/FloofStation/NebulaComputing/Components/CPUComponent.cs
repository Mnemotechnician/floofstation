using Robust.Shared.GameStates;
using Robust.Shared.Serialization;


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
}
