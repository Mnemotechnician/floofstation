using Content.Server.FloofStation.NebulaComputing.Systems;
using Content.Server.FloofStation.NebulaComputing.VirtualCPU;
using Content.Shared.FloofStation.NebulaComputing.UI;
using Content.Shared.FloofStation.NebulaComputing.Util;
using Robust.Shared.GameStates;


namespace Content.Server.FloofStation.NebulaComputing.Components;


[RegisterComponent, Access(typeof(ProgrammableComputerHostSystem), typeof(VirtualCPUIOProvider), Other = AccessPermissions.Read)]
public sealed partial class ProgrammableComputerHostComponent : Component
{
    public const string CPUContainerName = "cpu", MemoryContainerName = "memory", StorageContainerName = "storage";

    // References for easier future usage
    [NonSerialized, ViewVariables]
    public bool SetupDone;

    [NonSerialized, ViewVariables]
    public int OutputPorts, InputPorts;

    [NonSerialized, ViewVariables]
    public Entity<CPUComponent, MemoryComponent>? CPU;

    [NonSerialized, ViewVariables]
    public Entity<MemoryComponent>? Memory, Storage;

    [NonSerialized, ViewVariables]
    public VirtualCPUECSDataProvider DataProvider;

    // Shorthands
    [ViewVariables]
    public CPUMemoryCell[]? CPUStackData => CPU?.Comp2.StackData;

    [ViewVariables]
    public CPUMemoryCell[]? MemoryData => Memory?.Comp.RandomAccessData;

    [ViewVariables]
    public CPUMemoryCell[]? StorageData => Storage?.Comp.RandomAccessData;
}
