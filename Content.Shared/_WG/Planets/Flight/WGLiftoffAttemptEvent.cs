namespace Content.Shared._WG.Planets.Flight;

/// <summary>
/// Raised on every member of a liftoff transit set before the movement gates; cancel with a localised reason to veto.
/// </summary>
[ByRefEvent]
public record struct WGLiftoffAttemptEvent
{
    public bool Cancelled;
    public string? Reason;
}
