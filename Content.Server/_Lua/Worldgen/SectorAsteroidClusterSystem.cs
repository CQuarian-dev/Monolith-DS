using System.Numerics;
using Content.Shared._Lua.SpaceHazards;
using Content.Shared.Station.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Lua.Worldgen;

public sealed partial class SectorAsteroidClusterSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SectorAsteroidClusterControllerComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<SectorAsteroidClusterControllerComponent> ent, ref ComponentStartup args)
    {
        EnsureClusters(ent.Owner, ent.Comp);
    }

    public void EnsureClusters(EntityUid mapUid, SectorAsteroidClusterControllerComponent? controller = null)
    {
        if (!Resolve(mapUid, ref controller, false) || controller.Spawned)
            return;

        controller.Spawned = true;
        if (!TryComp<MapComponent>(mapUid, out var map))
            return;

        var stations = CollectStationGrids(map.MapId);
        var count = _random.Next(controller.MinCount, controller.MaxCount + 1);
        var empty = Math.Min(count, _random.Next(controller.MinEmptyCount, controller.MaxEmptyCount + 1));
        var minDistSq = controller.MinDistance * controller.MinDistance;
        var maxDistSq = controller.MaxDistance * controller.MaxDistance;

        for (var attempt = 0; attempt < count * 64 && controller.Clusters.Count < count; attempt++)
        {
            var hasPlanet = controller.Clusters.Count >= empty;
            var radius = hasPlanet
                ? _random.NextFloat(controller.PlanetClusterMinRadius, controller.PlanetClusterMaxRadius)
                : _random.NextFloat(controller.EmptyClusterMinRadius, controller.EmptyClusterMaxRadius);
            var angle = _random.NextFloat() * MathF.Tau;
            var dist = MathF.Sqrt(minDistSq + _random.NextFloat() * (maxDistSq - minDistSq));
            var center = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * dist;

            if (Overlaps(center, radius, controller, stations))
                continue;

            controller.Clusters.Add(new SectorAsteroidCluster(center, radius, hasPlanet));
        }

        var markers = EnsureComp<SectorAsteroidClusterMarkersComponent>(mapUid);
        markers.Centers.Clear();
        markers.Radii.Clear();
        foreach (var cluster in controller.Clusters)
        {
            markers.Centers.Add(cluster.Center);
            markers.Radii.Add(cluster.Radius);
        }
        Dirty(mapUid, markers);

        Log.Info($"Placed {controller.Clusters.Count}/{count} asteroid clusters on {ToPrettyString(mapUid)}");
    }

    public bool IsInCluster(EntityUid mapUid, Vector2 worldPos)
    {
        if (!TryComp<SectorAsteroidClusterControllerComponent>(mapUid, out var controller))
            return false;

        foreach (var cluster in controller.Clusters)
        {
            if ((worldPos - cluster.Center).LengthSquared() <= cluster.Radius * cluster.Radius)
                return true;
        }

        return false;
    }

    private static bool Overlaps(
        Vector2 center,
        float radius,
        SectorAsteroidClusterControllerComponent controller,
        List<(Vector2 Pos, float Extent)> stations)
    {
        foreach (var (pos, extent) in stations)
        {
            var minDist = radius + extent + controller.MinStationClearance;
            if ((center - pos).LengthSquared() < minDist * minDist)
                return true;
        }

        foreach (var other in controller.Clusters)
        {
            var minDist = radius + other.Radius + controller.MinSeparation;
            if ((center - other.Center).LengthSquared() < minDist * minDist)
                return true;
        }

        return false;
    }

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

                grids.Add((_transform.GetWorldPosition(xform), MathF.Max(grid.LocalAABB.Width, grid.LocalAABB.Height) * 0.5f));
            }
        }

        return grids;
    }
}
