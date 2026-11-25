namespace Content.Server._WG.Caverns;

/// <summary>On a viewer that has an eye on the cavern under a ground, so losing it takes a wider margin than gaining it.</summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class WGCavernViewerComponent : Component
{
    /// <summary>The ground whose cavern the viewer sees into.</summary>
    [ViewVariables]
    public EntityUid Ground;
}
