using Content.Server._WG.Planets.Hop;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._WG.Planets;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    /// <summary>True when FTL departure is refused: below orbit on a planet, mid-transit or mid-hop.</summary>
    // Checked from TrySetupFTL and FTLToDock, since the console's beacon and free-FTL paths skip CanFTLTo.
    public bool WgRefusesFtlDeparture(EntityUid shuttleUid)
    {
        if (HasComp<WGPlanetHopComponent>(shuttleUid))
            return true;

        if (Transform(shuttleUid).MapUid is not { } mapUid)
            return false;

        if (HasComp<CEZTransitMapComponent>(mapUid))
            return true;

        return HasComp<WGPlanetLayerComponent>(mapUid) && !HasComp<WGOrbitLayerComponent>(mapUid);
    }
}
