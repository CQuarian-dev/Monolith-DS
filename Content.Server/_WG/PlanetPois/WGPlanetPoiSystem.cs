using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server._WG.Caverns;
using Content.Server._WG.Planets;
using Content.Server._WG.Planets.Atmosphere;
using Content.Server._WG.Planets.Bounds;
using Content.Server.Parallax;
using Content.Server.Popups;
using Content.Server.Procedural;
using Content.Shared._WG.CCVar;
using Content.Shared._WG.PlanetPois;
using Content.Shared._WG.Planets;
using Content.Shared.Ghost;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Parallax.Biomes;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._WG.PlanetPois;

/// <summary>Places pinned points of interest and connecting roads before terrain, with discoverable orbit signals.</summary>
public sealed partial class WGPlanetPoiSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _sharedRandom = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BiomeSystem _biome = default!;
    [Dependency] private DungeonSystem _dungeon = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedRoofSystem _roof = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private WGPlanetPreloadSystem _preload = default!;
    [Dependency] private WGTerrainAtmosphereSystem _terrainAtmos = default!;

    /// <summary>The orbit-layer marker over a site.</summary>
    public const string SignalProto = "WGPoiSignal";

    private static readonly TimeSpan RevealInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextReveal;

    /// <summary>
    /// The running placement's own generator, seeded once from the shared one when it starts. Placement spreads over
    /// ticks by a wall-clock budget, and other systems draw from the shared generator in between, so drawing from it
    /// directly gave a different world for the same seed on a slower machine.
    /// </summary>
    private readonly AsyncLocal<IRobustRandom?> _placementRandom = new();

    /// <summary>The placement's generator inside a placement, the shared one elsewhere.</summary>
    private IRobustRandom _random => _placementRandom.Value ?? _sharedRandom;

    public override void Initialize()
    {
        base.Initialize();
        // Ahead of the cavern mouths, so a fixed city can hold its spot before they claim the ground.
        SubscribeLocalEvent<WGPlanetPreloadStartingEvent>(OnPreloadStarting, before: new[] { typeof(WGCavernMouthSystem) });
        SubscribeLocalEvent<WGPlanetPreloadedEvent>(OnPreloaded);
    }

    private void OnPreloadStarting(ref WGPlanetPreloadStartingEvent args)
    {
        if (!TryComp<WGPlanetLayerComponent>(args.Ground, out var layer)
            || !TryGetEntity(layer.Network, out var network)
            || !TryComp<WGPlanetNetworkComponent>(network, out var comp)
            || !_proto.TryIndex(comp.Surface, out var surface))
            return;

        var forced = _cfg.GetCVar(PlanetCVars.PoiTable);
        var tableId = forced.Length > 0 ? new ProtoId<WGPlanetPoiTablePrototype>(forced) : surface.Pois;

        if (tableId is not { } id)
            return;
        if (!_proto.TryIndex(id, out var table))
        {
            var error = $"Unknown POI table {id}; correct the surface prototype or poi_table override.";
            EnsureComp<WGPlanetPoiComponent>(args.Ground).GenerationError = error;
            Log.Error(error);
            args.Holds.Add(Task.FromException(new InvalidOperationException(error)));
            return;
        }

        args.Holds.Add(PlaceAsync(args.Ground, comp, args.Centre, args.Radius, table, args.Claims));
    }

    /// <summary>The marker on the orbit layer over a site, unidentified until someone on the ground reaches it.</summary>
    private EntityUid SpawnSignal(EntityUid ground, EntityUid orbit, Vector2i origin, WGPlanetPoiEntry entry, WGPlanetPoiTablePrototype table)
    {
        if (TerminatingOrDeleted(orbit))
            return EntityUid.Invalid;

        var signal = Spawn(SignalProto, new EntityCoordinates(orbit, new Vector2(origin.X + 0.5f, origin.Y + 0.5f)));
        _meta.SetEntityName(signal, Loc.GetString("wg-poi-unknown-signal"));

        var comp = EnsureComp<WGPoiSignalComponent>(signal);
        comp.Ground = ground;
        comp.Origin = origin;
        comp.Name = entry.Name;
        comp.RevealRange = table.RevealRange;
        return signal;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextReveal)
            return;

        _nextReveal = _timing.CurTime + RevealInterval;

        var query = EntityQueryEnumerator<WGPoiSignalComponent>();
        while (query.MoveNext(out var uid, out var signal))
        {
            if (signal.Revealed)
                continue;

            var site = new Vector2(signal.Origin.X + 0.5f, signal.Origin.Y + 0.5f);

            foreach (var session in _players.Sessions)
            {
                // Ghosts and admin observers only look; a site is found by someone there in body.
                if (session.AttachedEntity is not { } player
                    || HasComp<GhostComponent>(player)
                    || Transform(player).MapUid != signal.Ground
                    || Vector2.DistanceSquared(_transform.GetWorldPosition(player), site) > signal.RevealRange * signal.RevealRange)
                    continue;

                Reveal((uid, signal), player);
                break;
            }
        }
    }

    /// <summary>Names the signal after its site and tells the finder.</summary>
    public void Reveal(Entity<WGPoiSignalComponent> signal, EntityUid? finder)
    {
        var name = Loc.GetString(signal.Comp.Name);
        signal.Comp.Revealed = true;
        _meta.SetEntityName(signal, name);

        if (finder is { } player)
            _popup.PopupEntity(Loc.GetString("wg-poi-signal-identified", ("name", name)), player, player);
    }
}
