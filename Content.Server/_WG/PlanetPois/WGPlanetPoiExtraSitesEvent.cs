using Content.Shared._WG.PlanetPois;
using Content.Shared._WG.Planets;
using Robust.Shared.Prototypes;

namespace Content.Server._WG.PlanetPois;

/// <summary>
/// Raised broadcast as a world's sites are drawn from its table, for other modules to add sites of their own this round.
/// Added sites are placed first, before the table's.
/// </summary>
[ByRefEvent]
public readonly record struct WGPlanetPoiExtraSitesEvent(EntityUid Ground, ProtoId<WGPlanetSurfacePrototype> Surface, List<WGPlanetPoiEntry> Sites);
