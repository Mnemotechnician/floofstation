using Content.Server.FloofStation.NebulaComputing.VirtualCPU;
using Robust.Shared.GameStates;


namespace Content.Server.FloofStation.NebulaComputing.Components;


[RegisterComponent]
public sealed partial class ProgrammableComputerHostComponent : Component
{
    public const string CPUContainerName = "cpu", MemoryContainerName = "memory", StorageContainerName = "storage";

    // References for easier future usage
    [NonSerialized]
    public bool SetupDone;

    [NonSerialized]
    public Entity<CPUComponent, MemoryComponent>? CPU;

    [NonSerialized]
    public Entity<MemoryComponent>? Memory, Storage;


    public CPUMemoryCell[]? CPUStackData => CPU?.Comp2.StackData;

    public CPUMemoryCell[]? MemoryData => Memory?.Comp.RandomAccessData;

    public CPUMemoryCell[]? StorageData => Storage?.Comp.RandomAccessData;
}
