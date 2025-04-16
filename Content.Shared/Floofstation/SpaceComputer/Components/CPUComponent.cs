using Robust.Shared.GameStates;
using Robust.Shared.Serialization;


namespace Content.Shared.FloofStation.SpaceComputer.Components;


// ReSharper disable once InconsistentNaming
/// <summary>
///     The memory bank of a programmable computer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CPUComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public int InstructionRate;
}
