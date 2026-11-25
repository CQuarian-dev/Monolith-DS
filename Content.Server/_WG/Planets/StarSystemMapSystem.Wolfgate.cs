using System.Numerics;
using Content.Server._WG.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._Mono.ForceParent;
using Robust.Shared.Map;

namespace Content.Server._FarHorizons.StarSystem;

public sealed partial class StarSystemMapSystem
{
    [Dependency] private WGPlanetRegistrySystem _wgPlanets = default!;
    [Dependency] private ForceParentSystem _wgForceParent = default!;

    /// <summary>Hands a freshly spawned sector body to the Wolfgate planet registry.</summary>
    private void WgPlanetSpawned(Entity<StarSystemMapComponent> map, EntityUid planetEntity, Planet planet)
    {
        WgPinBody(map, planetEntity, planet.Position);
        _wgPlanets.RegisterPlanet(map, planetEntity, planet);
    }

    /// <summary>
    /// Pins a star or planet to the sector map. Grid traversal otherwise parents it to a hull that arrives on top
    /// of it, which carries the body (and its orbit) off and deletes it, with the whole planet, when the hull goes.
    /// </summary>
    private void WgPinBody(EntityUid map, EntityUid body, Vector2 position)
    {
        _wgForceParent.SetForceParent(body, new EntityCoordinates(map, position));
    }
}
