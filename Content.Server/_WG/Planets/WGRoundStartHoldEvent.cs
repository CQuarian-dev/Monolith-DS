namespace Content.Server._WG.Planets;

/// <summary>Raised broadcast each time the ticker goes to start a round; a handler sets <see cref="Hold"/> to wait instead.</summary>
[ByRefEvent]
public record struct WGRoundStartHoldEvent(bool Force)
{
    public bool Hold;
}
