using Content.Server._Floof.NebulaComputing.Systems;
using Content.Server._Floof.NebulaComputing.VirtualCPU;
using Content.Server._Floof.NebulaComputing.VirtualCPU.Cpu;
using Content.Shared._Floof.NebulaComputing.UI;
using Content.Shared._Floof.NebulaComputing.Util;
using Robust.Shared.GameStates;


namespace Content.Server._Floof.NebulaComputing.Components;


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
    public VirtualCPUECSIOProvider? IOProvider;

    // Shorthands
    [ViewVariables]
    public CPUMemoryCell[]? MemoryData => Memory?.Comp.RandomAccessData;

    [ViewVariables]
    public CPUMemoryCell[]? StorageData => Storage?.Comp.RandomAccessData;

    #region UI

    [DataField]
    public TimeSpan NextBUIUpdate = TimeSpan.Zero;

    #endregion

    #region Assembler

    [DataField]
    public string? AssemblerCode;

    [NonSerialized]
    public bool IsActivelyAssembling = false;

    #endregion
}
