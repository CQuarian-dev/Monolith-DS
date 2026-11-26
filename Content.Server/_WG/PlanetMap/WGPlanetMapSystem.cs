using System.Linq;
using System.Numerics;
using Content.Server._WG.PlanetPois;
using Content.Server._WG.Planets;
using Content.Server._WG.Planets.Bounds;
using Content.Shared._WG.CCVar;
using Content.Shared._WG.PlanetCities;
using Content.Shared._WG.PlanetMap;
using Content.Shared._WG.PlanetPois;
using Content.Shared._WG.Planets;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._WG.PlanetMap;

/// <summary>
/// Draws up each world's map on its orbit layer: the ground in coarse cells once the world exists, its roads, footpaths
/// and tunnels as polylines and its sites once they are placed, and a site's name once someone identifies it.
/// </summary>
public sealed partial class WGPlanetMapSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;
    [Dependency] private SharedBiomeSystem _biome = default!;
    [Dependency] private WGPlanetPreloadSystem _preload = default!;

    /// <summary>The most polyline points a world's roads, footpaths and tunnels may take in all.</summary>
    public const int MaxPoints = 4000;

    /// <summary>How far out an unbounded world's terrain is sampled.</summary>
    public const float UnboundedExtent = 384f;

    /// <summary>The fewest tiles a terrain cell spans.</summary>
    public const int MinCell = 8;

    private const string WreckName = "wg-poi-wreck";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);
    private TimeSpan _next;
    private readonly Dictionary<int, WGPlanetMapGround> _tileGrounds = new();

    /// <summary>Terrain samples under way, by orbit layer: a world's whole map takes a few ticks to sample.</summary>
    private readonly Dictionary<EntityUid, TerrainSampling> _sampling = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WGPlanetPreloadedEvent>(OnPreloaded, after: new[] { typeof(WGPlanetPoiSystem) });
    }

    private void OnPreloaded(ref WGPlanetPreloadedEvent args)
    {
        var query = EntityQueryEnumerator<WGPlanetNetworkComponent>();
        while (query.MoveNext(out _, out var network))
        {
            if (network.GroundMap == args.Ground)
                Refresh(network);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // A terrain sample under way carries on every tick.
        if (_timing.CurTime < _next && _sampling.Count == 0)
            return;
        if (_timing.CurTime >= _next)
            _next = _timing.CurTime + Interval;

        foreach (var orbit in _sampling.Keys.ToList())
        {
            if (TerminatingOrDeleted(orbit))
                _sampling.Remove(orbit);
        }

        var query = EntityQueryEnumerator<WGPlanetNetworkComponent>();
        while (query.MoveNext(out _, out var network))
        {
            Refresh(network);
        }
    }

    /// <summary>Brings one world's map up to date; cheap once it is drawn up.</summary>
    public void Refresh(WGPlanetNetworkComponent network)
    {
        var orbitUid = network.OrbitMap;
        if (TerminatingOrDeleted(orbitUid) || !TryComp<WGOrbitLayerComponent>(orbitUid, out var orbit) || orbit.RadarLayers.Count == 0)
            return;

        var map = EnsureComp<WGPlanetMapComponent>(orbitUid);
        if (!MathHelper.CloseTo(map.TerrainRadius, orbit.BoundsRadius))
        {
            var centre = orbit.BoundsRadius > 0f ? orbit.BoundsCentre : network.Centre;
            if (!_sampling.TryGetValue(orbitUid, out var sampling) || !MathHelper.CloseTo(sampling.Radius, orbit.BoundsRadius))
                _sampling[orbitUid] = sampling = BeginSample(orbit, centre, orbit.BoundsRadius > 0f ? orbit.BoundsRadius : UnboundedExtent, orbit.BoundsRadius);

            // Sampled within the world-generation budget; the rest waits for the next tick.
            if (!SampleRows(sampling, () => _preload.GenerationSpent()))
                return;

            _sampling.Remove(orbitUid);
            map.Centre = centre;
            map.Radius = orbit.BoundsRadius;
            map.LandingRadius = LandingRadius(network);
            map.Terrain = EndSample(sampling);
            map.TerrainRadius = orbit.BoundsRadius;
            Dirty(orbitUid, map);
        }

        var ground = network.GroundMap;
        if (!TryComp<WGPlanetPoiComponent>(ground, out var record) || !record.NetworkReady)
            return;

        if (map.RoadsFrom != ground)
        {
            map.RoadsFrom = ground;
            DrawRoads(map, record);
            map.Sites = Sites(record);
            Dirty(orbitUid, map);
            return;
        }

        var sites = Sites(record);
        if (sites.SequenceEqual(map.Sites))
            return;
        map.Sites = sites;
        DirtyField(orbitUid, map, nameof(WGPlanetMapComponent.Sites));
    }

    /// <summary>The roads, footpaths and tunnels of a ground, within the point budget, roads first.</summary>
    private void DrawRoads(WGPlanetMapComponent map, WGPlanetPoiComponent record)
    {
        var centreline = record.Centreline;
        map.RoadWidth = centreline.Count > 0 ? MathF.Max(1f, (float) record.RoadTiles.Count / centreline.Count) : 3f;
        map.ShoulderWidth = centreline.Count > 0 ? (float) record.ShoulderTiles.Count / centreline.Count : 0f;

        var tunnels = new HashSet<Vector2i>();
        foreach (var tunnel in record.Tunnels)
            tunnels.UnionWith(tunnel);

        var lines = WGPlanetMapGeometry.Lines(centreline, WGPlanetMapLineKind.Road,
            tunnels.Count > 0 ? tunnels.Contains : null, MaxPoints);
        var used = lines.Sum(line => line.Points.Length);
        var trails = new HashSet<Vector2i>(record.TrailTiles);
        trails.ExceptWith(record.RoadTiles);
        lines.AddRange(WGPlanetMapGeometry.Lines(trails, WGPlanetMapLineKind.Trail, null, MaxPoints - used));
        map.Lines = lines;
    }

    /// <summary>
    /// The map's markers for a ground's sites, in the record's order. <paramref name="all"/> names every one, identified
    /// or not, for an admin's True IFF.
    /// </summary>
    public List<WGPlanetMapSite> Sites(WGPlanetPoiComponent record, bool all = false)
    {
        var sites = new List<WGPlanetMapSite>(record.Sites.Count);
        foreach (var site in record.Sites)
        {
            var revealed = TryComp<WGPoiSignalComponent>(site.Signal, out var signal) && signal.Revealed || all;
            var kind = KindOf(site);
            var position = signal?.Origin ?? site.Origin;
            if (!revealed)
            {
                // A city or a cargo depot shows from orbit; anything else is just a signal until someone gets there.
                sites.Add(new WGPlanetMapSite(kind is WGPlanetMapSiteKind.City or WGPlanetMapSiteKind.Depot ? kind : WGPlanetMapSiteKind.Unknown,
                    position, site.Bounds, string.Empty));
                continue;
            }

            sites.Add(new WGPlanetMapSite(kind, position, site.Bounds, site.Name.Id));
        }

        return sites;
    }

    /// <summary>What a site is, from the prototype it came from or the words in its name and map.</summary>
    public WGPlanetMapSiteKind KindOf(WGPlanetPoiSite site)
    {
        var name = site.Name.Id;
        if (name == WreckName)
            return WGPlanetMapSiteKind.Wreck;
        foreach (var city in _proto.EnumeratePrototypes<WGFixedCityPrototype>())
        {
            if (city.Name == site.Name)
                return WGPlanetMapSiteKind.City;
        }
        foreach (var camp in _proto.EnumeratePrototypes<WGPlanetCampPrototype>())
        {
            if (camp.Name == site.Name)
                return WGPlanetMapSiteKind.Camp;
        }

        var words = (name + " " + site.MapPath + " " + site.Dungeon).ToLowerInvariant();
        if (words.Contains("city") || words.Contains("town"))
            return WGPlanetMapSiteKind.City;
        if (words.Contains("vault"))
            return WGPlanetMapSiteKind.Vault;
        if (words.Contains("gas-station") || words.Contains("gas_station") || words.Contains("fuel"))
            return WGPlanetMapSiteKind.Fuel;
        if (words.Contains("house") || words.Contains("hideout") || words.Contains("dorms") || words.Contains("cabin"))
            return WGPlanetMapSiteKind.House;
        if (words.Contains("camp"))
            return WGPlanetMapSiteKind.Camp;
        return site.Dungeon != null ? WGPlanetMapSiteKind.Facility : WGPlanetMapSiteKind.Building;
    }

    /// <summary>The clear ground round the middle of the world, from the same POI table the sites came from.</summary>
    private float LandingRadius(WGPlanetNetworkComponent network)
    {
        var forced = _cfg.GetCVar(PlanetCVars.PoiTable);
        ProtoId<WGPlanetPoiTablePrototype>? id = forced.Length > 0 ? new ProtoId<WGPlanetPoiTablePrototype>(forced)
            : _proto.TryIndex(network.Surface, out var surface) ? surface.Pois : null;
        return id is { } tableId && _proto.TryIndex(tableId, out var table) ? table.CentreClear : 0f;
    }

    /// <summary>The world's terrain recipe sampled once a cell, never loading or reading ground chunks.</summary>
    public WGPlanetMapTerrain SampleTerrain(WGOrbitLayerComponent orbit, Vector2 centre, float extent)
    {
        var sampling = BeginSample(orbit, centre, extent, extent);
        SampleRows(sampling, () => false);
        return EndSample(sampling);
    }

    private TerrainSampling BeginSample(WGOrbitLayerComponent orbit, Vector2 centre, float extent, float radius)
    {
        var side = (int) MathF.Ceiling(extent * 2f);
        var cell = Math.Max(MinCell, (int) MathF.Ceiling(side / 128f));
        var size = Math.Min(128, (int) MathF.Ceiling(side / (float) cell) + 1);
        var origin = new Vector2i((int) MathF.Floor(centre.X - size * cell / 2f), (int) MathF.Floor(centre.Y - size * cell / 2f));
        return new TerrainSampling(orbit, centre, extent, radius, cell, size, origin, new byte[size * size],
            new int[Enum.GetValues<WGPlanetMapGround>().Length]);
    }

    /// <summary>Samples rows until <paramref name="spent"/> says stop, at least one; true once every row is done.</summary>
    private bool SampleRows(TerrainSampling s, Func<bool> spent)
    {
        while (s.Row < s.Size)
        {
            var y = s.Row++;
            for (var x = 0; x < s.Size; x++)
            {
                var world = new Vector2(s.Origin.X + (x + 0.5f) * s.Cell, s.Origin.Y + (y + 0.5f) * s.Cell);
                var ground = Classify(s.Orbit, world, s.Centre, s.Extent);
                s.Cells[y * s.Size + x] = (byte) ground;
                s.Counts[(int) ground]++;
            }

            if (s.Row < s.Size && spent())
                return false;
        }

        return true;
    }

    private static WGPlanetMapTerrain EndSample(TerrainSampling s)
    {
        var (origin, cell, size, cells, counts) = (s.Origin, s.Cell, s.Size, s.Cells, s.Counts);
        var dominant = WGPlanetMapGround.Grass;
        for (var i = (int) WGPlanetMapGround.Void; i < counts.Length; i++)
        {
            if (counts[i] > counts[(int) dominant])
                dominant = (WGPlanetMapGround) i;
        }

        return new WGPlanetMapTerrain
        {
            Origin = origin,
            Cell = cell,
            Width = size,
            Height = size,
            Base = dominant,
            Runs = WGPlanetMapEncoding.Encode(cells),
        };
    }

    private WGPlanetMapGround Classify(WGOrbitLayerComponent orbit, Vector2 world, Vector2 centre, float extent)
    {
        if (Vector2.DistanceSquared(world, centre) > extent * extent)
            return WGPlanetMapGround.Outside;

        foreach (var scar in orbit.RadarScars)
        {
            if (Vector2.DistanceSquared(world, new Vector2(scar.X, scar.Y)) <= scar.Z * scar.Z)
                return WGPlanetMapGround.Void;
        }

        var indices = world.Floored();
        if (!_biome.TryGetBiomeTile(indices, orbit.RadarLayers, orbit.RadarSeed, (Entity<MapGridComponent>?) null, out var tile)
            || tile.Value.IsEmpty)
            return WGPlanetMapGround.Void;

        if (_biome.TryGetEntity(indices, orbit.RadarLayers, tile.Value, orbit.RadarSeed, (Entity<MapGridComponent>?) null, out var feature)
            && FeatureGround(feature) is { } fromFeature)
            return fromFeature;

        return TileGround(tile.Value.TypeId);
    }

    /// <summary>Lava, water and the like are biome entities, not tiles.</summary>
    public static WGPlanetMapGround? FeatureGround(string? feature)
    {
        if (feature == null)
            return null;
        var id = feature.ToLowerInvariant();
        if (id.Contains("bloodriver") || id.Contains("fleshtree") || id.Contains("fleshpolyp") || id.Contains("wallmeat") || id.Contains("fleshblocker"))
            return WGPlanetMapGround.Flesh;
        if (id.Contains("lava"))
            return WGPlanetMapGround.Lava;
        if (id.Contains("water"))
            return WGPlanetMapGround.Water;
        if (id.Contains("plasma"))
            return WGPlanetMapGround.Plasma;
        if (id.Contains("rock") || id.Contains("planetmapore") || id.Contains("boulder") || id.Contains("crystal"))
            return WGPlanetMapGround.Formation;
        if (id.Contains("tree") || id.Contains("bush"))
            return WGPlanetMapGround.Vegetation;
        return null;
    }

    private WGPlanetMapGround TileGround(int tile)
    {
        if (_tileGrounds.TryGetValue(tile, out var ground))
            return ground;
        var id = _tiles[tile].ID.ToLowerInvariant();
        ground = id.Contains("flesh") ? WGPlanetMapGround.Flesh
            : id.Contains("water") || id.Contains("ocean") || id.Contains("riverbed") ? WGPlanetMapGround.Water
            : id.Contains("lava") ? WGPlanetMapGround.Lava
            : id.Contains("snow") || id.Contains("ice") ? WGPlanetMapGround.Snow
            : id.Contains("sand") || id.Contains("dirt") || id.Contains("mud") || id.Contains("desert") ? WGPlanetMapGround.Dry
            : id.Contains("grass") || id.Contains("jungle") ? WGPlanetMapGround.Grass
            : WGPlanetMapGround.Rock;
        _tileGrounds[tile] = ground;
        return ground;
    }

    /// <summary>A terrain sample part way through: the grid it fills and the next row to sample.</summary>
    private sealed record TerrainSampling(WGOrbitLayerComponent Orbit, Vector2 Centre, float Extent, float Radius, int Cell, int Size,
        Vector2i Origin, byte[] Cells, int[] Counts)
    {
        public int Row;
    }
}
