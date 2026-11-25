using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._CE.ZLevels.Core;

/// <summary>
/// Keeps planet ground out of z-level flight. A planet layer's terrain is a grid on the map entity itself, so a console
/// built on bare ground would otherwise lift the whole planet into a transit map, where its missing physics body stops
/// the broadphase for the whole server.
/// </summary>
public sealed partial class CEZLevelsSystem
{
    /// <summary>Whether a grid can fly between z-levels: a real hull with a body, never a map's own grid.</summary>
    public bool WgIsFlyableGrid(EntityUid grid)
    {
        return !HasComp<MapComponent>(grid) && HasComp<PhysicsComponent>(grid);
    }

    /// <summary>Refuses vertical input from a console on ground that can't fly, saying so now and then.</summary>
    private bool WgRefusesGroundInput(EntityUid pilot, EntityUid console, EntityUid grid)
    {
        if (WgIsFlyableGrid(grid))
            return false;

        if (_timing.CurTime >= _wgNextLiftoffInputPopup.GetValueOrDefault(console))
        {
            _wgNextLiftoffInputPopup[console] = _timing.CurTime + WGLiftoffInputPopupCooldown;
            _popup.PopupEntity(Loc.GetString("wg-liftoff-refused-ground"), console, pilot);
        }

        return true;
    }
}
