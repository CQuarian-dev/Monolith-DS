using Content.Server._CE.ZLevels.Core;

namespace Content.Server.Physics.Controllers;

public sealed partial class MoverController
{
    [Dependency] private CEZLevelsSystem _wgFlightLevels = default!;

    /// <summary>Thrust left for manoeuvres: after supporting the ship's weight in atmosphere, or after its cargo load in space.</summary>
    private float WgAtmosphereManeuvering(EntityUid grid) =>
        _thruster.WgInAtmosphere(grid) ? _wgFlightLevels.WgManeuveringFactor(grid) : WgCargoSpaceFactor(grid);
}
