using Robust.Shared.Map;

namespace Content.Server._WG.Planets.Hop;

/// <summary>Raised on every grid of a hop so a system can refuse it for its own reason.</summary>
[ByRefEvent]
public record struct WGPlanetHopAttemptEvent(EntityUid Uid, bool Cancelled, string Reason);

/// <summary>A hull's warm-up is over and it has left for the hold map.</summary>
[ByRefEvent]
public readonly record struct WGPlanetHopStartedEvent(EntityUid Hull, EntityCoordinates Target);

/// <summary>A hull and the grids docked to it have come out on the target map.</summary>
[ByRefEvent]
public readonly record struct WGPlanetHopCompletedEvent(EntityUid Hull, EntityUid MapUid);
