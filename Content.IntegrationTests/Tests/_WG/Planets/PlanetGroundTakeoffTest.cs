#nullable enable
using System.Numerics;
using Content.Server._CE.ZLevels.Core;
using Content.Server.Shuttles.Systems;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using static Content.IntegrationTests.Tests._WG.Planets.PlanetFixture;

namespace Content.IntegrationTests.Tests._WG.Planets;

/// <summary>
/// A console built on bare planet ground has no ship under it. Round 317: holding ascend there lifted the whole planet
/// map into a transit map and physics stopped for the server.
/// </summary>
[TestFixture]
[TestOf(typeof(CEZLevelsSystem))]
public sealed class PlanetGroundTakeoffTest
{
    [Test]
    public async Task PlanetGroundNeverFlies()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        await EnableFeature(pair);
        var layers = await BuildStandalone(pair);
        var ground = layers[0];
        await LayTiles(pair, ground, new Vector2i(-4, -4), new Vector2i(4, 4));

        var levels = server.System<CEZLevelsSystem>();
        EntityUid? groundParent = null;

        await server.WaitAssertion(() =>
        {
            groundParent = entMan.GetComponent<TransformComponent>(ground).ParentUid;
            var grid = entMan.GetComponent<MapGridComponent>(ground);

            Assert.That(levels.WgIsFlyableGrid(ground), Is.False);
            Assert.That(levels.TryEnterTransit((ground, grid), preferUpperGap: true), Is.False, "the planet entered transit");
            Assert.That(levels.WgLiftoffHold(ground), Is.Not.Null, "liftoff had no reason to refuse the planet");
            Assert.That(server.System<ShuttleSystem>().CanFTL(ground, out _), Is.False, "the planet could FTL");

            var console = entMan.SpawnEntity("ComputerShuttle", new EntityCoordinates(ground, new Vector2(0.5f, 0.5f)));
            Assert.That(entMan.GetComponent<TransformComponent>(console).GridUid, Is.EqualTo(ground));
        });

        await HoldVertical(pair, ground, ShuttleButtons.AscendZ);
        await server.WaitRunTicks(pair.SecondsToTicks(4));

        await server.WaitAssertion(() =>
        {
            var xform = entMan.GetComponent<TransformComponent>(ground);
            Assert.That(xform.ParentUid, Is.EqualTo(groundParent), "holding ascend on planet ground moved the planet");
            Assert.That(xform.MapUid, Is.EqualTo(ground));
        });

        await Teardown(pair, layers);
        await pair.CleanReturnAsync();
    }
}
