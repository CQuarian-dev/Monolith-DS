using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Server._CE.ZLevels.Core;
using Content.Server._CE.ZLevels.Core.Components;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.PlanetCities;
using Content.Shared._WG.Planets;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using static Content.IntegrationTests.Tests._WG.Planets.PlanetFixture;

namespace Content.IntegrationTests.Tests._WG.Planets;

[TestFixture]
public sealed class ZLevelImpactTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task FastDescentHitsTheFloorOnce(bool continueBelow)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await EnableFeature(pair);
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var levels = server.System<CEZLevelsSystem>();
        var explosions = server.System<ExplosionSystem>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();

        await server.WaitAssertion(() =>
        {
            var middle = maps.CreateMap(out var middleId);
            var top = maps.CreateMap(out var topId);
            var layers = new Dictionary<EntityUid, int> { [middle] = 0, [top] = 1 };
            if (continueBelow)
                layers.Add(maps.CreateMap(), -1);
            Assert.That(levels.TryAddMapsIntoNetwork(levels.CreateMapNetwork(), layers), Is.True);

            var floor = new Tile(tileDefs[FloorTile].TileId);
            var platform = maps.CreateGridEntity(middleId);
            var ship = maps.CreateGridEntity(topId);
            maps.SetTile(platform, platform.Comp, Vector2i.Zero, floor);
            maps.SetTile(ship, ship.Comp, Vector2i.Zero, floor);
            Assert.That(levels.TryEnterTransit(ship, startProgress: 0.1f), Is.True);

            var faller = entMan.GetComponent<CEZGridFallerComponent>(ship);
            faller.Velocity = faller.GridCrashVelocity;
            Assert.That(levels.SetTransitAltitude(ship, -0.1f), Is.True);
            Assert.That(entMan.HasComponent<CEZTransitMapComponent>(entMan.GetComponent<TransformComponent>(ship).MapUid),
                Is.EqualTo(continueBelow));

            var impactIntensity = 0f;
            foreach (var explosion in QueuedExplosions(explosions))
                impactIntensity += explosion.TotalIntensity;

            // A single impact on these one-tile grids queues 300 intensity per grid.
            Assert.That(impactIntensity, Is.EqualTo(600f));
        });

        await pair.CleanReturnAsync();
    }

    [TestCase(false, true, false)]
    [TestCase(true, false, false)]
    [TestCase(true, true, true)]
    public async Task RoofCollapseOnlyAffectsEnabledPlanetTerrain(bool enabled, bool city, bool collapses)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        if (enabled)
            await EnableFeature(pair);
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var roofs = server.System<SharedRoofSystem>();
        var xform = server.System<SharedTransformSystem>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();
        var roofTile = new Vector2i(1, 0);
        Entity<MapGridComponent, RoofComponent> roofGrid = default;

        await server.WaitAssertion(() =>
        {
            var map = maps.CreateMap(out var mapId);
            entMan.AddComponent<WGPlanetLayerComponent>(map);
            var grid = maps.CreateGridEntity(mapId);
            if (city)
                entMan.AddComponent<WGPlanetCityGridComponent>(grid);

            var floor = new Tile(tileDefs[FloorTile].TileId);
            maps.SetTiles(grid, grid.Comp, [(Vector2i.Zero, floor), (roofTile, floor)]);
            var wall = entMan.SpawnEntity("WallSolid", new EntityCoordinates(grid, new Vector2(0.5f)));
            Assert.That(xform.AnchorEntity(wall), Is.True);
            var roof = entMan.EnsureComponent<RoofComponent>(grid);
            roofGrid = (grid.Owner, grid.Comp, roof);
            roofs.SetRoof((grid.Owner, grid.Comp, roof), roofTile, true);
            xform.Unanchor(wall);
            entMan.DeleteEntity(wall);
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() => Assert.That(roofs.IsRooved(roofGrid, roofTile), Is.EqualTo(!collapses)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SparseTileRemovalWakesOnlyBodiesOverTheRemovedTiles()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        await EnableFeature(pair);
        var server = pair.Server;
        var entMan = server.EntMan;
        var maps = server.System<SharedMapSystem>();
        var levels = server.System<CEZLevelsSystem>();
        var tileDefs = server.ResolveDependency<ITileDefinitionManager>();

        await server.WaitAssertion(() =>
        {
            var map = maps.CreateMap(out var mapId);
            Assert.That(levels.TryAddMapsIntoNetwork(levels.CreateMapNetwork(), new() { [map] = 0 }), Is.True);
            var grid = maps.CreateGridEntity(mapId);
            var floor = new Tile(tileDefs[FloorTile].TileId);
            var removedTile = Vector2i.Zero;
            var keptTile = new Vector2i(1, 0);
            var farTile = new Vector2i(2, 0);
            maps.SetTiles(grid, grid.Comp, [(removedTile, floor), (keptTile, floor), (farTile, floor)]);

            var overHole = entMan.SpawnEntity("Crowbar", new EntityCoordinates(grid, new Vector2(0.5f)));
            var onFloor = entMan.SpawnEntity("Crowbar", new EntityCoordinates(grid, new Vector2(1.5f, 0.5f)));
            var removedBody = entMan.EnsureComponent<CEZPhysicsComponent>(overHole);
            var keptBody = entMan.EnsureComponent<CEZPhysicsComponent>(onFloor);
            levels.SleepBody((overHole, removedBody));
            levels.SleepBody((onFloor, keptBody));

            maps.SetTiles(grid, grid.Comp, [(removedTile, Tile.Empty), (farTile, Tile.Empty)]);
            Assert.That(removedBody.Sleeping, Is.False, "The body over the new hole must start falling.");
            Assert.That(keptBody.Sleeping, Is.True, "The body between the holes still has support.");
        });
        await pair.CleanReturnAsync();
    }

    // Observe the queued impacts before a tick can apply or merge them with unrelated damage.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_queuedExplosions")]
    private static extern ref HashSet<QueuedExplosion> QueuedExplosions(ExplosionSystem system);
}
