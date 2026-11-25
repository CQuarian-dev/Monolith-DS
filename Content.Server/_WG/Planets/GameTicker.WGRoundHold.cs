using Content.Server._WG.Planets;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    /// <summary>True while something, such as a round-start world still forming, holds the round start back.</summary>
    private bool WGRoundStartHeld(bool force)
    {
        var ev = new WGRoundStartHoldEvent(force);
        RaiseLocalEvent(ref ev);
        return ev.Hold;
    }
}
