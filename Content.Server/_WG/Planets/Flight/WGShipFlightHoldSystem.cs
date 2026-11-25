using Content.Server._CE.ZLevels.Core;
using Content.Server._CE.ZLevels.Flight;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Flight.Components;
using Robust.Shared.Map.Components;

namespace Content.Server._WG.Planets.Flight;

/// <summary>
/// A flyer stepping off a ship holds the level it left at. Flight keeps the level it started on, and a ship carrying
/// the flyer up never moves it, so leaving the hull high above that level pulled the flyer straight down.
/// </summary>
public sealed partial class WGShipFlightHoldSystem : EntitySystem
{
    [Dependency] private CEZFlightSystem _flight = default!;

    public override void Initialize()
    {
        base.Initialize();

        // After z-levels, which first moves a flyer leaving a ship in transit onto the level below.
        SubscribeLocalEvent<CEZFlyerComponent, EntParentChangedMessage>(OnParentChanged,
            after: new[] { typeof(CEZLevelsSystem) });
    }

    private void OnParentChanged(Entity<CEZFlyerComponent> ent, ref EntParentChangedMessage args)
    {
        if (!ent.Comp.Active || args.OldParent is not { } old || !HasComp<MapGridComponent>(old)
            || HasComp<MapComponent>(old) || !TryComp<CEZPhysicsComponent>(ent, out var zPhys))
            return;

        var level = (int) MathF.Floor(zPhys.CurrentZLevel + zPhys.LocalPosition);
        if (ent.Comp.TargetMapHeight < level)
            _flight.SetTargetHeight((ent, ent.Comp), level);
    }
}
