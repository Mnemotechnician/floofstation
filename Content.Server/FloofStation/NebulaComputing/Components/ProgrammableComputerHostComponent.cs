using Robust.Shared.GameStates;


namespace Content.Server.FloofStation.NebulaComputing.Components;


[RegisterComponent]
public sealed partial class ProgrammableComputerHostComponent : Component
{
    // References for easier future usage
    [NonSerialized]
    public int[]? CPUOperationStack, RandomAccessStorage, PersistentStorage;
}
