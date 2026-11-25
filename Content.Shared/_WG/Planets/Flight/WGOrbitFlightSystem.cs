using Content.Shared._CE.ZLevels.Flight;
using Content.Shared._CE.ZLevels.Flight.Components;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._WG.Planets.Flight;

/// <summary>
/// Wings and z-flight jets don't work in a planet's orbit layer: it is vacuum, and flying between it and the air layer
/// below desynced the flyer. Flight can't start there and stops once a flyer is in one, also when a ship carries it up.
/// </summary>
public sealed partial class WGOrbitFlightSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZFlyerComponent, CEStartFlightAttemptEvent>(OnStartAttempt);
    }

    private void OnStartAttempt(EntityUid uid, CEZFlyerComponent comp, CEStartFlightAttemptEvent args)
    {
        if (!InOrbit(uid))
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("wg-orbit-flight-refused"), uid, uid);
    }

    /// <summary>Server only, and polled: a flyer on a ship that hops into orbit never changes parent itself.</summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient || _timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        // The flight system is abstract here, so it can't be a dependency.
        var flight = EntityManager.System<CESharedZFlightSystem>();
        var query = EntityQueryEnumerator<CEZFlyerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var flyer, out var xform))
        {
            if (!flyer.Active || !HasComp<WGOrbitLayerComponent>(xform.MapUid))
                continue;

            flight.DeactivateFlight((uid, flyer));
            _popup.PopupEntity(Loc.GetString("wg-orbit-flight-stopped"), uid, uid);
        }
    }

    private bool InOrbit(EntityUid uid)
    {
        return HasComp<WGOrbitLayerComponent>(Transform(uid).MapUid);
    }
}
