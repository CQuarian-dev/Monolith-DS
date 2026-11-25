using Content.Shared._WG.Planets;

namespace Content.Server._WG.Planets;

/// <summary>Raised broadcast last in BuildNetwork, after InitializeZNetwork re-stamped network components.</summary>
[ByRefEvent]
public readonly record struct WGPlanetNetworkBuiltEvent(
    EntityUid Network, EntityUid Ground, WGPlanetSurfacePrototype Surface, IReadOnlyList<EntityUid> Lower);
