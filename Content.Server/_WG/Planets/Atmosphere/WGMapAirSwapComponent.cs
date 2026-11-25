using Content.Shared.Atmos;

namespace Content.Server._WG.Planets.Atmosphere;

/// <summary>The map air its outdoor tiles were last woken for, so small drifts swap the air without waking them.</summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class WGMapAirSwapComponent : Component
{
    /// <summary>The mixture the map's tiles were last activated with.</summary>
    [ViewVariables]
    public GasMixture? Activated;
}
