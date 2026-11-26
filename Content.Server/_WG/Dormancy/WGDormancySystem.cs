using System.Numerics;
using Content.Server._WG.Planets;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Shuttles.Components;
using Content.Shared._WG.Planets;
using Content.Shared._LuaM.CCVar; // LuaM
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._WG.Dormancy;

/// <summary>
/// Says whether a spot on a planet is near a player, so systems can leave planet ground nobody is on alone.
/// Off-planet, and on a planet's orbit layer, everything is always active.
/// </summary>
public sealed partial class WGDormancySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    /// <summary>Side of an activity chunk, in metres.</summary>
    public const float ChunkSize = 16f;

    /// <summary>How far round a player, in metres, a planet stays awake; past what a player sees, so things wake before they come into view.</summary>
    public const float Range = 48f;

    private static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(0.5);

    private readonly HashSet<(EntityUid Network, Vector2i Chunk)> _active = new();
    private readonly HashSet<EntityUid> _activeNetworks = new();
    private readonly Dictionary<EntityUid, List<Vector2>> _viewers = new();
    private readonly Dictionary<EntityUid, EntityUid?> _networkOf = new();
    private TimeSpan _nextRefresh;

    private static readonly TimeSpan OffPlanetLogEvery = TimeSpan.FromSeconds(10);
    private readonly HashSet<(EntityUid Map, Vector2i Chunk)> _offPlanetActive = new();
    private readonly HashSet<EntityUid> _pilotedGrids = new();
    private readonly Dictionary<EntityUid, bool> _offPlanetSleeps = new();
    private bool _offPlanetDormancy;
    private int _offPlanetSlept;
    private TimeSpan _nextOffPlanetLog;
    private EntityQuery<FTLComponent> _ftlQuery;

    private EntityQuery<CEZMapComponent> _zMapQuery;
    private EntityQuery<WGPlanetNetworkComponent> _networkQuery;
    private EntityQuery<WGOrbitLayerComponent> _orbitQuery;
    private EntityQuery<TransformComponent> _xformQuery;
    private EntityQuery<OrganComponent> _organQuery;
    private EntityQuery<MindContainerComponent> _mindQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _zMapQuery = GetEntityQuery<CEZMapComponent>();
        _networkQuery = GetEntityQuery<WGPlanetNetworkComponent>();
        _orbitQuery = GetEntityQuery<WGOrbitLayerComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
        _organQuery = GetEntityQuery<OrganComponent>();
        _mindQuery = GetEntityQuery<MindContainerComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _ftlQuery = GetEntityQuery<FTLComponent>();

        Subs.CVar(_cfg, LuaMCVars.OffPlanetDormancy /* LuaM: WolfmedCVars */, value => _offPlanetDormancy = value, true);
        SubscribeLocalEvent<PlayerAttachedEvent>(_ => _nextRefresh = TimeSpan.Zero);
    }

    /// <summary>Whether an entity is somewhere that should run: off-planet, or on a planet near a player.</summary>
    public bool IsActive(EntityUid uid, TransformComponent? xform = null)
    {
        if (!_xformQuery.Resolve(uid, ref xform, false) || xform.MapUid is not { } map || NetworkOf(map) is not { } network)
            return true;

        Refresh();

        return _activeNetworks.Contains(network)
            && _active.Contains((network, ChunkOf(_transform.GetWorldPosition(xform))));
    }

    /// <summary>Whether a spot on a map should run: off-planet, or on a planet near a player.</summary>
    public bool IsActiveAt(EntityUid map, Vector2 worldPosition)
    {
        if (NetworkOf(map) is not { } network)
            return true;

        Refresh();
        return _activeNetworks.Contains(network) && _active.Contains((network, ChunkOf(worldPosition)));
    }

    /// <summary>
    /// Whether a body, or an organ in one, may skip its life upkeep (breath, blood, metabolism): an NPC on planet ground
    /// no player is near, or with wolfmed.off_planet_dormancy one off-planet (see <see cref="OffPlanetSleeps"/>). A body
    /// with a mind keeps going wherever it is.
    /// </summary>
    public bool BodySleeps(EntityUid uid)
    {
        if (_organQuery.TryComp(uid, out var organ) && organ.Body is { } body)
            uid = body;

        if (_mindQuery.TryComp(uid, out var mind) && mind.HasMind)
            return false;

        if (!IsActive(uid))
            return true;

        return _offPlanetDormancy && OffPlanetSleeps(uid);
    }

    /// <summary>
    /// Off-planet: a mindless body no player is near, on a grid nobody is flying, that is neither bleeding nor
    /// critical. Decided once per refresh, so a body asked about by every upkeep system pays for it once.
    /// </summary>
    private bool OffPlanetSleeps(EntityUid uid)
    {
        Refresh();
        if (_offPlanetSleeps.TryGetValue(uid, out var cached))
            return cached;

        var sleeps = DecideOffPlanet(uid);
        _offPlanetSleeps[uid] = sleeps;
        if (sleeps)
            _offPlanetSlept++;

        return sleeps;
    }

    private bool DecideOffPlanet(EntityUid uid)
    {
        if (!_xformQuery.TryComp(uid, out var xform) || xform.MapUid is not { } map)
            return false;

        if (xform.GridUid is { } grid && (_pilotedGrids.Contains(grid) || _ftlQuery.HasComp(grid)))
            return false;

        if (_offPlanetActive.Contains((map, ChunkOf(_transform.GetWorldPosition(xform)))))
            return false;

        return !_mobState.IsCritical(uid) && !(TryComp<Content.Server.Body.Components.BloodstreamComponent>(uid, out var blood) && blood.BleedAmount > 0f) /* LuaM: Wolfmed bleed rate */;
    }

    /// <summary>Whether a mover's body is at rest on planet ground no player is near, so its movement needs no handling.</summary>
    public bool MoverSleeps(EntityUid uid)
    {
        return _physicsQuery.TryComp(uid, out var body) && !body.Awake && !IsActive(uid);
    }

    /// <summary>Whether any player is anywhere on the planet a map belongs to; true off-planet.</summary>
    public bool IsMapActive(EntityUid map)
    {
        if (NetworkOf(map) is not { } network)
            return true;

        Refresh();
        return _activeNetworks.Contains(network);
    }

    /// <summary>
    /// Where on a planet anyone can see from: players' bodies (a vehicle, for someone in its cabin) and the eyes they
    /// watch through. Positions are shared by every layer of the planet.
    /// </summary>
    public IReadOnlyList<Vector2> Viewers(EntityUid network)
    {
        Refresh();
        return _viewers.TryGetValue(network, out var viewers) ? viewers : Array.Empty<Vector2>();
    }

    /// <summary>The planet a map is a layer of, or null off-planet and on an orbit layer.</summary>
    public EntityUid? PlanetOf(EntityUid map)
    {
        return NetworkOf(map);
    }

    /// <summary>Drops the cached player positions, so the next question sees where everyone is now.</summary>
    public void Invalidate()
    {
        _nextRefresh = TimeSpan.Zero;
    }

    private EntityUid? NetworkOf(EntityUid map)
    {
        if (_networkOf.TryGetValue(map, out var cached))
            return cached;

        EntityUid? network = null;
        if (_zMapQuery.TryComp(map, out var zMap)
            && _networkQuery.HasComp(zMap.NetworkUid)
            && !_orbitQuery.HasComp(map))
        {
            network = zMap.NetworkUid;
        }

        _networkOf[map] = network;
        return network;
    }

    private void Refresh()
    {
        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshEvery;
        _active.Clear();
        _activeNetworks.Clear();
        _networkOf.Clear();
        foreach (var viewers in _viewers.Values)
        {
            viewers.Clear();
        }

        var reach = (int) MathF.Ceiling(Range / ChunkSize);
        RefreshOffPlanet();
        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } body)
                continue;

            AddEyes(session, reach);

            if (!_xformQuery.TryComp(body, out var xform) || xform.MapUid is not { } map)
                continue;

            if (NetworkOf(map) is not { } network)
            {
                AddOffPlanetViewer(map, _transform.GetWorldPosition(xform), reach);
                continue;
            }

            _activeNetworks.Add(network);
            var position = _transform.GetWorldPosition(xform);
            _viewers.GetOrNew(network).Add(position);
            var centre = ChunkOf(position);
            for (var x = -reach; x <= reach; x++)
            for (var y = -reach; y <= reach; y++)
            {
                _active.Add((network, centre + new Vector2i(x, y)));
            }
        }
    }

    /// <summary>Notes the eyes a player watches through, such as a camera, as viewers of their planet.</summary>
    private void AddEyes(ICommonSession session, int reach)
    {
        foreach (var eye in session.ViewSubscriptions)
        {
            if (!_xformQuery.TryComp(eye, out var xform) || xform.MapUid is not { } map)
                continue;

            if (NetworkOf(map) is { } network)
                _viewers.GetOrNew(network).Add(_transform.GetWorldPosition(xform));
            else
                AddOffPlanetViewer(map, _transform.GetWorldPosition(xform), reach);
        }
    }

    /// <summary>Starts a refresh window for the off-planet rule: piloted grids, and the counter's log line.</summary>
    private void RefreshOffPlanet()
    {
        _offPlanetActive.Clear();
        _offPlanetSleeps.Clear();
        _pilotedGrids.Clear();
        if (!_offPlanetDormancy)
            return;

        var pilots = EntityQueryEnumerator<PilotComponent, TransformComponent>();
        while (pilots.MoveNext(out _, out var pilot, out var pilotXform))
        {
            var xform = pilot.Console is { } console && _xformQuery.TryComp(console, out var consoleXform)
                ? consoleXform
                : pilotXform;

            if (xform.GridUid is { } grid)
                _pilotedGrids.Add(grid);
        }

        if (_timing.CurTime < _nextOffPlanetLog)
            return;

        _nextOffPlanetLog = _timing.CurTime + OffPlanetLogEvery;
        if (_offPlanetSlept > 0)
            Log.Debug($"Off-planet dormancy slept {_offPlanetSlept} body decisions in the last {OffPlanetLogEvery.TotalSeconds:0} s.");

        _offPlanetSlept = 0;
    }

    /// <summary>Marks the chunks round an off-planet viewer awake.</summary>
    private void AddOffPlanetViewer(EntityUid map, Vector2 position, int reach)
    {
        if (!_offPlanetDormancy)
            return;

        var centre = ChunkOf(position);
        for (var x = -reach; x <= reach; x++)
        for (var y = -reach; y <= reach; y++)
        {
            _offPlanetActive.Add((map, centre + new Vector2i(x, y)));
        }
    }

    private static Vector2i ChunkOf(Vector2 position)
    {
        return new Vector2i((int) MathF.Floor(position.X / ChunkSize), (int) MathF.Floor(position.Y / ChunkSize));
    }
}
