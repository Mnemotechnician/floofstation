using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;


namespace Content.Shared.FloofStation.XenoArch.DelayedStationDestruction;


[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CollapsingArtifactComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public TimeSpan CollapseTime;

    [DataField(required: true), AutoNetworkedField]
    public EntProtoId SpawnOnCollapse;

    [DataField, AutoNetworkedField]
    public float VisualParticlesRange = 15;

    /// <summary>
    ///     Initialized on mapinit
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan CollapseStart;
}
