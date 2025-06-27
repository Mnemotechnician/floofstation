using Content.Shared.Construction.Components;
using Robust.Shared.Network;
using Robust.Shared.Timing;


namespace Content.Shared.FloofStation.XenoArch.DelayedStationDestruction;


public sealed class CollapsingArtifactSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CollapsingArtifactComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CollapsingArtifactComponent, AnchorAttemptEvent>(OnAnchor);
        SubscribeLocalEvent<CollapsingArtifactComponent, UnanchorAttemptEvent>(OnAnchor);
    }

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CollapsingArtifactComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (_gameTiming.CurTime <= comp.CollapseStart + comp.CollapseTime)
                continue;

            QueueDel(uid);
            Spawn(comp.SpawnOnCollapse, xform.Coordinates);
        }
    }

    private void OnMapInit(Entity<CollapsingArtifactComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.CollapseStart = _gameTiming.CurTime;
        _xforms.AnchorEntity(ent);
    }

    private void OnAnchor<T>(Entity<CollapsingArtifactComponent> ent, ref T args) where T : BaseAnchoredAttemptEvent
    {
        args.Delay *= 40f; // Are you brave enough?
    }
}
