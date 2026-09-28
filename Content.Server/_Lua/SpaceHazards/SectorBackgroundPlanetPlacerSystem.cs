using System.Numerics;
using Content.Server._Lua.Worldgen; // LuaM
using Content.Shared.Station.Components; // LuaM: Server > Shared
using Content.Shared._Lua.SpaceHazards;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lua.SpaceHazards;

public sealed partial class SectorBackgroundPlanetPlacerSystem : EntitySystem
{
    public const int PaletteCount = 9;
    private static readonly PixelPlanetKind[] PlanetKinds = Enum.GetValues<PixelPlanetKind>();
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SectorPixelPlanetLightSystem _lights = default!;
    [Dependency] private SectorLandmarkAnchorSystem _landmarks = default!;
    [Dependency] private SectorAsteroidClusterSystem _clusters = default!; // LuaM

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SectorBackgroundPlanetPlacerControllerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectorBackgroundPlanetComponent, MapInitEvent>(OnPlanetMapInit);
    }

    private void OnStartup(EntityUid uid, SectorBackgroundPlanetPlacerControllerComponent component, ComponentStartup args)
    {
        TrySpawn(uid, component);
    }

    public void InitializePlacer(EntityUid mapUid)
    {
        if (!TryComp(mapUid, out SectorBackgroundPlanetPlacerControllerComponent? placer))
            return;

        TrySpawn(mapUid, placer);
    }

    private void TrySpawn(EntityUid mapUid, SectorBackgroundPlanetPlacerControllerComponent placer)
    {
        if (placer.Spawned)
            return;

        if (!TryComp<MapComponent>(mapUid, out var map))
            return;

        if (!_prototypes.HasIndex<EntityPrototype>(placer.PlanetPrototype))
        {
            Log.Error($"Background planet prototype '{placer.PlanetPrototype}' missing");
            return;
        }

        var count = _random.Next(placer.MinCount, placer.MaxCount + 1);
        var stationCenters = CollectStationCenters(map.MapId);
        var spawned = 0;

        // LuaM start: one planet in the centre of every sector asteroid cluster
        if (TryComp<SectorAsteroidClusterControllerComponent>(mapUid, out var clusters))
        {
            _clusters.EnsureClusters(mapUid, clusters);
            foreach (var cluster in clusters.Clusters)
            {
                stationCenters.Add(cluster.Center);
                if (!cluster.HasPlanet)
                    continue;

                SpawnPlanet(placer, map.MapId, cluster.Center, _random.NextFloat(placer.AnchorMinSpriteRadius, placer.AnchorMaxSpriteRadius));
                spawned++;
            }
        }
        count += spawned;
        // LuaM end

        for (var attempt = 0; attempt < count * 8 && spawned < count; attempt++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = _random.NextFloat(placer.MinDistance, placer.MaxDistance);
            var pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

            if (OverlapsStation(pos, placer.MinStationClearance, stationCenters))
                continue;

            SpawnPlanet(placer, map.MapId, pos, _random.NextFloat(placer.MinSpriteRadius, placer.MaxSpriteRadius)); // LuaM: inline spawn > SpawnPlanet
            spawned++;
        }

        placer.Spawned = true;
        Log.Info($"Spawned {spawned} background planets on {ToPrettyString(mapUid)}");
    }

    // LuaM start
    private void SpawnPlanet(SectorBackgroundPlanetPlacerControllerComponent placer, MapId mapId, Vector2 pos, float spriteRadius)
    {
        var ent = Spawn(placer.PlanetPrototype, new MapCoordinates(pos, mapId));
        if (!TryComp<SectorBackgroundPlanetComponent>(ent, out var planet))
            return;

        RandomizeShaderParams(ent, planet, mapId);
        planet.SpriteRadius = spriteRadius;
        Dirty(ent, planet);
        _lights.ApplyPlanet(ent, planet);
        _landmarks.LockToMap(ent);
    }
    // LuaM end

    private void OnPlanetMapInit(EntityUid uid, SectorBackgroundPlanetComponent planet, MapInitEvent args)
    {
        if (!planet.VisualsInitialized)
        {
            var mapId = Transform(uid).MapID;
            RandomizeShaderParams(uid, planet, mapId);
        }

        Dirty(uid, planet);
        _lights.ApplyPlanet(uid, planet);
        _landmarks.LockToMap(uid);
    }

    private void RandomizeShaderParams(EntityUid uid, SectorBackgroundPlanetComponent planet, MapId mapId)
    {
        planet.PlanetKind = _random.Pick(PlanetKinds);
        planet.Seed = 0.01f + _random.NextFloat() * 9.99f;
        var mix = uid.Id * 2654435761u ^ (uint) mapId.GetHashCode() ^ (uint) _random.Next();
        planet.PaletteIndex = (byte) (mix % (uint) PaletteCount);
        planet.Rotation = _random.NextFloat() * MathF.Tau;
        planet.LightOriginX = _random.NextFloat(0.2f, 0.8f);
        planet.LightOriginY = _random.NextFloat(0.2f, 0.8f);
        planet.VisualsInitialized = true;
    }

    private List<Vector2> CollectStationCenters(MapId mapId)
    {
        var centers = new List<Vector2>();
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out _, out var data))
        {
            foreach (var gridUid in data.Grids)
            {
                if (!TryComp(gridUid, out TransformComponent? xform) || xform.MapID != mapId)
                    continue;

                centers.Add(_transform.GetWorldPosition(xform));
            }
        }

        return centers;
    }

    private static bool OverlapsStation(Vector2 pos, float clearance, List<Vector2> centers)
    {
        var clearSq = clearance * clearance;
        foreach (var c in centers)
        {
            if ((pos - c).LengthSquared() < clearSq)
                return true;
        }

        return false;
    }
}
