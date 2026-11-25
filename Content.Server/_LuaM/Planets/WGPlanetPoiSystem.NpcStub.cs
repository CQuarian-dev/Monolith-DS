using System.Numerics;
using System.Threading.Tasks;
using Content.Shared._WG.PlanetPois;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

// -- LuaM: NPC, лагеря и техника точек интереса не переносятся до шага 5; здесь пустые версии их методов.
namespace Content.Server._WG.PlanetPois;

public sealed partial class WGPlanetPoiSystem
{
    private Task PlaceCampsAsync(EntityUid ground, MapGridComponent grid, BiomeComponent biome, EntityUid orbit, Vector2 centre, float radius,
        WGPlanetPoiTablePrototype table, WGPlanetPoiComponent record, HashSet<EntityUid> created)
    {
        return Task.CompletedTask;
    }

    private Task SpawnPendingCampsAsync(Entity<BiomeComponent> ground, MapGridComponent grid, WGPlanetPoiComponent record)
    {
        record.PendingCamps.Clear();
        return Task.CompletedTask;
    }

    private Task SpawnVehiclesAsync(Entity<BiomeComponent> ground, MapGridComponent grid, WGPlanetPoiComponent record)
    {
        record.PendingVehicles = null;
        return Task.CompletedTask;
    }

    private Task SpawnRoadPatrolsAsync(EntityUid ground, MapGridComponent grid, WGPlanetPoiComponent record)
    {
        record.PendingRoadPatrols.Clear();
        return Task.CompletedTask;
    }

    private void SpawnHome(EntProtoId id, EntityCoordinates centre, float radius)
    {
    }

    private HashSet<Vector2i> ClearCampGround(EntityUid ground, MapGridComponent grid, BiomeComponent biome, Vector2 spot, float camped)
    {
        var under = new HashSet<Vector2i>();
        var anchored = new List<EntityUid>();
        for (var x = (int) MathF.Floor(spot.X - camped); x <= (int) MathF.Ceiling(spot.X + camped); x++)
        for (var y = (int) MathF.Floor(spot.Y - camped); y <= (int) MathF.Ceiling(spot.Y + camped); y++)
        {
            var tile = new Vector2i(x, y);
            if (Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), spot) > camped)
                continue;
            under.Add(tile);
            anchored.Clear();
            var enumerator = _maps.GetAnchoredEntitiesEnumerator(ground, grid, tile);
            while (enumerator.MoveNext(out var uid))
                anchored.Add(uid.Value);
            foreach (var uid in anchored)
                QueueDel(uid);
        }
        var fill = new List<(Vector2i, Tile)>();
        foreach (var tile in under)
        {
            if (_maps.GetTileRef(ground, grid, tile).Tile.IsEmpty && _biome.TryGetBiomeTile(ground, grid, tile, out var natural))
                fill.Add((tile, natural.Value));
        }
        if (fill.Count > 0)
            _maps.SetTiles(ground, grid, fill);
        _biome.WgPinTiles((ground, biome), under);
        return under;
    }
}
