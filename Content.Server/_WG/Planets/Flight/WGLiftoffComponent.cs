namespace Content.Server._WG.Planets.Flight;

/// <summary>Latched ascent intent; feeds CE upward input until takeoff or cancellation.</summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class WGLiftoffComponent : Component
{
    /// <summary>The console that engaged liftoff.</summary>
    [DataField]
    public EntityUid Console;

    /// <summary>The pilot who engaged liftoff; losing this pilot cancels the latch.</summary>
    [DataField]
    public EntityUid Pilot;
}
