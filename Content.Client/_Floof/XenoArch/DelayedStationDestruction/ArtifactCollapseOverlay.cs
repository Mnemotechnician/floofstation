using Content.Shared.FloofStation.XenoArch.DelayedStationDestruction;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;


namespace Content.Client._Floof.XenoArch.DelayedStationDestruction;


public sealed class ArtifactCollapseOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private TransformSystem _transform = default!;

    private const float PvsDist = 25.0f;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    [ValidatePrototypeId<ShaderPrototype>] public const string Shader = "ArtifactCollapse";
    private ShaderInstance _shader;
    private List<Entity<CollapsingArtifactComponent, TransformComponent>> _artifacts = new();

    public ArtifactCollapseOverlay()
    {
        IoCManager.InjectDependencies(this);

        _shader = _prototypeManager.Index<ShaderPrototype>(Shader).Instance().Duplicate();
        RequestScreenTexture = true;
        ZIndex = 101; // Same as singularity
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        _transform ??= _entityManager.System<TransformSystem>();

        var eye = args.Viewport.Eye;
        if (eye == null)
            return false;

        var query = _entityManager.EntityQueryEnumerator<CollapsingArtifactComponent, TransformComponent>();
        _artifacts.Clear();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            // Don't render artifacts that are too far away or on different maps
            if (args.MapId != xform.MapID
                || (eye.Position.Position - _transform.GetWorldPosition(xform)).LengthSquared() > PvsDist * PvsDist)
                continue;

            _artifacts.Add((uid, comp, xform));
        }

        return _artifacts.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null || _artifacts.Count == 0)
            return;

        var worldHandle = args.WorldHandle;
        var viewport = args.Viewport;
        // This is not ideal, but having 2 or more artifacts collapsing at the same time within your PVS is practically impossible.
        // Even if it happens (e.g. due to multiple artifexium spraying), chances are singularities/teslas will be stacked on top of each other.
        var artifact = _artifacts[0];
        var life = (_timing.CurTime - artifact.Comp1.CollapseStart) / artifact.Comp1.CollapseTime;
        var range = artifact.Comp1.VisualParticlesRange;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("positionInput", viewport.WorldToLocal(_transform.GetWorldPosition(artifact.Comp2)));
        _shader.SetParameter("range", range);
        _shader.SetParameter("life", (float) life);
        _shader.SetParameter("renderScale", viewport.RenderScale);

        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldAABB, Color.White);
        worldHandle.UseShader(null);
    }
}
