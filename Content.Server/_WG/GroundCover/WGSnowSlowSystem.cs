using Content.Shared._WG.GroundCover;
using Robust.Shared.Player;

namespace Content.Server._WG.GroundCover;

/// <summary>Snow slowdown and footsteps from the depths clients were sent, as each player's client has them.</summary>
public sealed class WGSnowSlowSystem : SharedWGSnowSlowSystem
{
    [Dependency] private WGGroundCoverSystem _cover = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    private EntityQuery<ActorComponent> _actorQuery;

    public override void Initialize()
    {
        base.Initialize();
        _actorQuery = GetEntityQuery<ActorComponent>();
    }

    protected override int SnowVersion => _cover.SentStamp;

    // Half a second between full scans; player mobs are visited every tick, so their walking stays predicted.
    protected override int ScanTicks => 15;

    protected override int AreaVersion(EntityUid grid, Vector2i tile) => _cover.SentVersionAround(grid, tile);

    protected override void GetUrgent(List<EntityUid> mobs)
    {
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob)
                mobs.Add(mob);
        }
    }

    protected override bool HasSnow(EntityUid grid)
    {
        return HasComp<WGSnowCoverComponent>(grid);
    }

    protected override bool Tracks(EntityUid mob)
    {
        return true;
    }

    protected override int TileDepth(EntityUid mob, EntityUid grid, Vector2i tile)
    {
        return _cover.GetSentDepth(grid, tile, _actorQuery.TryComp(mob, out var actor) ? actor.PlayerSession : null);
    }
}
