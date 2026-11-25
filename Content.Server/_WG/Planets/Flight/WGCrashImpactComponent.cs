using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
namespace Content.Server._WG.Planets.Flight;

/// <summary>Marks a hull that has crashed onto a planet and paces its impact effects.</summary>
[RegisterComponent, UnsavedComponent, AutoGenerateComponentPause]
public sealed partial class WGCrashImpactComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextImpact;

    // Temporary seam indices, consumed once the engine has separated the floor grids.
    public HashSet<Vector2i>? LatticeSeam;
}
