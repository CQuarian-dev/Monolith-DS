using Content.Shared._WG.Planets;

namespace Content.Server._WG.Planets;

/// <summary>Raised broadcast once a planet's ground map exists, before any of it generates.</summary>
[ByRefEvent]
public readonly record struct WGPlanetGroundSpawnedEvent(EntityUid Ground, WGPlanetSurfacePrototype Surface);
