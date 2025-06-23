using Robust.Shared.Serialization;


namespace Content.Shared._Floof.NebulaComputing.UI.Events;


/// <summary>
///     Raised client->server to request compiling an assembly file and assigning its output to the computer's memory.
/// </summary>
[Serializable, NetSerializable]
public sealed class AssemblerRunRequest : BoundUserInterfaceMessage
{
    public string Code = string.Empty;

    /// <summary>
    ///     Whether to immediately load the code into the computer RAM and run it.
    /// </summary>
    public bool Run = true;
}
