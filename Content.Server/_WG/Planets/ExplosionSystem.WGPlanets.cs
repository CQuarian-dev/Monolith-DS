using Content.Server._WG.Planets;

namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    [Dependency] private WGPlanetTileBreakSystem _wgPlanetTileBreak = default!;
}
