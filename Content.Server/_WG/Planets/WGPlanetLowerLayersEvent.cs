using System.Numerics;
using Content.Shared._WG.Planets;

namespace Content.Server._WG.Planets;

/// <summary>Raised broadcast before a planet's depths are added; handlers append uninitialised maps.</summary>
// Lower[i] becomes depth -(i + 1), so handlers add nearest first.
[ByRefEvent]
public readonly record struct WGPlanetLowerLayersEvent(
    EntityUid Ground, WGPlanetSurfacePrototype Surface, Vector2 Centre, List<EntityUid> Lower);
