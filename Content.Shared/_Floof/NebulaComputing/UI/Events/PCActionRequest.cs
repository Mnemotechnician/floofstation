using Robust.Shared.Serialization;


namespace Content.Shared._Floof.NebulaComputing.UI.Events;


/// <summary>
///     Raised client->server to request an action.
/// </summary>
[Serializable, NetSerializable]
public sealed class PCActionRequest : BoundUserInterfaceMessage
{
    public Action Act;

    public enum Action : int
    {
        Start,
        Restart,
        Reset
    }
}
