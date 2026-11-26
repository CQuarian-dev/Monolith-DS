using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._WG.GroundCover;

/// <summary>
/// Slows mobs in deep snow and gives their steps a crunch on snow cover. Both sides work it out from the snow depths
/// each has, so walking stays predicted: the server from what it last sent clients, the client from what it got.
/// </summary>
public abstract partial class SharedWGSnowSlowSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier SnowSteps = new SoundCollectionSpecifier("FootstepSnow");
    private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(30);

    /// <summary>Where each mob's slowdown was last worked out, and what it came to.</summary>
    private readonly Dictionary<EntityUid, (EntityUid Grid, Vector2i Tile, int Version, float Speed)> _tracked = new();

    /// <summary>Mobs on a snow grid at the last full scan; the ticks between scans only visit these.</summary>
    private readonly HashSet<EntityUid> _onSnowGrid = new();

    private readonly List<EntityUid> _stale = new();
    private readonly List<EntityUid> _urgent = new();
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<InputMoverComponent> _inputQuery;
    private EntityQuery<MovementSpeedModifierComponent> _moveQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private TimeSpan _nextPrune;
    private int _scanIn;
    private bool _forget;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _inputQuery = GetEntityQuery<InputMoverComponent>();
        _moveQuery = GetEntityQuery<MovementSpeedModifierComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<MovementSpeedModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<WGSnowFootstepEvent>(OnFootstep);
        UpdatesBefore.Add(typeof(SharedPhysicsSystem));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _tracked.Clear();
        _onSnowGrid.Clear();
    }

    /// <summary>
    /// Forgets what each mob's slowdown was, so the next update sets it again for all of them. The client calls this
    /// when a game state resets its mobs' speeds to the server's, which can lag their tile by a tick.
    /// </summary>
    protected void ForgetTracked()
    {
        _tracked.Clear();
        _forget = true;
    }

    /// <summary>Snow depth on a tile as this side knows it for a mob, 0 where it isn't known yet.</summary>
    protected abstract int TileDepth(EntityUid mob, EntityUid grid, Vector2i tile);

    /// <summary>Whether a grid has any snow cover this side knows of.</summary>
    protected abstract bool HasSnow(EntityUid grid);

    /// <summary>Goes up whenever the depths this side reads change, so standing mobs are looked at again.</summary>
    protected abstract int SnowVersion { get; }

    /// <summary>Goes up whenever the depths around a tile change; by default <see cref="SnowVersion"/>.</summary>
    protected virtual int AreaVersion(EntityUid grid, Vector2i tile) => SnowVersion;

    /// <summary>Ticks between full scans of every mover; the ticks between only visit mobs on snow grids.</summary>
    protected virtual int ScanTicks => 1;

    /// <summary>Mobs visited every tick even between full scans, such as player-controlled ones.</summary>
    protected virtual void GetUrgent(List<EntityUid> mobs)
    {
    }

    /// <summary>Whether this side works out a mob's slowdown: every mob on the server, predicted ones on the client.</summary>
    protected abstract bool Tracks(EntityUid mob);

    /// <summary>The share of its walking speed a mob has in the snow where it stands.</summary>
    public float GetSpeed(EntityUid mob, TransformComponent? xform = null)
    {
        if (!Resolve(mob, ref xform, false) || !TileUnder(xform, out var grid, out var tile))
            return 1f;

        return WGSnowCover.SpeedAt(WGSnowCover.MoveDepth(tile, t => TileDepth(mob, grid, t)));
    }

    /// <summary>Whether the tile a mob stands on is snow covered, by this side's depths.</summary>
    public bool OnCover(EntityUid mob, TransformComponent? xform = null)
    {
        return Resolve(mob, ref xform, false)
               && TileUnder(xform, out var grid, out var tile)
               && TileDepth(mob, grid, tile) >= WGSnowCover.CoverFrom;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_forget || --_scanIn <= 0)
        {
            _scanIn = ScanTicks;
            _onSnowGrid.Clear();
            var query = EntityQueryEnumerator<InputMoverComponent, MovementSpeedModifierComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out _, out var move, out var xform))
            {
                if (Visit(uid, move, xform))
                    _onSnowGrid.Add(uid);
            }
        }
        else
        {
            foreach (var uid in _onSnowGrid)
            {
                if (_moveQuery.TryComp(uid, out var move) && _xformQuery.TryComp(uid, out var xform))
                    Visit(uid, move, xform);
            }

            _urgent.Clear();
            GetUrgent(_urgent);
            foreach (var uid in _urgent)
            {
                if (!_onSnowGrid.Contains(uid)
                    && _inputQuery.HasComp(uid)
                    && _moveQuery.TryComp(uid, out var move)
                    && _xformQuery.TryComp(uid, out var xform)
                    && Visit(uid, move, xform))
                    _onSnowGrid.Add(uid);
            }
        }

        _forget = false;

        var now = _timing.CurTime;
        if (now < _nextPrune)
            return;

        _nextPrune = now + PruneInterval;
        _stale.Clear();
        foreach (var uid in _tracked.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _stale.Add(uid);
        }

        foreach (var uid in _stale)
        {
            _tracked.Remove(uid);
        }
    }

    /// <summary>Sets one mob's slowdown if its tile or the snow around it changed. True when it stands on a snow grid.</summary>
    private bool Visit(EntityUid uid, MovementSpeedModifierComponent move, TransformComponent xform)
    {
        if (!Tracks(uid))
            return false;

        var on = TileUnder(xform, out var grid, out var tile);
        if (!on)
        {
            if (_tracked.Remove(uid, out var gone) && gone.Speed < 1f || _forget)
                _speed.RefreshMovementSpeedModifiers(uid, move);

            return false;
        }

        var version = AreaVersion(grid, tile);
        var had = _tracked.TryGetValue(uid, out var last);
        if (had && last.Grid == grid && last.Tile == tile && last.Version == version)
            return true;

        var speed = WGSnowCover.SpeedAt(WGSnowCover.MoveDepth(tile, t => TileDepth(uid, grid, t)));
        _tracked[uid] = (grid, tile, version, speed);
        if (_forget || (had ? last.Speed != speed : speed < 1f))
            _speed.RefreshMovementSpeedModifiers(uid, move);

        return true;
    }

    /// <summary>The grid tile a mob stands on, if that grid has snow.</summary>
    private bool TileUnder(TransformComponent xform, out EntityUid grid, out Vector2i tile)
    {
        tile = default;
        grid = default;
        if (xform.GridUid is not { } gridUid
            || xform.ParentUid != gridUid
            || !HasSnow(gridUid)
            || !_gridQuery.TryComp(gridUid, out var gridComp))
            return false;

        grid = gridUid;
        tile = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        return true;
    }

    private void OnRefresh(Entity<MovementSpeedModifierComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var speed = GetSpeed(ent);
        if (speed >= 1f)
            return;

        // Gear that caps contact slowdowns, such as a vest's terrain runner, caps snow the same way.
        var cap = new GetSpeedModifierContactCapEvent();
        RaiseLocalEvent(ent, ref cap);
        var walk = MathF.Min(1f, MathF.Max(speed, cap.MaxWalkSlowdown));
        var sprint = MathF.Min(1f, MathF.Max(speed, cap.MaxSprintSlowdown));
        if (walk < 1f || sprint < 1f)
            args.ModifySpeed(walk, sprint);
    }

    private void OnFootstep(ref WGSnowFootstepEvent args)
    {
        if (args.Sound == null && HasSnow(args.Grid) && TileDepth(args.User, args.Grid, args.Tile) >= WGSnowCover.CoverFrom)
            args.Sound = SnowSteps;
    }
}
