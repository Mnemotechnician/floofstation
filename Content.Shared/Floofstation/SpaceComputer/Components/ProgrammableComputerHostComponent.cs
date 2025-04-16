using Robust.Shared.GameStates;


namespace Content.Shared.FloofStation.SpaceComputer.Components;


[RegisterComponent, NetworkedComponent]
public sealed partial class ProgrammableComputerHostComponent : Component
{
    // References for easier future usage
    [NonSerialized]
    public int[] CPUOperationStack, RandomAccessStorage, PersistentStorage;
}
