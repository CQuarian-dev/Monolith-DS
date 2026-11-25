using Robust.Shared.Input;

namespace Content.Shared._WG.LandingGuide;

/// <summary>The landing guide's key, bound in the pilot's shuttle input context.</summary>
[KeyFunctions]
public static class WGLandingGuideKeys
{
    /// <summary>Turns the landing guide off and on again while piloting.</summary>
    public static readonly BoundKeyFunction WGToggleLandingGuide = "WGToggleLandingGuide";
}
