using Content.Shared._WG.BitmaskWalls;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Utility;

namespace Content.Client._WG.BitmaskWalls;

/// <summary>Tracks each <see cref="WallFrillComponent"/> wall's top for <see cref="WallFrillOverlay"/>.</summary>
public sealed partial class WallFrillSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private const string FrillStatePrefix = "frill-";

    /// <summary>A wall with all of these neighbours has another wall's top above it, so it draws no frill.</summary>
    private const int FrillCull = 1 | 4 | 8 | 16 | 128;

    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile, int Junction, ResPath Rsi)> _frillOf = new();

    /// <summary>Frills to draw, per grid and tile, with the wall below each one.</summary>
    public readonly Dictionary<EntityUid, Dictionary<Vector2i, (Texture Texture, EntityUid Wall)>> Frills = new();

    private EntityQuery<WallFrillComponent> _frillQuery;

    public override void Initialize()
    {
        base.Initialize();

        _frillQuery = GetEntityQuery<WallFrillComponent>();
        _overlays.AddOverlay(new WallFrillOverlay(this));

        SubscribeLocalEvent<WallFrillComponent, ComponentShutdown>(OnShutdown);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlays.RemoveOverlay<WallFrillOverlay>();
        Frills.Clear();
        _frillOf.Clear();
    }

    private void OnShutdown(Entity<WallFrillComponent> ent, ref ComponentShutdown args)
    {
        RemoveFrill(ent);
    }

    /// <summary>Puts the wall's top on the tile above it, or removes it when unanchored or covered.</summary>
    public void UpdateFrill(Entity<SpriteComponent> wall, int junction, EntityUid? grid, Vector2i tile)
    {
        if (!_frillQuery.HasComp(wall))
            return;

        if (grid is not { } gridUid || (junction & FrillCull) == FrillCull || wall.Comp.BaseRSI is not { } rsi)
        {
            RemoveFrill(wall);
            return;
        }

        var above = tile + new Vector2i(0, 1);
        if (_frillOf.TryGetValue(wall, out var old) && old == (gridUid, above, junction, rsi.Path))
            return;

        RemoveFrill(wall);
        var state = FrillStatePrefix + junction;
        if (!rsi.TryGetState(state, out _))
            return;

        var texture = _sprite.Frame0(new SpriteSpecifier.Rsi(rsi.Path, state));
        if (!Frills.TryGetValue(gridUid, out var frills))
        {
            frills = new Dictionary<Vector2i, (Texture, EntityUid)>();
            Frills[gridUid] = frills;
        }

        frills[above] = (texture, wall);
        _frillOf[wall] = (gridUid, above, junction, rsi.Path);
    }

    private void RemoveFrill(EntityUid wall)
    {
        if (!_frillOf.Remove(wall, out var old) || !Frills.TryGetValue(old.Grid, out var frills))
            return;

        if (frills.TryGetValue(old.Tile, out var frill) && frill.Wall == wall)
            frills.Remove(old.Tile);

        if (frills.Count == 0)
            Frills.Remove(old.Grid);
    }
}
