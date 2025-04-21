using Content.Server.FloofStation.NebulaComputing.VirtualCPU;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;


namespace Content.Server.FloofStation.NebulaComputing.Components;


/// <summary>
///     The memory bank of a programmable computer.
/// </summary>
[RegisterComponent]
public sealed partial class MemoryComponent : Component
{
    /// <summary>
    ///     Storage capacity, in bytes.
    /// </summary>
    [DataField(required: true)]
    public int Capacity;

    [DataField(required: true)]
    public MemoryType Kind;

    /// <summary>
    ///     The actual memory of the computer. Created at runtime. Which fields are initialized depends on the <see cref="Kind"/>.
    /// </summary>
    [NonSerialized]
    public CPUMemoryCell[]? StackData, RandomAccessData, PersistentData;
}

[Serializable, Flags]
public enum MemoryType : int
{
    /// <summary>
    ///     The random access storage, aka storage that only persists while the computer is powered on.
    /// </summary>
    RandomAccess = 1,

    /// <summary>
    ///     The persistent storage, aka storage that is not reset when the computer is powered off.
    /// </summary>
    Persistent = 2,

    // ReSharper disable once InconsistentNaming
    /// <summary>
    ///     The operation stack, where numbers and other values are pushed and popped.
    ///     This is a cheap replacement for actual registers and shit.
    ///     Because I can't be bothered to write a compiler that respects registers and all that.
    /// </summary>
    CPUOperationStack = 4,

    /// <summary>
    ///     Only use for things like microcontrollers, where RAM and storage are built-in.
    /// </summary>
    Combined = RandomAccess | Persistent | CPUOperationStack
}
