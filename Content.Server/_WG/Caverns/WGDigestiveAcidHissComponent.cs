namespace Content.Server._WG.Caverns;

/// <summary>On whoever digestive acid is burning: the hiss looping on them while it does.</summary>
[RegisterComponent, Access(typeof(WGDigestiveAcidHissSystem))]
public sealed partial class WGDigestiveAcidHissComponent : Component
{
    /// <summary>The looping stream attached to the victim.</summary>
    [ViewVariables]
    public EntityUid? Stream;
}
