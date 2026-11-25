using Content.Shared._WG.PlanetCities;
using Content.Shared._WG.Planets;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map.Components;

namespace Content.Shared._CE.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] private SharedRoofSystem _wgRoof = default!;

    /// <summary>
    /// Whether a tile one layer down is roofed over, so a body above it stands on the roof instead of dropping through:
    /// a roofed tile on a planet layer, or any tile of a vessel there, which is always under cover.
    /// </summary>
    private bool WgRoofedBelow(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        var map = Transform(grid).MapUid;
        if (!HasComp<WGPlanetLayerComponent>(map))
            return false;

        if (grid != map && !HasComp<WGPlanetCityGridComponent>(grid))
            return true;

        return TryComp<RoofComponent>(grid, out var roof) && _wgRoof.IsRooved((grid, gridComp, roof), tile);
    }
}
