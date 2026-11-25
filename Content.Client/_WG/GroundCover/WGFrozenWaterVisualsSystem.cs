using Content.Shared._WG.GroundCover;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.Client._WG.GroundCover;

/// <summary>
/// Draws frozen water with ice art in the water's own shore shapes, and hides it under snow cover so the snow drawn on
/// the ground shows.
/// </summary>
public sealed class WGFrozenWaterVisualsSystem : EntitySystem
{
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private WGGroundCoverSystem _cover = default!;

    private static readonly ResPath IcePath = new("/Textures/_WG/GroundCover/lake_ice.rsi");

    private readonly HashSet<EntityUid> _queued = new();
    private int _snowVersion = -1;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WGFreezableWaterComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<WGFreezableWaterComponent, ComponentStartup>(OnStartup);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _queued.Clear();
    }

    private void OnState(Entity<WGFreezableWaterComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _queued.Add(ent);
    }

    private void OnStartup(Entity<WGFreezableWaterComponent> ent, ref ComponentStartup args)
    {
        // Smoothing lays the sprite's corner layers at startup too, so the art is set once a frame has passed.
        _queued.Add(ent);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_cover.Version != _snowVersion)
        {
            _snowVersion = _cover.Version;
            var query = EntityQueryEnumerator<WGFreezableWaterComponent>();
            while (query.MoveNext(out var uid, out var water))
            {
                if (water.Frozen)
                    _queued.Add(uid);
            }
        }

        if (_queued.Count == 0)
            return;

        var ice = _resources.GetResource<RSIResource>(IcePath).RSI;
        foreach (var uid in _queued)
        {
            if (TryComp<WGFreezableWaterComponent>(uid, out var water) && TryComp<SpriteComponent>(uid, out var sprite))
                Apply((uid, water, sprite), ice);
        }

        _queued.Clear();
    }

    private void Apply(Entity<WGFreezableWaterComponent, SpriteComponent> ent, RSI ice)
    {
        var sprite = ent.Comp2;
        var rsi = ent.Comp1.Frozen ? ice : sprite.BaseRSI;
        for (var i = 0; _sprite.TryGetLayer((ent, sprite), i, out var layer, false); i++)
        {
            if (layer.ActualRsi != rsi)
                _sprite.LayerSetRsi((ent, sprite), i, rsi, layer.State);
        }

        _sprite.SetVisible((ent, sprite), !ent.Comp1.Frozen || !SnowCovered(ent));
    }

    /// <summary>Whether snow lies thick enough on the tile to hide the ice.</summary>
    private bool SnowCovered(EntityUid uid)
    {
        var xform = Transform(uid);
        return xform.GridUid is { } grid
               && TryComp<MapGridComponent>(grid, out var gridComp)
               && _cover.GetDepth(grid, _map.TileIndicesFor(grid, gridComp, xform.Coordinates)) >= WGSnowCover.CoverFrom;
    }
}
