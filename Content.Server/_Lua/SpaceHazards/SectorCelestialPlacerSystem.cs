using System.Numerics;
using Content.Shared.Station.Components; // LuaM
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Radiation.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Lua.SpaceHazards;

public sealed partial class SectorCelestialPlacerSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SectorPixelPlanetLightSystem _lights = default!;
    [Dependency] private SectorLandmarkAnchorSystem _landmarks = default!;
    [Dependency] private SharedTransformSystem _transform = default!; // LuaM

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SectorCelestialPlacerControllerComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectorCelestialBodyComponent, MapInitEvent>(OnBodyMapInit);
    }

    private void OnStartup(EntityUid uid, SectorCelestialPlacerControllerComponent component, ComponentStartup args)
    { TrySpawn(uid, component); }

    public void InitializePlacer(EntityUid mapUid)
    {
        if (!TryComp(mapUid, out SectorCelestialPlacerControllerComponent? placer)) return;
        TrySpawn(mapUid, placer);
    }

    private void TrySpawn(EntityUid mapUid, SectorCelestialPlacerControllerComponent placer)
    {
        if (placer.Spawned) return;
        if (!TryComp<MapComponent>(mapUid, out var map)) return;
        var kind = placer.ForceKind ?? (_random.Prob(0.5f) ? CelestialKind.Star : CelestialKind.BlackHole);
        var proto = kind == CelestialKind.Star ? placer.StarPrototype : placer.BlackHolePrototype;
        if (!_prototypes.HasIndex<EntityPrototype>(proto))
        {
            Log.Error($"Celestial prototype '{proto}' missing for map {ToPrettyString(mapUid)}");
            return;
        }
        // LuaM start: keep clear of stations and POIs
        var stationGrids = CollectStationGrids(map.MapId);
        var pos = Vector2.Zero;
        for (var attempt = 0; attempt < 64; attempt++)
        {
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = placer.MaxSpawnDistance is { } maxDist ? _random.NextFloat(placer.SpawnDistance, maxDist) : placer.SpawnDistance;
            pos = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;
            if (!OverlapsStation(pos, placer.MinStationClearance, stationGrids))
                break;
        }
        // LuaM end
        var ent = Spawn(proto, new MapCoordinates(pos, map.MapId));
        if (!TryComp<SectorCelestialBodyComponent>(ent, out var body))
        {
            QueueDel(ent);
            return;
        }
        body.Kind = kind;
        RandomizeShaderParams(ent, body, map.MapId);
        Dirty(ent, body);
        _lights.ApplyCelestial(ent, body);
        SyncRadiationSource(ent, body);
        _landmarks.LockToMap(ent);
        placer.Spawned = true;
        Log.Info($"Spawned sector {kind} palette={body.Palette} seed={body.Seed:F2} at {pos} on {ToPrettyString(mapUid)}");
    }

    // LuaM start
    private List<(Vector2 Pos, float Extent)> CollectStationGrids(MapId mapId)
    {
        var grids = new List<(Vector2, float)>();
        var query = EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out _, out var data))
        {
            foreach (var gridUid in data.Grids)
            {
                if (!TryComp(gridUid, out TransformComponent? xform) || xform.MapID != mapId ||
                    !TryComp(gridUid, out MapGridComponent? grid))
                    continue;

                grids.Add((_transform.GetWorldPosition(xform), MathF.Max(grid.LocalAABB.Width, grid.LocalAABB.Height) * 0.75f));
            }
        }

        return grids;
    }

    private static bool OverlapsStation(Vector2 pos, float clearance, List<(Vector2 Pos, float Extent)> grids)
    {
        foreach (var (gridPos, extent) in grids)
        {
            var minDist = clearance + extent;
            if ((pos - gridPos).LengthSquared() < minDist * minDist)
                return true;
        }

        return false;
    }
    // LuaM end

    private void OnBodyMapInit(EntityUid uid, SectorCelestialBodyComponent body, MapInitEvent args)
    {
        if (!body.VisualsInitialized)
        {
            var mapId = Transform(uid).MapID;
            RandomizeShaderParams(uid, body, mapId);
        }
        Dirty(uid, body);
        _lights.ApplyCelestial(uid, body);
        SyncRadiationSource(uid, body);
        _landmarks.LockToMap(uid);
    }

    private void SyncRadiationSource(EntityUid uid, SectorCelestialBodyComponent body)
    {
        if (!TryComp(uid, out RadiationSourceComponent? source)) return;
        var peak = SectorCelestialMobDamage.GetDamageAmount(
            body.MobRadiationDamage,
            SectorCelestialMobDamage.RadiationDamageType);
        SectorCelestialMobDamage.SyncRadiationSource(uid, source, body.RadiationRange, peak);
    }

    private void RandomizeShaderParams(EntityUid uid, SectorCelestialBodyComponent body, MapId mapId)
    {
        body.Seed = 0.01f + _random.NextFloat() * 9.99f;
        var mix = uid.Id * 2654435761u ^ (uint) mapId.GetHashCode() ^ (uint) _random.Next();
        body.Palette = (byte) (mix % (uint) SectorBackgroundPlanetPlacerSystem.PaletteCount);
        body.VisualsInitialized = true;
    }
}
