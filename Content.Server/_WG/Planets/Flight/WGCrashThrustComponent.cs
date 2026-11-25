namespace Content.Server._WG.Planets.Flight;

/// <summary>Last firing command retained only when the impact severs this engine from all pilot consoles.</summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class WGCrashThrustComponent : Component
{
    public bool Detached;
    public TimeSpan ExpiresAt;
}
