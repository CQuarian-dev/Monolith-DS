using System.Collections.Generic;
using System.Numerics;
using Content.Server._FarHorizons.StarSystem;
using Content.Server._WG.Planets;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Lathe;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences;
using Content.Shared.Research.Prototypes;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._LuaM.CCVar;
using Content.Shared._WG.CCVar;
using Content.Shared._WG.GroundCover;
using Content.Shared._WG.Planets;
using Content.Shared._WG.WolfWeather;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

// -- Планеты Wolfgate выключаются cvar'ами без правки кода: по умолчанию миров нет, и одного wg.planet_networks мало
[TestFixture]
[TestOf(typeof(WGPlanetNetworkSystem))]
public sealed class PlanetsSwitchedOffTest
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task SectorAndEquipmentFollowBothSwitches(bool planets, bool gridNetworks)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            server.CfgMan.SetCVar(PlanetCVars.PlanetNetworks, planets);
            server.CfgMan.SetCVar(LuaMCVars.ZLevelsGridNetworks, gridNetworks);
            var enabled = planets && gridNetworks;
            var registry = server.System<WGPlanetRegistrySystem>();
            var stars = server.System<StarSystemMapSystem>();
            var layout = registry.SectorLayout("SystemKyphrus");
            var system = stars.BuildPlanetarySystem(layout);
            if (!enabled)
            {
                var sector = server.EntMan.EnsureComponent<StarSystemMapComponent>(map.MapUid);
                stars.SetSystem((map.MapUid, sector), "SystemKyphrus");
                Assert.That(sector.System, Is.EqualTo(layout));
                system = sector.StarSystem;
                Assert.That(server.EntMan.Count<WGSectorPlanetComponent>(), Is.Zero);
            }

            Assert.That(system.Planets, Has.Count.EqualTo(enabled ? 6 : 5));
            Assert.That(system.Planets[2].Name, Is.EqualTo(enabled ? "Verda" : "Asclepiu"));
            Assert.That(system.Planets[2].Position.Length(), Is.EqualTo(enabled ? 11500 : 12800).Within(0.1));
            Assert.That(system.Planets[3].Position.Length(), Is.EqualTo(enabled ? 14000 : 17500).Within(0.1));
            Assert.That(system.Planets[4].Position.Length(), Is.EqualTo(enabled ? 14500 : 27500).Within(0.1));

            var recipes = new HashSet<ProtoId<LatheRecipePrototype>>();
            server.System<SharedLatheSystem>().AddRecipesFromPacks(recipes, ["WGPlanetStatic", "EngineeringBoardsStatic", "ToolsStatic"]);
            Assert.That(recipes.Contains("WGParachute"), Is.EqualTo(enabled));
            Assert.That(recipes.Contains("WGLandingThrusterKit"), Is.EqualTo(enabled));
            Assert.That(recipes.Contains("WGWeatherForecastComputerCircuitboard"), Is.EqualTo(enabled));
            Assert.That(recipes.Contains("Shovel"), Is.EqualTo(enabled));
            Assert.That(recipes.Contains("Wrench"), Is.True);

            Assert.That(server.ProtoMan.Index<CargoProductPrototype>("WGPlanetTimepiece").Available(server.CfgMan), Is.EqualTo(enabled));
            server.System<SharedWolfWeatherSystem>().SetWeather(map.MapUid, "WGRainLight", null, 1);
            var deps = server.ResolveDependency<IEntitySystemManager>().DependencyCollection;
            var role = new RoleLoadout("JobContractor");
            var profile = HumanoidCharacterProfile.DefaultWithSpecies();
            Assert.That(role.IsHidden(profile, null, "WGPlanetTimepiece", deps), Is.EqualTo(!enabled));
            Assert.That(role.IsValid(profile, null, "WGPlanetTimepiece", deps, out _), Is.EqualTo(enabled));
        });
        await server.WaitRunTicks(2);
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<WGGroundWeatherComponent>(map.MapUid),
            Is.EqualTo(planets && gridNetworks)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoWorldsUnlessBothCVarsAreOn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var cfg = server.CfgMan;
        var proto = server.ResolveDependency<IPrototypeManager>();
        var networks = server.System<WGPlanetNetworkSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(cfg.GetCVar(PlanetCVars.PlanetNetworks), Is.False);
            Assert.That(cfg.GetCVar(LuaMCVars.ZLevelsGridNetworks), Is.False);

            var surface = proto.Index<WGPlanetSurfacePrototype>("WGSurfaceAsclepiu");
            Assert.That(networks.BuildNetwork(surface, Vector2.Zero, "Asclepiu", null), Is.Null);

            cfg.SetCVar(PlanetCVars.PlanetNetworks, true);
            Assert.That(networks.BuildNetwork(surface, Vector2.Zero, "Asclepiu", null), Is.Null);

            Assert.That(server.EntMan.Count<WGPlanetNetworkComponent>(), Is.Zero);
        });

        await pair.CleanReturnAsync();
    }
}
